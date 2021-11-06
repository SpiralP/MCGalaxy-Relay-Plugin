
using System;
using System.Collections.Generic;

using PlayerIdToPacketId = System.Collections.Generic.Dictionary<byte, byte>;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        // [sender id][packet id][target id] = packet id
        private static Dictionary<PlayerIdToPacketId, PlayerIdToPacketId> Map
            = new Dictionary<PlayerIdToPacketId, PlayerIdToPacketId>();


        public static void OnPluginMessageReceived(Player p, byte channel, byte[] data) {
            Logger.Log(
                LogType.Debug,
                "PluginMessage channel: {0} from {1}",
                channel,
                p.truename
            );

            IIncomingPacket packet = null;

            try {
                int i = 0;
                Flags flags = Flags.Decode(data[i++]);

                byte[] nextData = new byte[data.Length - i];
                Array.Copy(data, i, nextData, 0, nextData.Length);
                if (flags.isStart) {
                    packet = IncomingStartPacket.TryCreate(p, channel, flags, nextData);
                } else {
                    packet = IncomingContinuationPacket.TryCreate(p, channel, flags, nextData);
                }
            } catch (Exception e) {
                Warn("Exception when parsing PluginMessage from {0}: {1}", p, e);
            }

            try {
                if (packet != null) {
                    packet.Relay();
                }
            } catch (Exception e) {
                Warn("Exception when sending PluginMessage from {0}: {1}", p, e);
            }
        }
    }
}

