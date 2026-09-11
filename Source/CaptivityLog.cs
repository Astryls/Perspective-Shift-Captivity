using Verse;

namespace PerspectiveShiftCaptivity
{
    public static class CaptivityLog
    {
        public const string Prefix = "[PSC] ";

        public static void Msg(string m) => Log.Message(Prefix + m);
        public static void Warn(string m) => Log.Warning(Prefix + m);
        public static void Error(string m) => Log.Error(Prefix + m);
        public static void ErrorOnce(string m, int key) => Log.ErrorOnce(Prefix + m, key);
    }
}
