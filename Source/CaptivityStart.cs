using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using PerspectiveShift;

namespace PerspectiveShiftCaptivity
{
    // One-shot setup that turns a normal single-pawn start into a captivity start:
    // pick a hostile settlement, generate its map, relocate the avatar in as a
    // prisoner of the captor faction (native hostility suppression, keeps player
    // faction so Perspective Shift control still works), and shackle them.
    public static class CaptivityStart
    {
        public static void Begin()
        {
            try
            {
                BeginInner();
            }
            catch (Exception e)
            {
                CaptivityLog.Error("captivity start failed, falling back to normal play: " + e);
                var comp = GameComponent_Captivity.Comp;
                if (comp != null)
                {
                    comp.captivityRun = false;
                    comp.status = CaptivityStatus.Inactive;
                }
            }
        }

        private static void BeginInner()
        {
            var comp = GameComponent_Captivity.Comp;
            if (comp == null || comp.status != CaptivityStatus.Inactive) return;

            // Resolve the captive: the Perspective Shift avatar, else the lone colonist.
            Pawn captive = State.Avatar?.pawn;
            if (captive == null || captive.Dead)
                captive = PawnsFinder.AllMaps_FreeColonists.FirstOrDefault();
            if (captive == null)
            {
                CaptivityLog.Warn("no captive pawn found; captivity aborted.");
                comp.captivityRun = false;
                return;
            }

            Map homeMap = captive.MapHeld;

            // Force single-pawn mode.
            State.CurrentMode = PlaystyleMode.Authentic;
            State.permadeath = comp.permadeath;

            // Pick a captor settlement (prefer hostile, nearest to the captive).
            Settlement settlement = PickCaptorSettlement(captive);
            if (settlement == null)
            {
                CaptivityLog.Warn("no suitable captor settlement on the world; captivity aborted.");
                comp.captivityRun = false;
                return;
            }
            Faction captor = settlement.Faction;

            // Relocate the captive into the settlement as a prisoner (shared with the
            // "sold" handoff): generates the map, places + reveals, sets prisoner status,
            // shackles, and re-asserts Perspective Shift control.
            Map map = CaptivityUtility.PlaceCaptiveInSettlement(comp, captive, settlement);
            if (map == null)
            {
                CaptivityLog.Warn("failed to place captive in settlement; captivity aborted.");
                comp.captivityRun = false;
                return;
            }

            comp.captive = captive;
            comp.status = CaptivityStatus.Held;
            comp.heat = 80;
            comp.obedience = 40;
            comp.lastDisciplineAt = comp.lastHarassAt = comp.lastSellAt = comp.lastHarvestAt = comp.lastHeatDecayAt = comp.lastFeedAt = Find.TickManager.TicksGame;

            Find.LetterStack.ReceiveLetter(
                "PSC_Letter_CaptiveTitle".Translate(),
                "PSC_Letter_CaptiveBody".Translate(captive.LabelShortCap, captor.Name),
                LetterDefOf.NegativeEvent,
                new LookTargets(captive));

            CaptivityLog.Msg($"captivity started: {captive.LabelShort} held by {captor.Name} at {settlement.Label}.");
        }

        private static Settlement PickCaptorSettlement(Pawn captive)
        {
            var all = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null
                            && !s.Faction.IsPlayer
                            && s.Faction.def.humanlikeFaction
                            && !s.HasMap)
                .ToList();
            if (all.Count == 0) return null;

            var hostile = all.Where(s => s.Faction.HostileTo(Faction.OfPlayer)).ToList();
            var pool = hostile.Count > 0 ? hostile : all;

            PlanetTile from = captive.Tile;
            return pool
                .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(from, s.Tile))
                .FirstOrDefault();
        }

    }
}
