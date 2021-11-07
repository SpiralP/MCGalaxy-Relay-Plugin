using MCGalaxy.Events.ServerEvents;

namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin : Plugin {
        public override string name => "Relay";
        public override string creator => "SpiralP";

        // TODO bump version for PluginMessages CPE
        public override string MCGalaxy_Version => "1.9.3.5";

        public override void Load(bool isStartup) {
            OnPluginMessageReceivedEvent.Register(OnPluginMessageReceived, Priority.Low);
        }

        public override void Unload(bool isShutdown) {
            OnPluginMessageReceivedEvent.Unregister(OnPluginMessageReceived);
        }
    }
}
