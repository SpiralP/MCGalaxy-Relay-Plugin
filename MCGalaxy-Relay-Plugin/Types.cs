
namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        // only act on channels >= RelayChannelStartIndex
        public const byte RelayChannelStartIndex = 200;

        public enum ChannelType : byte {
            Cef = 200,
            VoiceChat = 201,
        }

        public struct Flags {
            // is a start packet, else is a continuation
            public bool isStart;

            public byte streamId;

            public static Flags Decode(byte b) {
                bool isStart = (b & 0b1000_0000) != 0;
                byte streamId = (byte)(b & 0b0111_1111);
                return new Flags {
                    isStart = isStart,
                    streamId = streamId,
                };
            }

            public byte Encode() {
                byte b = (byte)(isStart ? 0b1000_0000 : 0);
                b |= (byte)(streamId & 0b0111_1111);
                return b;
            }
        }

    }
}
