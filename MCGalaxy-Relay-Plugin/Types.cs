
namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        public enum ChannelType : byte {
            Cef = 16,
            VoiceChat = 17,
        }


        public struct Flags {
            // is a start packet, else is a continuation
            public bool isStart;

            public byte packetId;

            public static Flags Decode(byte b) {
                bool isStart = (b & 0b1000_0000) != 0;
                byte packetId = (byte)(b & 0b0111_1111);
                return new Flags {
                    isStart = isStart,
                    packetId = packetId,
                };
            }

            public byte Encode() {
                byte b = (byte)(isStart ? 0b1000_0000 : 0);
                b |= (byte)(packetId & 0b0111_1111);
                return b;
            }
        }

    }
}
