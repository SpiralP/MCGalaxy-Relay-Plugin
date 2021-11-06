using System;
using MCGalaxy.Network;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {
        public interface IIncomingPacket {
            void Relay();
        }


        public class IncomingStartPacket : IIncomingPacket {
            private Player sender;
            private byte channel;
            private Flags flags;
            private IScope scope;
            private UInt16 packetSize;
            private byte[] data;

            public static IncomingStartPacket TryCreate(Player sender, byte channel, Flags flags, byte[] data) {
                int i = 0;

                byte nScopeKind = data[i++];
                byte nScopeExtra = data[i++];
                IScope scope = TryGetScope(sender, channel, nScopeKind, nScopeExtra);

                UInt16 packetSize = NetUtils.ReadU16(data, i);
                i += 2;

                byte[] innerData = new byte[data.Length - i];
                Array.Copy(data, i, innerData, 0, innerData.Length);

                return new IncomingStartPacket {
                    sender = sender,
                    channel = channel,
                    flags = flags,
                    scope = scope,
                    packetSize = packetSize,
                    data = innerData,
                };
            }

            private byte[] BuildOutgoingPacket() {
                byte[] data = new byte[Packet.PluginMessageDataLength];

                int i = 0;
                // TODO convert packetId
                data[i++] = flags.Encode();
                data[i++] = sender.id;
                NetUtils.WriteU16(packetSize, data, i);
                i += 2;

                if (this.data.Length > data.Length - i) {
                    throw new Exception("!");
                }
                Array.Copy(this.data, 0, data, i, data.Length - i);

                return data;
            }

            public void Relay() {
                byte[] data = BuildOutgoingPacket();

                foreach (var target in scope.GetTargets()) {
                    target.Send(Packet.PluginMessage(channel, data));
                }
            }
        }


        public class IncomingContinuationPacket : IIncomingPacket {
            private Player sender;
            private byte channel;
            private Flags flags;
            private IScope scope;
            private byte[] data;

            public static IncomingContinuationPacket TryCreate(Player sender, byte channel, Flags flags, byte[] data) {
                IScope scope = null;
                // TODO get scope from packetId
                return new IncomingContinuationPacket {
                    sender = sender,
                    channel = channel,
                    flags = flags,
                    scope = scope,
                    data = data,
                };
            }


            private byte[] BuildOutgoingPacket() {
                byte[] data = new byte[Packet.PluginMessageDataLength];

                int i = 0;
                // TODO convert packetId
                data[i++] = flags.Encode();

                if (this.data.Length > data.Length - i) {
                    throw new Exception("!");
                }
                Array.Copy(this.data, 0, data, i, data.Length - i);

                return data;
            }

            public void Relay() {
                byte[] data = BuildOutgoingPacket();

                foreach (var target in scope.GetTargets()) {
                    target.Send(Packet.PluginMessage(channel, data));
                }
            }
        }

    }
}
