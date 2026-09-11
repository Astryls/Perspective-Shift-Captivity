using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PerspectiveShiftCaptivity
{
    public static class CaptivityUtility
    {
        public static int Clamp0100(int v) => v < 0 ? 0 : (v > 100 ? 100 : v);

        // The pawn's unarmed (fists/body) melee verb, so discipline stays low-lethality
        // instead of using an equipped weapon.
        public static Verb UnarmedMeleeVerb(Pawn p)
        {
            if (p?.meleeVerbs == null) return null;
            List<VerbEntry> verbs = p.meleeVerbs.GetUpdatedAvailableVerbsList(false);
            for (int i = 0; i < verbs.Count; i++)
            {
                Verb v = verbs[i].verb;
                if (v != null && v.EquipmentSource == null && v.HediffCompSource == null)
                    return v;
            }
            for (int i = 0; i < verbs.Count; i++)
            {
                Verb v = verbs[i].verb;
                if (v != null && v.EquipmentSource == null)
                    return v;
            }
            return null;
        }

        // Nearest eligible captor pawn on the captive's map.
        public static Pawn FindAvailableCaptor(GameComponent_Captivity comp, bool needsViolence)
        {
            Pawn captive = comp?.captive;
            Faction captor = comp?.captorFaction;
            if (captive?.Map == null || captor == null) return null;
            Map map = captive.Map;

            Pawn best = null;
            float bestDistSq = float.MaxValue;
            foreach (Pawn p in map.mapPawns.SpawnedPawnsInFaction(captor))
            {
                if (p == captive || p.Dead || p.Downed) continue;
                if (p.RaceProps == null || !p.RaceProps.Humanlike) continue;
                if (p.InMentalState) continue;
                if (needsViolence && p.WorkTagIsDisabled(WorkTags.Violent)) continue;
                Job cur = p.CurJob;
                if (cur?.def != null && !cur.def.playerInterruptible) continue;
                if (!p.CanReach(captive, PathEndMode.Touch, Danger.Deadly)) continue;

                float d = p.Position.DistanceToSquared(captive.Position);
                if (d < bestDistSq) { bestDistSq = d; best = p; }
            }
            return best;
        }

        // A standable, reachable cell a few tiles from the captor to drag the captive to.
        public static IntVec3 FindProcessingSpot(Pawn captor)
        {
            if (captor?.Map == null) return captor?.Position ?? IntVec3.Invalid;
            Map map = captor.Map;
            IntVec3 c;
            if (CellFinder.TryFindRandomCellNear(captor.Position, map, 10,
                    x => x.Standable(map) && x.Walkable(map)
                         && (x - captor.Position).LengthHorizontalSquared >= 9
                         && captor.CanReach(x, PathEndMode.OnCell, Danger.Deadly),
                    out c, 200))
                return c;
            return captor.Position;
        }

        public static int CountNearbyCaptors(GameComponent_Captivity comp, float radius)
        {
            Pawn captive = comp?.captive;
            if (captive?.Map == null || comp.captorFaction == null) return 0;
            float r2 = radius * radius;
            int n = 0;
            foreach (Pawn p in captive.Map.mapPawns.SpawnedPawnsInFaction(comp.captorFaction))
                if (!p.Dead && !p.Downed && p.Position.DistanceToSquared(captive.Position) <= r2)
                    n++;
            return n;
        }

        // A closed captor door adjacent to the captive that they cannot open and have not
        // already picked. Null when there is nothing to pick.
        public static Building_Door FindPickableDoor(GameComponent_Captivity comp, Pawn captive)
        {
            if (comp == null || captive?.Map == null) return null;
            Map map = captive.Map;
            for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                IntVec3 c = captive.Position + GenAdj.AdjacentCells[i];
                if (!c.InBounds(map)) continue;
                Building_Door door = c.GetDoor(map);
                if (door == null) continue;
                if (comp.IsDoorPicked(door.thingIDNumber)) continue;
                if (door.PawnCanOpen(captive)) continue;
                return door;
            }
            return null;
        }

        // Edible food on or next to the captive. They must physically walk to it, so the
        // theft is a real act on the map rather than a menu click.
        public static Thing FindStealableFood(Pawn captive)
        {
            if (captive?.Map == null) return null;
            Map map = captive.Map;
            Thing t = FoodAt(captive.Position, map);
            if (t != null) return t;
            for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                IntVec3 c = captive.Position + GenAdj.AdjacentCells[i];
                if (!c.InBounds(map)) continue;
                t = FoodAt(c, map);
                if (t != null) return t;
            }
            return null;
        }

        private static Thing FoodAt(IntVec3 c, Map map)
        {
            List<Thing> list = c.GetThingList(map);
            for (int i = 0; i < list.Count; i++)
            {
                Thing t = list[i];
                if (t is Pawn) continue;
                if (t.def?.ingestible == null) continue;
                if (!t.def.IsNutritionGivingIngestible) continue;
                if (t.def.ingestible.preferability <= FoodPreferability.NeverForNutrition) continue;
                return t;
            }
            return null;
        }

        public static void ThrowCaptivityMote(Pawn captive, string text, Color color)
        {
            if (captive?.Map == null) return;
            MoteMaker.ThrowText(captive.DrawPos, captive.Map, text, color, 3.6f);
        }

        public static IntVec3 FindHoldingCell(Map map)
        {
            IntVec3 c;
            if (CellFinder.TryFindRandomCellNear(map.Center, map, 22,
                    x => x.Standable(map) && x.Walkable(map), out c, 300))
                return c;
            if (CellFinderLoose.TryGetRandomCellWith(x => x.Standable(map), map, 1000, out c))
                return c;
            return map.Center;
        }

        public static void EnsureShackled(Pawn p)
        {
            if (p?.health == null) return;
            if (!p.health.hediffSet.HasHediff(PSCDefOf.PSC_Shackled))
                p.health.AddHediff(PSCDefOf.PSC_Shackled);
        }

        // Generate the settlement map, place the captive in a holding cell, reveal the
        // area, set prisoner status (native hostility suppression), shackle, and re-assert
        // Perspective Shift control. Shared by the initial start and the "sold" handoff.
        public static Map PlaceCaptiveInSettlement(GameComponent_Captivity comp, Pawn captive, Settlement settlement)
        {
            if (comp == null || captive == null || settlement == null) return null;

            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, null);
            if (map == null) return null;

            IntVec3 cell = FindHoldingCell(map);

            if (captive.Spawned) captive.DeSpawn(DestroyMode.Vanish);
            GenSpawn.Spawn(captive, cell, map);

            foreach (IntVec3 c in GenRadial.RadialCellsAround(cell, 9f, true))
                if (c.InBounds(map)) map.fogGrid.Unfog(c);

            if (captive.guest == null)
                PawnComponentsUtility.AddAndRemoveDynamicComponents(captive);
            captive.guest?.SetGuestStatus(settlement.Faction, GuestStatus.Prisoner);

            EnsureShackled(captive);
            CaptivityCompat.TryFitCollar(captive);

            comp.captorFaction = settlement.Faction;

            PerspectiveShift.State.SetAvatar(captive);
            Current.Game.CurrentMap = map;
            Find.CameraDriver?.JumpToCurrentMapLoc(cell);

            return map;
        }
    }
}
