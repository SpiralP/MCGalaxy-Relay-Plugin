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
                Debug(
                    "sending {0} bytes",
                    this.data.Length
                );

                foreach (var target in targets) {
                    if (!target.player.Supports(CpeExt.PluginMessages, 1)) {
                        continue;
                    }

                    byte[] data = BuildOutgoingPacket(target);
                    if (data == null) continue;

                    try {
                        target.player.Send(Packet.PluginMessage(channel, data));
                    } catch (Exception e) {
                        Warn(
                            "Exception when sending PluginMessage from {0} to {1}: {2}",
                            sender.truename,
                            target.player.truename,
                            e
                        );
                        return;
                    }
                }
            }

            public void CheckCleanup(StreamTarget[] targets) {
                Store.With(channel, (store) => {
                    foreach (var target in targets) {
                        target.Sent((UInt16)this.data.Length);

                        if (target.IsFinished()) {
                            Debug(
                                "stream finished for {0} ({1})",
                                sender.truename,
                                this.flags.streamId
                            );
                            store.CleanupFromSender(sender, this.flags.streamId);
                        }
                    }
                });
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
                return store.CreateTargets(scope, dataLength);
            }

            public override byte[] BuildOutgoingPacket(StreamTarget target) {
                byte[] data = new byte[Packet.PluginMessageDataLength];

                int i = 0;
                data[i++] = new Flags {
                    isStart = true,
                    streamId = target.streamId,
                }.Encode();

                // scope, scope extra
                data[i++] = (byte)ScopeKind.PlayerEntity;
                byte senderID;
                if (!target.player.EntityList.GetID(sender, out senderID)) return null; //TODO: Figure out how this should work with server scope
                data[i++] = senderID;

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
