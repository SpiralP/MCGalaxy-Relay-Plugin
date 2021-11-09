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

            public Store() {
                this.incomingIds = new ConcurrentDictionary<Player, ConcurrentDictionary<byte, StreamTarget[]>>();
                this.outgoingIds = new ConcurrentDictionary<Player, HashSet<byte>>();
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

                var idToTargets = incomingIds.GetOrAdd(scope.sender, (_) => new ConcurrentDictionary<byte, StreamTarget[]>());
                // make note of new stream from client
                return idToTargets.AddOrUpdate(scope.streamId, (_) => targets, (_, _) => {
                    Debug("restarting stream for {0} ({1})", scope.sender.truename, scope.streamId);
                    return targets;
                });
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
                for (byte channel = 0; channel <= 255; channel++) {
                    Store.With(channel, (store) => {
                        store.HandlePlayerDisconnect(p);
                    });
                }
            }

            private void HandlePlayerDisconnect(Player p) {
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


        }


    }
}
