
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        // remember who has sent what channel ids;
        // this is used in HasPlugin() for knowing who can receive a certain channel id
        private static ConcurrentDictionary<Player, HashSet<byte>> PlayerSentOnChannel = new ConcurrentDictionary<Player, HashSet<byte>>();


        public static void OnPluginMessageReceived(Player sender, byte channel, byte[] data) {
            if (channel < RelayChannelStartIndex) return;

            // keep track of who sends what channels
            PlayerSentOnChannel.AddOrUpdate(sender, (_) => {
                Debug(
                    "received new channel {0} from {1}",
                    (ChannelType)channel,
                    sender.truename
                );

                var sentChannels = new HashSet<byte>();
                sentChannels.Add(channel);
                return sentChannels;
            }, (_, sentChannels) => {
                sentChannels.Add(channel);
                return sentChannels;
            });


            IncomingPacket packet = null;

            try {
                packet = IncomingPacket.TryCreate(sender, channel, data);
            } catch (Exception e) {
                Warn("Exception when parsing PluginMessage from {0}: {1}", sender.truename, e);
                return;
            }

            StreamTarget[] targets = null;

            try {
                Store.With(channel, (store) => {
                    targets = packet.GetTargets(store);
                });
                Debug(
                    "Relay {0}: {1} {2} {3} ({4}) -> [ {5} ]",
                    (ChannelType)channel,
                    packet.flags.isStart ? "start" : "continue",
                    packet.flags.isStart ? ((IncomingStartPacket)packet).scope.kind : "",
                    sender.truename,
                    packet.flags.streamId,
                    targets
                        .Select((t) => string.Format("{0} ({1})", t.player.truename, t.streamId))
                        .Join(", ")
                );
            } catch (Exception e) {
                Warn(
                    "Exception when trying to reserve ids from {0}: {1}",
                    sender.truename, e
                );
                return;
            }


            try {
                packet.Relay(targets);
            } catch (Exception e) {
                Warn("Exception when sending PluginMessage from {0}: {1}", sender.truename, e);
                // TODO what do?
            }

        }


        public static void OnPlayerDisconnect(Player p, string reason) {
            Debug(
                "player disconnected {0}",
                p.truename
            );
            Store.HandlePlayerDisconnectAll(p);
            PlayerSentOnChannel.TryRemove(p, out _);
        }
    }
}

