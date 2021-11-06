namespace MCGalaxy {
    public sealed partial class MCGalaxyRelayPlugin {
        private static readonly bool debug = true;
        private static void Debug(string format, object arg0, object arg1, object arg2) {
            if (!debug) return;
            Logger.Log(LogType.Debug, format, arg0, arg1, arg2);
        }
        private static void Debug(string format, object arg0, object arg1) {
            if (!debug) return;
            Logger.Log(LogType.Debug, format, arg0, arg1);
        }
        private static void Debug(string format, object arg0) {
            if (!debug) return;
            Logger.Log(LogType.Debug, format, arg0);
        }
        private static void Debug(string format) {
            if (!debug) return;
            Logger.Log(LogType.Debug, format);
        }

        private static void Warn(string format, object arg0, object arg1, object arg2) {
            Logger.Log(LogType.Warning, format, arg0, arg1, arg2);
        }
        private static void Warn(string format, object arg0, object arg1) {
            Logger.Log(LogType.Warning, format, arg0, arg1);
        }
        private static void Warn(string format, object arg0) {
            Logger.Log(LogType.Warning, format, arg0);
        }
        private static void Warn(string format) {
            Logger.Log(LogType.Warning, format);
        }
    }
}
