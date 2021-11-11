using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {
        public class StreamTarget {
            public Player player;
            public byte streamId;
            public UInt16 dataLength;
            public UInt16 dataSent;

            public StreamTarget(
                Player player,
                byte streamId,
                UInt16 dataLength
            ) {
                this.player = player;
                this.streamId = streamId;
                this.dataLength = dataLength;
                this.dataSent = 0;
            }

            public void Sent(UInt16 sentLength) {
                this.dataSent += sentLength;
            }

            public bool IsFinished() {
                return dataSent >= dataLength;
            }
        }

        public class Store {
            // [Player sender][incoming stream id] = scope
            // which contains a list of players to send to (and what stream id to use for each target)
            public ConcurrentDictionary<Player, ConcurrentDictionary<byte, StreamTarget[]>> incomingIds;

            // [Player target][outgoing stream id] = exists
            public ConcurrentDictionary<Player, HashSet<byte>> outgoingIds;

            // [Player sender][incoming stream id] = timer
            // after timeout is reached, we free reserved ids
            private ConcurrentDictionary<Player, ConcurrentDictionary<byte, System.Timers.Timer>> timers;


            public Store() {
                this.incomingIds = new ConcurrentDictionary<Player, ConcurrentDictionary<byte, StreamTarget[]>>();
                this.outgoingIds = new ConcurrentDictionary<Player, HashSet<byte>>();
                this.timers = new ConcurrentDictionary<Player, ConcurrentDictionary<byte, System.Timers.Timer>>();
            }

            private static ConcurrentDictionary<byte, Store> ForChannel = new ConcurrentDictionary<byte, Store>();
            private static ConcurrentDictionary<byte, object> ForChannelLocks = new ConcurrentDictionary<byte, object>();
            private static object GetCacheLock(byte channel) {
                return ForChannelLocks.GetOrAdd(channel, (_) => new object());
            }

            public static T With<T>(byte channel, Func<Store, T> callback) {
                lock (GetCacheLock(channel)) {
                    var store = ForChannel.GetOrAdd(channel, (_) => new Store());
                    return callback(store);
                }
            }
            public static void With(byte channel, Action<Store> callback) {
                With<object>(channel, (store) => {
                    callback(store);
                    return new object();
                });
            }

            public StreamTarget[] CreateTargets(Scope scope, UInt16 dataLength) {
                var targets = scope.GetPlayers()
                    .Select((p) => {
                        var outgoingIds = this.outgoingIds.GetOrAdd(p, (_) => new HashSet<byte>());

                        // find free id for target
                        byte targetStreamId = 0;
                        bool found;
                        while (true) {
                            if (!outgoingIds.Contains(targetStreamId)) {
                                found = true;
                                break;
                            }

                            if (targetStreamId == 0xFF) {
                                found = false;
                                break;
                            }
                            targetStreamId += 1;
                        }
                        if (!found) {
                            // skip this player
                            Warn(
                                "couldn't find free outgoing stream id for {0}",
                                p.truename
                            );
                            return null;
                        }

                        // reserve outgoing id on target
                        Debug(
                            "new outgoing ids: {0} {1} -> {2} {3}",
                            scope.streamId, scope.sender.truename,
                            targetStreamId, p.truename
                        );
                        outgoingIds.Add(targetStreamId);
                        return new StreamTarget(
                            p,
                            targetStreamId,
                            dataLength
                        );
                    })
                    .Where((target) => target != null)
                    .ToArray();

                if (targets.Length != 0) {
                    var idToTargets = incomingIds.GetOrAdd(scope.sender, (_) => new ConcurrentDictionary<byte, StreamTarget[]>());
                    // make note of new stream from client
                    var ag = idToTargets.AddOrUpdate(scope.streamId, (_) => targets, (_, _) => {
                        Debug("restarting stream for {0} ({1})", scope.sender.truename, scope.streamId);
                        return targets;
                    });

                    StartTimeoutTimer(scope.sender, scope.streamId);

                    return ag;
                } else {
                    Debug("no targets");
                    return targets;
                }
            }

            public StreamTarget[] GetTargets(Player sender, byte streamId) {
                if (this.incomingIds.TryGetValue(sender, out var incomingIds)) {
                    if (incomingIds.TryGetValue(streamId, out var targets)) {
                        return targets;
                    }
                }

                throw new Exception(
                    string.Format(
                        "couldn't find targets for {0} stream {1}",
                        sender.truename,
                        streamId
                    )
                );
            }



            public static void HandlePlayerDisconnectAll(Player p) {
                for (int channel = 0; channel <= 255; channel++) {
                    Store.With((byte)channel, (store) => {
                        store.HandlePlayerDisconnect(p);
                    });
                }
            }

            private void HandlePlayerDisconnect(Player p) {
                if (this.timers.TryRemove(p, out var timers)) {
                    foreach (var pair in timers) {
                        Debug("ClearTimeoutTimer {0}", pair.Key);
                        ClearTimeoutTimer(p, pair.Key);
                    }
                }
                if (this.outgoingIds.TryRemove(p, out var outgoingIds)) {
                    Debug(
                        "player disconnected with {0} outgoing streams left",
                        outgoingIds.Count
                    );
                }
                if (this.incomingIds.TryRemove(p, out var incomingIds)) {
                    Debug(
                        "player disconnected with {0} incoming streams left",
                        incomingIds.Count
                    );
                    foreach (var pair in incomingIds) {
                        foreach (var target in pair.Value) {
                            Debug("CleanupTarget");
                            CleanupTarget(target);
                        }
                    }
                }
            }

            public void CleanupFromSender(Player sender, byte incomingStreamId) {
                Debug(
                    "cleanup for sender {0}",
                    sender.truename
                );
                ClearTimeoutTimer(sender, incomingStreamId);
                if (incomingIds.TryGetValue(sender, out var idToTargets)) {
                    // make note of new stream from client
                    if (idToTargets.TryRemove(incomingStreamId, out var targets)) {
                        foreach (var target in targets) {
                            CleanupTarget(target);
                        }
                    }
                }
            }
            public void CleanupTarget(StreamTarget target) {
                Debug(
                    "cleanup target {0}",
                    target.player.truename
                );
                if (this.outgoingIds.TryGetValue(target.player, out var outgoingIds)) {
                    outgoingIds.Remove(target.streamId);
                }
            }



            private void StartTimeoutTimer(Player sender, byte incomingStreamId) {
                var timers = this.timers.GetOrAdd(sender, (_) => new ConcurrentDictionary<byte, System.Timers.Timer>());
                timers.AddOrUpdate(incomingStreamId, (_) => {
                    Debug("new timer");
                    // TODO 10 seconds?
                    var timer = new System.Timers.Timer(10 * 1000) {
                        AutoReset = false
                    };
                    timer.Elapsed += (obj, elapsedEventArgs) => {
                        timer.Stop();
                        timer.Dispose();

                        Debug("timer finished, cleaning up sender {0} ({1})", sender.truename, incomingStreamId);
                        CleanupFromSender(sender, incomingStreamId);
                    };
                    timer.Start();
                    return timer;
                }, (_, timer) => {
                    // restart if existing
                    Debug("restart existing timer");
                    timer.Stop();
                    timer.Start();
                    return timer;
                });
            }
            private void ClearTimeoutTimer(Player sender, byte incomingStreamId) {
                if (this.timers.TryGetValue(sender, out var timers)) {
                    if (timers.TryRemove(incomingStreamId, out var timer)) {
                        timer.Stop();
                        timer.Dispose();
                    }
                }
            }


        }


    }
}
