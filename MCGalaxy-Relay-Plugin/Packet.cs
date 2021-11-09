using System;
using System.Collections.Generic;
using MCGalaxy.Network;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {

        public abstract class IncomingPacket {
            public Player sender;
            public byte channel;
            public Flags flags;
            public byte[] data;

            protected IncomingPacket(
                Player sender,
                byte channel,
                Flags flags,
                byte[] data
            ) {
                this.sender = sender;
                this.channel = channel;
                this.flags = flags;
                this.data = data;
            }

            public static IncomingPacket TryCreate(Player sender, byte channel, byte[] data) {
                int i = 0;
                Flags flags = Flags.Decode(data[i++]);

                byte[] nextData = new byte[data.Length - i];
                Array.Copy(data, i, nextData, 0, nextData.Length);

                if (flags.isStart) {
                    return new IncomingStartPacket(sender, channel, flags, nextData);
                } else {
                    return new IncomingContinuationPacket(sender, channel, flags, nextData);
                }
            }

            public abstract StreamTarget[] GetTargets(Store store);

            public abstract byte[] BuildOutgoingPacket(StreamTarget target);

            public void Relay(StreamTarget[] targets) {
                foreach (var target in targets) {
                    byte[] data = BuildOutgoingPacket(target);
                    target.player.Send(Packet.PluginMessage(channel, data));
                }
            }
        }


        public class IncomingStartPacket : IncomingPacket {
            public Scope scope;
            private UInt16 dataLength;

            public IncomingStartPacket(
                Player sender,
                byte channel,
                Flags flags,
                byte[] data,
                UInt16 dataLength
            ) : base(
                sender,
                channel,
                flags,
                data
            ) {
                this.dataLength = dataLength;
            }

            public IncomingStartPacket(
                Player sender,
                byte channel,
                Flags flags,
                byte[] data
            ) : base(
                sender,
                channel,
                flags,
                null
            ) {
                int i = 0;

                byte scopeKind = data[i++];
                byte scopeExtra = data[i++];
                Scope scope = Scope.TryCreate(sender, channel, flags.streamId, scopeKind, scopeExtra);

                UInt16 dataLength = NetUtils.ReadU16(data, i);
                i += 2;

                byte[] innerData = new byte[data.Length - i];
                Array.Copy(data, i, innerData, 0, innerData.Length);

                this.data = innerData;
                this.scope = scope;
                this.dataLength = dataLength;
            }

            public override StreamTarget[] GetTargets(Store store) {
                // begin reserving ids
                return store.StoreTargets(scope);
            }

            public override byte[] BuildOutgoingPacket(StreamTarget target) {
                byte[] data = new byte[Packet.PluginMessageDataLength];

                int i = 0;
                data[i++] = new Flags {
                    isStart = true,
                    streamId = target.streamId,
                }.Encode();

                // scope, scope extra
                data[i++] = (byte)ScopeKind.Player;
                data[i++] = sender.id;

                NetUtils.WriteU16(dataLength, data, i);
                i += 2;

                Array.Copy(this.data, 0, data, i, data.Length - i);

                return data;
            }
        }


        public class IncomingContinuationPacket : IncomingPacket {
            public IncomingContinuationPacket(
                Player sender,
                byte channel,
                Flags flags,
                byte[] data
            ) : base(
                sender,
                channel,
                flags,
                data
            ) { }

            public override StreamTarget[] GetTargets(Store store) {
                return store.GetTargets(sender, flags.streamId);
            }

            public override byte[] BuildOutgoingPacket(StreamTarget target) {
                byte[] data = new byte[Packet.PluginMessageDataLength];

                int i = 0;
                data[i++] = new Flags {
                    isStart = false,
                    streamId = target.streamId,
                }.Encode();

                Array.Copy(this.data, 0, data, i, data.Length - i);

                return data;
            }
        }

    }
}