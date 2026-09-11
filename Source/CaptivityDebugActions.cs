using LudeonTK;
using RimWorld;
using Verse;

namespace PerspectiveShiftCaptivity
{
    public static class CaptivityDebugActions
    {
        private const string Cat = "Perspective Shift - Captivity";

        [DebugAction(Cat, "Force discipline (beat)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceDiscipline()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive == null)
            {
                Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!CaptivityDirector.TryStartAction(comp, CaptorAction.Discipline))
                Messages.Message("No available captor could reach the captive.", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Cat, "Force harass", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceHarass()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive == null) { Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false); return; }
            if (!CaptivityDirector.TryStartAction(comp, CaptorAction.Harass))
                Messages.Message("No available captor could reach the captive.", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Cat, "Force sell to new owner", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceSell()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive == null) { Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false); return; }
            if (!CaptivityDirector.TryStartAction(comp, CaptorAction.Sell))
                Messages.Message("No available captor could reach the captive.", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Cat, "Force organ harvest", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceHarvest()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive == null) { Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false); return; }
            if (!CaptivityDirector.TryStartAction(comp, CaptorAction.Harvest))
                Messages.Message("No available captor could reach the captive.", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Cat, "Brand captive (Slavery Overhaul)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BrandDbg()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive == null) { Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false); return; }
            CaptivityCompat.TryBrand(comp.captive);
            CaptivityCompat.UpdateTemperament(comp.captive, comp.obedience);
        }

        [DebugAction(Cat, "Force feed", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceFeed()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive == null) { Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false); return; }
            comp.lastFeedAt = Find.TickManager.TicksGame;
            if (!CaptivityDirector.TryFeed(comp))
                Messages.Message("No available captor to feed.", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Cat, "Pick nearest door", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PickNearestDoor()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive?.Map == null) { Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false); return; }
            Building_Door best = null;
            float bestSq = float.MaxValue;
            foreach (Thing t in comp.captive.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (!(t is Building_Door d) || comp.IsDoorPicked(d.thingIDNumber)) continue;
                float sq = d.Position.DistanceToSquared(comp.captive.Position);
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            if (best != null) CaptivityDirector.StartLockpick(comp, best);
            else Messages.Message("No unpicked door found.", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Cat, "Fit slave collar", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FitCollarDbg()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive == null) { Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false); return; }
            CaptivityCompat.TryFitCollar(comp.captive);
        }

        [DebugAction(Cat, "Free the captive", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FreeCaptiveDbg()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp?.captive == null) { Messages.Message("No captive registered.", MessageTypeDefOf.RejectInput, false); return; }
            CaptivityDirector.FreeCaptive(comp, escaped: false);
        }

        [DebugAction(Cat, "Obedience -> 0 (defiant)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ObedienceZero()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null) return;
            comp.obedience = 0;
            CaptivityCompat.UpdateTemperament(comp.captive, comp.obedience);
        }

        [DebugAction(Cat, "Obedience -> 100 (broken)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ObedienceMax()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null) return;
            comp.obedience = 100;
            CaptivityCompat.UpdateTemperament(comp.captive, comp.obedience);
        }

        [DebugAction(Cat, "Heat -> 0 / reset resist streak", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CoolOff()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null) return;
            comp.heat = 0;
            comp.resistStreak = 0;
        }

        [DebugAction(Cat, "Dump captivity state", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpState()
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null) { Log.Message("[PSC] no game component."); return; }
            Log.Message($"[PSC] run={comp.captivityRun} status={comp.status} " +
                        $"captor={comp.captorFaction?.Name ?? "null"} captive={comp.captive?.LabelShort ?? "null"} " +
                        $"obedience={comp.obedience} heat={comp.heat} directorBroken={comp.directorBroken}");
        }
    }
}
