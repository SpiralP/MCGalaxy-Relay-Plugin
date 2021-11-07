using System;
using System.Collections.Generic;
using MCGalaxy.Network;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        public abstract class IncomingPacket {
            public Player sender;
            public byte channel;
            public Flags flags;
            public Flags outgoingFlags;
            public Scope scope;
            public byte[] data;

            public static IncomingPacket TryCreate(Player sender, byte channel, byte[] data) {
                int i = 0;
                Flags flags = Flags.Decode(data[i++]);

                byte[] nextData = new byte[data.Length - i];
                Array.Copy(data, i, nextData, 0, nextData.Length);

                if (flags.isStart) {
                    return IncomingStartPacket.TryCreate(sender, channel, flags, nextData);
                } else {
                    return IncomingContinuationPacket.TryCreate(sender, channel, flags, nextData);
                }
            }

            public void ReserveTargetIds() {
                // begin reserving ids
                scope.ReserveTargetIds();
            }

            public abstract byte[] BuildOutgoingPacket(PacketTarget target);

            public void Relay() {
                foreach (var target in scope.targets) {
                    byte[] data = BuildOutgoingPacket(target);
                    target.player.Send(Packet.PluginMessage(channel, data));
                }
            }
        }


        public class IncomingStartPacket : IncomingPacket {
            private UInt16 packetSize;

            public static IncomingStartPacket TryCreate(Player sender, byte channel, Flags flags, byte[] data) {
                int i = 0;

                byte scopeKind = data[i++];
                byte scopeExtra = data[i++];
                Scope scope = Scope.TryCreate(sender, channel, flags.packetId, scopeKind, scopeExtra);

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

            public override byte[] BuildOutgoingPacket(PacketTarget target) {
                byte[] data = new byte[Packet.PluginMessageDataLength];

                int i = 0;
                data[i++] = new Flags {
                    isStart = true,
                    packetId = target.packetId,
                }.Encode();
                data[i++] = sender.id;
                NetUtils.WriteU16(packetSize, data, i);
                i += 2;

                if (this.data.Length > data.Length - i) {
                    throw new Exception("!");
                }
                Array.Copy(this.data, 0, data, i, data.Length - i);

                return data;
            }
        }


        public class IncomingContinuationPacket : IncomingPacket {
            public static IncomingContinuationPacket TryCreate(Player sender, byte channel, Flags flags, byte[] data) {
                Scope scope = Scope.GetForSender(sender, channel);

                if (scope == null) {
                    throw new Exception(
                        string.Format(
                            "couldn't find scope for {0} {1}",
                            channel, sender.truename
                        )
                    );
                }

                return new IncomingContinuationPacket {
                    sender = sender,
                    channel = channel,
                    flags = flags,
                    scope = scope,
                    data = data,
                };
            }

            public override byte[] BuildOutgoingPacket(PacketTarget target) {
                byte[] data = new byte[Packet.PluginMessageDataLength];

                int i = 0;
                data[i++] = new Flags {
                    isStart = false,
                    packetId = target.packetId,
                }.Encode();

                if (this.data.Length > data.Length - i) {
                    throw new Exception("!");
                }
                Array.Copy(this.data, 0, data, i, data.Length - i);

                return data;
            }
        }

    }
}
