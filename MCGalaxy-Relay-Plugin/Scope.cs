using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        public enum ScopeKind : byte {
            PlayerEntity = 0,
            Map = 1,
            Server = 2,
            PlayerTab = 3,
        }

        public abstract class Scope {
            public ScopeKind kind;
            public Player sender;
            public byte channel;
            public byte streamId;

            public Scope(
                ScopeKind kind,
                Player sender,
                byte channel,
                byte streamId
            ) {
                this.kind = kind;
                this.sender = sender;
                this.channel = channel;
                this.streamId = streamId;
            }

            public static Scope TryCreate(
                Player sender,
                byte channel,
                byte streamId,
                byte scopeKind,
                byte scopeExtra
            ) {
                if (scopeKind == (byte)ScopeKind.PlayerEntity) {
                    return new ScopePlayerEntity(sender, channel, streamId, scopeExtra);
                } else if (scopeKind == (byte)ScopeKind.Map) {
                    return new ScopeMap(sender, channel, streamId, scopeExtra);
                } else if (scopeKind == (byte)ScopeKind.Server) {
                    return new ScopeServer(sender, channel, streamId, scopeExtra);
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

            public virtual Player[] GetPlayers() {
                return PlayerInfo.Online.Items
                    .Where((p) => p != sender)
                    .Where((p) => p.Supports(CpeExt.PluginMessages))
                    .ToArray();
            }
        }


        // a single player to target;
        // ScopeExtra: { u8 player id }
        public class ScopePlayerEntity : Scope {
            /// <summary>
            /// A client wants to send a message to a player it can see who has this ID
            /// </summary>
            private readonly byte targetPlayerID;

            public ScopePlayerEntity(
                Player sender,
                byte channel,
                byte streamId,
                byte extra
            ) : base(
                ScopeKind.PlayerEntity,
                sender,
                channel,
                streamId
            ) {
                this.targetPlayerID = extra;
            }

            public override Player[] GetPlayers() {
                return base.GetPlayers()
                    .Where((candidate) => {
                        byte candidateID;
                        return sender.EntityList.GetID(candidate, out candidateID) && candidateID == targetPlayerID;
                    })
                    .ToArray();
            }
        }


        // all players in this player's map
        public class ScopeMap : Scope {
            private Level level;

            // only send to players that have the same plugin
            // that this channel was sent from
            private bool samePlugin;

            public ScopeMap(
                Player sender,
                byte channel,
                byte streamId,
                byte extra
            ) : base(
                ScopeKind.Map,
                sender,
                channel,
                streamId
            ) {
                this.samePlugin = (extra & 0b1000_000) != 0;
                this.level = sender.level;
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

            public ScopeServer(
                Player sender,
                byte channel,
                byte streamId,
                byte extra
            ) : base(
                ScopeKind.Server,
                sender,
                channel,
                streamId
            ) {
                this.samePlugin = (extra & 0b1000_000) != 0;
            }

            public override Player[] GetPlayers() {
                var channel = this.channel;
                var samePlugin = this.samePlugin;

                return base.GetPlayers()
                    .Where((p) => !samePlugin || HasPlugin(p, channel))
                    .ToArray();
            }
        }

        //TODO once EntityList has GetTabID api
        public class ScopePlayerTab {

        }

        public static bool HasPlugin(Player p, byte channelType) {
            if (PlayerSentOnChannel.TryGetValue(p, out var knownChannels)) {
                return knownChannels.Contains(channelType);
            } else {
                return false;
            }
        }

    }
}
