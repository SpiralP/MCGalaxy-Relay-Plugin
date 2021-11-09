using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {
        // TODO concurrent dictionary all

        public struct StreamTarget {
            public Player player;
            public byte streamId;
            // TODO sent bytes (to know when to remove)
            public UInt16 dataLength;
            public UInt16 dataSent;
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


            // TODO cleanup on disconnect or stream finished
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

            public static void HandlePlayerDisconnect(Player p) {
                // lock (GetCacheLock(channel)) {
                // TODO ForChannel.Keys
                // }
            }

            private void _HandlePlayerDisconnect(Player p) {
                // TODO
            }


            public StreamTarget[] StoreTargets(Scope scope) {
                var players = scope.GetPlayers();

                var targetsList = new List<StreamTarget>();
                foreach (var p in players) {
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
                        continue;
                    }

                    // reserve outgoing id on target
                    Debug(
                        "new outgoing ids: {0} {1} -> {2} {3}",
                        scope.streamId, scope.sender.truename,
                        targetStreamId, p.truename
                    );
                    outgoingIds.Add(targetStreamId);
                    targetsList.Add(new StreamTarget {
                        player = p,
                        streamId = targetStreamId,
                    });
                }

                var targets = targetsList.ToArray();

                var idToTargets = incomingIds.GetOrAdd(scope.sender, (_) => new ConcurrentDictionary<byte, StreamTarget[]>());
                // make note of new stream from client
                return idToTargets.AddOrUpdate(scope.streamId, (_) => targets, (_, _) => {
                    Debug("restarting stream for {0} ({1})", scope.sender.truename, scope.streamId);
                    return targets;
                });
            }

            public StreamTarget[] GetTargets(Player sender, byte streamId) {
                if (incomingIds.TryGetValue(sender, out var value)) {
                    if (value.TryGetValue(streamId, out var scope)) {
                        return scope;
                    }
                }

                return null;
            }
        }


    }
}
