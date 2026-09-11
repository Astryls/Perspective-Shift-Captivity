using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShiftCaptivity
{
    // Detects the captive fighting back: if the captive damages a captor, obedience
    // drops hard and the captors clamp down (heat spikes). Vanilla self-defense then
    // handles the immediate retaliation.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_Pawn_PostApplyDamage
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            try
            {
                if (totalDamageDealt <= 0f) return;
                GameComponent_Captivity comp = GameComponent_Captivity.Comp;
                if (comp == null || !comp.Active || comp.captive == null) return;
                if (dinfo.Instigator != comp.captive) return;
                Pawn victim = __instance;
                if (victim == null || victim == comp.captive) return;
                if (victim.Faction == null || victim.Faction != comp.captorFaction) return;

                CaptivityDirector.OnCaptiveResisted(comp);
            }
            catch (System.Exception e)
            {
                CaptivityLog.ErrorOnce("PostApplyDamage patch failed: " + e, 74310099);
            }
        }
    }
}
