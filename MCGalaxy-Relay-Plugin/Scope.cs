using System;
using System.Linq;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        public enum ScopeKind : byte {
            Player = 0,
            Map = 1,
            Server = 2,
        }


        public interface IScope {
            Player[] GetTargets();
        }

        public static IScope TryGetScope(Player sender, byte channel, byte nScopeKind, byte nScopeExtra) {
            ScopeKind scopeKind = (ScopeKind)nScopeKind;
            if (scopeKind == ScopeKind.Player) {
                return new ScopePlayer(nScopeExtra);
            } else if (scopeKind == ScopeKind.Map) {
                return new ScopeMap(sender.level, channel, nScopeExtra);
            } else if (scopeKind == ScopeKind.Server) {
                return new ScopeServer(channel, nScopeExtra);
            } else {
                throw new Exception(
                    string.Format(
                        "Invalid scope {0} {1}",
                        nScopeKind,
                        nScopeExtra
                    )
                );
            }
        }

        // a single player to target;
        // ScopeExtra: { u8 player id }
        public struct ScopePlayer : IScope {
            private byte targetId;
            public ScopePlayer(byte b) {
                this.targetId = b;
            }

            public Player[] GetTargets() {
                var targetId = this.targetId;
                return PlayerInfo.Online.Items
                    .Where((p) => p.Supports(CpeExt.PluginMessages))
                    .Where((p) => p.id == targetId)
                    .ToArray();
            }
        }


        // all players in this player's map
        public struct ScopeMap : IScope {
            private Level level;
            private byte channel;

            // only send to players that have the same plugin
            // that this channel was sent from
            private bool samePlugin;
            public ScopeMap(Level level, byte channel, byte b) {
                this.level = level;
                this.channel = channel;
                samePlugin = b != 0;
            }

            public Player[] GetTargets() {
                var channel = this.channel;
                var samePlugin = this.samePlugin;
                return level.getPlayers()
                    .Where((p) => p.Supports(CpeExt.PluginMessages))
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
        public struct ScopeServer : IScope {
            private byte channel;

            // only send to players that have the same plugin
            // that this channel was sent from
            private bool samePlugin;
            public ScopeServer(byte channel, byte b) {
                this.channel = channel;
                samePlugin = b != 0;
            }

            public Player[] GetTargets() {
                var channel = this.channel;
                var samePlugin = this.samePlugin;
                return PlayerInfo.Online.Items
                    .Where((p) => p.Supports(CpeExt.PluginMessages))
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

        public static bool HasPlugin(Player p, byte channelType) {

            // TODO convert these to add to the cpe string list instead
            if (channelType == (byte)ChannelType.Cef) {
                // TODO version check
                return true;
            } else if (channelType == (byte)ChannelType.VoiceChat) {
                // TODO version check
                return true;
            } else {
                // assume yes
                return true;
            }
        }

    }
}
