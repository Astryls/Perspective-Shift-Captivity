using UnityEngine;
using Verse;

namespace PerspectiveShiftCaptivity
{
    public class CaptivityMod : Mod
    {
        public static CaptivitySettings Settings;

        public CaptivityMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CaptivitySettings>();
        }

        public override string SettingsCategory() => "Perspective Shift - Captivity";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindow(inRect);
        }
    }
}
