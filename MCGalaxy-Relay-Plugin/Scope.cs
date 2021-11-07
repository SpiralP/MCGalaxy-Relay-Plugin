using System;
using System.Collections.Generic;
using System.Linq;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        public struct PacketTarget {
            public Player player;
            public byte packetId;
            // TODO sent bytes (to know when to remove)
        }


        // TODO clear on disconnect
        // [Player sender][incoming packet id] = scope
        // which contains a list of players to send to (and what packet id to use for each target)
        private static Dictionary<Player, Dictionary<byte, Scope>> IncomingIds
            = new Dictionary<Player, Dictionary<byte, Scope>>();


        // TODO clear on disconnect
        // [Player target][outgoing packet id] = exists
        private static Dictionary<Player, HashSet<byte>> OutgoingIds
            = new Dictionary<Player, HashSet<byte>>();


        public enum ScopeKind : byte {
            Player = 0,
            Map = 1,
            Server = 2,
        }


        public class Scope {
            protected Player sender;
            protected byte channel;
            protected byte packetId;
            public PacketTarget[] targets;

            public virtual Player[] GetPlayers() {
                return PlayerInfo.Online.Items
                    .Where((p) => p.Supports(CpeExt.PluginMessages))
                    .ToArray();
            }

            public void ReserveTargetIds() {
                var players = GetPlayers();
                var targets = new List<PacketTarget>();
                foreach (var p in players) {
                    if (!OutgoingIds.TryGetValue(p, out var outgoingIds)) {
                        outgoingIds = new HashSet<byte>();
                        OutgoingIds.Add(p, outgoingIds);
                    }
                    // find free id for target
                    // TODO
                    byte targetPacketId = 1;
                    // for x in ... {outgoingIds..Has(2);}
                    // TODO no free ids???

                    // reserve outgoing id on target
                    Debug(
                        "{0} {1} -> {2} {3}",
                        packetId, sender.truename,
                        targetPacketId, p.truename
                    );
                    outgoingIds.Add(targetPacketId);
                    targets.Add(new PacketTarget {
                        player = p,
                        packetId = targetPacketId,
                    });
                }

                if (!IncomingIds.TryGetValue(sender, out var idToTargets)) {
                    idToTargets = new Dictionary<byte, Scope>();
                    IncomingIds.Add(sender, idToTargets);
                }
                // make note of new stream from client
                idToTargets.Add(packetId, this);
                // TODO client restarting on same id
            }

            public static Scope TryCreate(Player sender, byte channel, byte packetId, byte scopeKind, byte scopeExtra) {
                if (scopeKind == (byte)ScopeKind.Player) {
                    return ScopePlayer.TryCreate(sender, channel, packetId, scopeExtra);
                } else if (scopeKind == (byte)ScopeKind.Map) {
                    return ScopeMap.TryCreate(sender, channel, packetId, scopeExtra);
                } else if (scopeKind == (byte)ScopeKind.Server) {
                    return ScopeServer.TryCreate(sender, channel, packetId, scopeExtra);
                } else {
                    throw new Exception(
                        string.Format(
                            "Invalid scope kind {0} {1}",
                            scopeKind,
                            scopeExtra
                        )
                    );
                }
            }

            public static Scope GetForSender(Player sender, byte channel) {
                throw new NotImplementedException();
            }
        }


        // a single player to target;
        // ScopeExtra: { u8 player id }
        public class ScopePlayer : Scope {
            private byte targetId;
            public static ScopePlayer TryCreate(Player sender, byte channel, byte packetId, byte extra) {
                return new ScopePlayer {
                    sender = sender,
                    channel = channel,
                    packetId = packetId,
                    targetId = extra,
                };
            }

            public override Player[] GetPlayers() {
                var targetId = this.targetId;

                return base.GetPlayers()
                    .Where((p) => p.id == targetId)
                    .ToArray();
            }
        }


        // all players in this player's map
        public class ScopeMap : Scope {
            private Level level;

            // only send to players that have the same plugin
            // that this channel was sent from
            private bool samePlugin;
            public static ScopeMap TryCreate(Player sender, byte channel, byte packetId, byte extra) {
                var samePlugin = (extra & 0b1000_000) != 0;

                return new ScopeMap {
                    sender = sender,
                    channel = channel,
                    packetId = packetId,
                    level = sender.level,
                    samePlugin = samePlugin,
                };
            }

            public override Player[] GetPlayers() {
                var channel = this.channel;
                var level = this.level;
                var samePlugin = this.samePlugin;

                return base.GetPlayers()
                    .Where((p) => p.level == level)
                    .Where((p) => {
                        if (samePlugin) {
                            return HasPlugin(p, channel);
                        } else {
                            return true;
                        }
                    })
                    .ToArray();
            }
        }

        // all players in the server
        public class ScopeServer : Scope {

            // only send to players that have the same plugin
            // that this channel was sent from
            private bool samePlugin;
            public static ScopeServer TryCreate(Player sender, byte channel, byte packetId, byte extra) {
                var samePlugin = (extra & 0b1000_000) != 0;

                return new ScopeServer {
                    sender = sender,
                    channel = channel,
                    packetId = packetId,
                    samePlugin = samePlugin,
                };
            }

            public override Player[] GetPlayers() {
                var channel = this.channel;
                var samePlugin = this.samePlugin;

                return base.GetPlayers()
                    .Where((p) => !samePlugin || HasPlugin(p, channel))
                    .ToArray();
            }
        }


        public static bool HasPlugin(Player p, byte channelType) {
            if (PlayerSentOnChannel.TryGetValue(p.id, out List<byte> knownChannels)) {
                return knownChannels.Contains(channelType);
            } else {
                return false;
            }
        }

    }
}
