
using System;
using System.Collections.Generic;
using System.Linq;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {


        // TODO clear on disconnect
        // remember who has sent what channel ids;
        // this is used in HasPlugin() for knowing who can receive a certain channel id
        private static Dictionary<byte, List<byte>> PlayerSentOnChannel = new Dictionary<byte, List<byte>>();


        public static void OnPluginMessageReceived(Player sender, byte channel, byte[] data) {
            if (channel < RelayChannelStartIndex) return;

            IncomingPacket packet = null;

            try {
                packet = IncomingPacket.TryCreate(sender, channel, data);
            } catch (Exception e) {
                Warn("Exception when parsing PluginMessage from {0}: {1}", sender, e);
                return;
            }

            packet.ReserveTargetIds();
            Debug(
                "Relay {0}: {1} {2} ({3}) -> [ {3} ]",
                channel,
                packet.flags.isStart ? "start" : "continue",
                sender.truename,
                packet.flags.packetId,
                packet.scope.targets
                    .Select((t) => string.Format("{0} ({1})", t.player.truename, t.packetId))
                    .Join(", ")
            );

            try {
                packet.Relay();
            } catch (Exception e) {
                Warn("Exception when sending PluginMessage from {0}: {1}", sender, e);
                // TODO what do?
                return;
            }
        }
    }
}

