using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Events.ServerEvents;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin : Plugin {
        public override string name => "Relay";
        public override string creator => "SpiralP";

        public override string MCGalaxy_Version => "1.9.3.6";

        public override void Load(bool isStartup) {
            OnPlayerDisconnectEvent.Register(OnPlayerDisconnect, Priority.Low);
            OnPluginMessageReceivedEvent.Register(OnPluginMessageReceived, Priority.Low);
        }

        public override void Unload(bool isShutdown) {
            OnPluginMessageReceivedEvent.Unregister(OnPluginMessageReceived);
            OnPlayerDisconnectEvent.Unregister(OnPlayerDisconnect);
        }
    }
}
