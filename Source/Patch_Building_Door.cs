using HarmonyLib;
using RimWorld;
using Verse;

namespace PerspectiveShiftCaptivity
{
    // Let the captive open (and thus path through) doors they have picked. PawnCanOpen gates
    // pathing (Verse.AI.GenPath) as well as opening, so this one postfix covers both. Hot
    // method: early-out before touching the component on the common cases.
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    public static class Patch_Building_Door_PawnCanOpen
    {
        public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
        {
            if (__result || p == null) return;
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null || !comp.Active || p != comp.captive) return;
            if (comp.IsDoorPicked(__instance.thingIDNumber)) __result = true;
        }
    }
}
