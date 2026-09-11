using RimWorld;
using Verse;

namespace PerspectiveShiftCaptivity
{
    // Marker part on the Captive scenario. Flags the run and kicks off the
    // captivity setup once the game has started (deferred to a long event so the
    // settlement map generates cleanly on the main thread).
    public class ScenPart_Captivity : ScenPart
    {
        public override void PostGameStart()
        {
            base.PostGameStart();
            var comp = GameComponent_Captivity.Comp;
            if (comp == null) return;

            comp.captivityRun = true;
            comp.permadeath = CaptivityMod.Settings?.permadeathByDefault ?? false;

            LongEventHandler.QueueLongEvent(CaptivityStart.Begin, "PSC_SettingUpCaptivity", false, null);
        }
    }
}
