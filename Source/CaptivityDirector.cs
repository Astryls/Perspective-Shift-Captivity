using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PerspectiveShiftCaptivity
{
    public enum CaptorAction { Discipline, Harass, Sell, Harvest }

    // Drives captor behavior on an elapsed-time cadence (robust against time-skips
    // and save/load, per the modulo pitfall). Only acts while the captive is Held.
    public static class CaptivityDirector
    {
        public static void Tick(GameComponent_Captivity comp)
        {
            Pawn captive = comp.captive;
            if (captive == null || !captive.Spawned || captive.Dead) return;
            if (comp.status != CaptivityStatus.Held) return;

            int now = Find.TickManager.TicksGame;
            CaptivitySettings s = CaptivityMod.Settings;

            // Survival: captors keep their property fed enough to stay alive and sellable.
            if (s.captorsFeedYou)
            {
                Need_Food food = captive.needs?.food;
                if (food != null && food.CurLevelPercentage < 0.30f
                    && (comp.lastFeedAt < 0 || now - comp.lastFeedAt >= GenDate.TicksPerHour))
                {
                    comp.lastFeedAt = now;
                    if (!TryFeed(comp) && food.CurLevelPercentage < 0.08f)
                    {
                        food.CurLevel = Mathf.Min(food.MaxLevel, food.CurLevel + food.MaxLevel * 0.2f);
                        Messages.Message("PSC_Msg_Scavenged".Translate(), captive, MessageTypeDefOf.NeutralEvent, false);
                    }
                }
            }

            // Discipline: more likely the more defiant the captive is.
            int discInterval = Mathf.Max(2500, (int)(s.disciplineIntervalDays * GenDate.TicksPerDay));
            if (comp.lastDisciplineAt < 0 || now - comp.lastDisciplineAt >= discInterval)
            {
                comp.lastDisciplineAt = now;
                float defiance = (100 - comp.obedience) / 100f;
                if (Rand.Value < Mathf.Lerp(0.12f, 0.70f, defiance))
                    TryStartAction(comp, CaptorAction.Discipline);
            }

            // Harassment: frequent, low-stakes cruelty.
            int harInterval = Mathf.Max(1500, (int)(s.harassIntervalDays * GenDate.TicksPerDay));
            if (comp.lastHarassAt < 0 || now - comp.lastHarassAt >= harInterval)
            {
                comp.lastHarassAt = now;
                if (Rand.Value < 0.55f)
                    TryStartAction(comp, CaptorAction.Harass);
            }

            // Sold: occasional handoff to a new owner.
            if (s.allowSelling)
            {
                int sellInterval = Mathf.Max(GenDate.TicksPerDay, (int)(s.sellIntervalDays * GenDate.TicksPerDay));
                if (comp.lastSellAt < 0)
                    comp.lastSellAt = now;
                else if (now - comp.lastSellAt >= sellInterval)
                {
                    comp.lastSellAt = now;
                    if (Rand.Value < s.sellChancePerCheck)
                        TryStartAction(comp, CaptorAction.Sell);
                }
            }

            // Organ harvest: rare, dark, setting-gated. Only ever a spare (paired) organ,
            // so it is never lethal. More likely against a defiant captive.
            if (s.allowOrganHarvest)
            {
                int hvInterval = Mathf.Max(GenDate.TicksPerDay * 2, (int)(s.organHarvestIntervalDays * GenDate.TicksPerDay));
                if (comp.lastHarvestAt < 0)
                    comp.lastHarvestAt = now;
                else if (now - comp.lastHarvestAt >= hvInterval)
                {
                    comp.lastHarvestAt = now;
                    float chance = s.organHarvestChance * Mathf.Lerp(0.5f, 1.5f, (100 - comp.obedience) / 100f);
                    if (Rand.Value < chance)
                        TryStartAction(comp, CaptorAction.Harvest);
                }
            }

            // Captors relax over time when the captive is not causing trouble, slowly opening
            // an escape window. Resisting and seizures push heat back up.
            if (comp.lastHeatDecayAt < 0)
                comp.lastHeatDecayAt = now;
            else if (now - comp.lastHeatDecayAt >= GenDate.TicksPerHour)
            {
                comp.lastHeatDecayAt = now;
                if (comp.heat > 0)
                    comp.heat = CaptivityUtility.Clamp0100(comp.heat - 2);
            }
        }

        public static bool TryDiscipline(GameComponent_Captivity comp)
        {
            Pawn captive = comp.captive;
            if (captive == null || !captive.Spawned || captive.Downed || captive.Dead) return false;

            Pawn guard = CaptivityUtility.FindAvailableCaptor(comp, needsViolence: true);
            if (guard == null) return false;

            Job job = JobMaker.MakeJob(PSCDefOf.PSC_DisciplineCaptive, captive);
            guard.jobs.StartJob(job, JobCondition.InterruptForced, null,
                resumeCurJobAfterwards: true, cancelBusyStances: true);
            return true;
        }

        // Applied once per completed beating (called from the JobDriver finish action
        // only if at least one strike landed).
        public static void OnDisciplined(Pawn victim)
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null || victim == null || victim != comp.captive) return;

            victim.needs?.mood?.thoughts?.memories?.TryGainMemory(PSCDefOf.PSC_Beaten);

            ShiftObedience(comp, 6);
            comp.heat = CaptivityUtility.Clamp0100(comp.heat + 4);

            CaptivityUtility.ThrowCaptivityMote(victim, "PSC_Mote_Disciplined".Translate(), new Color(0.9f, 0.4f, 0.4f));
            Messages.Message("PSC_Msg_Disciplined".Translate(victim.LabelShortCap), victim, MessageTypeDefOf.NegativeEvent, historical: false);
        }

        public static bool TryHarass(GameComponent_Captivity comp)
        {
            Pawn captive = comp.captive;
            if (captive == null || !captive.Spawned || captive.Downed || captive.Dead) return false;

            Pawn harasser = CaptivityUtility.FindAvailableCaptor(comp, needsViolence: false);
            if (harasser == null) return false;

            Job job = JobMaker.MakeJob(PSCDefOf.PSC_HarassCaptive, captive);
            harasser.jobs.StartJob(job, JobCondition.InterruptForced, null,
                resumeCurJobAfterwards: true, cancelBusyStances: true);
            return true;
        }

        public static void ApplyHarassEffects(Pawn victim)
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null || victim == null || victim != comp.captive) return;

            victim.needs?.mood?.thoughts?.memories?.TryGainMemory(PSCDefOf.PSC_Humiliated);
            ShiftObedience(comp, -3);
            comp.heat = CaptivityUtility.Clamp0100(comp.heat + 2);
        }

        public static bool SellToNewOwner(GameComponent_Captivity comp)
        {
            Pawn captive = comp?.captive;
            if (captive == null || !captive.Spawned || captive.Downed || captive.Dead) return false;

            Settlement dest = PickNewOwnerSettlement(comp);
            if (dest == null) return false;

            Faction oldOwner = comp.captorFaction;
            comp.status = CaptivityStatus.BeingSold;

            Map map = CaptivityUtility.PlaceCaptiveInSettlement(comp, captive, dest);
            if (map == null) { comp.status = CaptivityStatus.Held; return false; }

            comp.status = CaptivityStatus.Held;
            comp.heat = 90;
            comp.obedience = CaptivityUtility.Clamp0100(comp.obedience - 10);
            int now = Find.TickManager.TicksGame;
            comp.lastDisciplineAt = comp.lastHarassAt = comp.lastSellAt = comp.lastHarvestAt = comp.lastHeatDecayAt = comp.lastFeedAt = now;

            captive.needs?.mood?.thoughts?.memories?.TryGainMemory(PSCDefOf.PSC_Sold);
            CaptivityCompat.TryBrand(captive);
            CaptivityCompat.UpdateTemperament(captive, comp.obedience);

            bool auction = CaptivityCompat.SlaveryOverhaul;
            Find.LetterStack.ReceiveLetter(
                (auction ? "PSC_Letter_AuctionedTitle" : "PSC_Letter_SoldTitle").Translate(),
                (auction ? "PSC_Letter_AuctionedBody" : "PSC_Letter_SoldBody")
                    .Translate(captive.LabelShortCap, oldOwner?.Name ?? "someone", dest.Faction.Name),
                LetterDefOf.NegativeEvent, new LookTargets(captive));
            CaptivityLog.Msg($"captive sold to {dest.Faction?.Name} at {dest.Label}.");
            return true;
        }

        private static Settlement PickNewOwnerSettlement(GameComponent_Captivity comp)
        {
            var all = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && !s.Faction.IsPlayer
                            && s.Faction.def.humanlikeFaction && s.Faction != comp.captorFaction)
                .ToList();
            if (all.Count == 0) return null;

            var hostile = all.Where(s => s.Faction.HostileTo(Faction.OfPlayer)).ToList();
            var pool = hostile.Count > 0 ? hostile : all;
            return pool.RandomElement();
        }

        public static void OnCaptiveResisted(GameComponent_Captivity comp)
        {
            if (comp?.captive == null) return;
            comp.obedience = CaptivityUtility.Clamp0100(comp.obedience - 12);
            comp.heat = 100;
            CaptivityCompat.UpdateTemperament(comp.captive, comp.obedience);

            int now = Find.TickManager.TicksGame;
            if (now - lastResistMsgTick > 250)
            {
                lastResistMsgTick = now;
                Messages.Message("PSC_Msg_Resisted".Translate(), comp.captive, MessageTypeDefOf.ThreatSmall, historical: false);
            }
        }
        private static int lastResistMsgTick = -9999;

        public static void ClearCaptivityMarks(Pawn captive)
        {
            if (captive == null) return;
            if (captive.guest != null && captive.guest.HostFaction != null)
                captive.guest.SetGuestStatus(null);
            Hediff sh = captive.health?.hediffSet?.GetFirstHediffOfDef(PSCDefOf.PSC_Shackled);
            if (sh != null) captive.health.RemoveHediff(sh);
            CaptivityCompat.RemoveCollar(captive);
        }

        public static void FreeCaptive(GameComponent_Captivity comp, bool escaped)
        {
            Pawn captive = comp?.captive;
            if (captive == null) return;
            ClearCaptivityMarks(captive);
            comp.status = escaped ? CaptivityStatus.Escaped : CaptivityStatus.Free;
            captive.needs?.mood?.thoughts?.memories?.TryGainMemory(PSCDefOf.PSC_Freed);
            Find.LetterStack.ReceiveLetter(
                "PSC_Letter_FreeTitle".Translate(),
                "PSC_Letter_FreeBody".Translate(captive.LabelShortCap),
                LetterDefOf.PositiveEvent, new LookTargets(captive));
            CaptivityLog.Msg("captive freed (escaped=" + escaped + ").");
        }

        // Chance the captive slips away cleanly. Shown to the player before they commit.
        public static float EscapeChance(GameComponent_Captivity comp, Pawn captive)
        {
            float c = 0.85f;
            c -= (comp.heat / 100f) * 0.6f;
            if (captive.health?.hediffSet?.HasHediff(PSCDefOf.PSC_Shackled) ?? false) c -= 0.35f;
            if (CaptivityCompat.IsCollared(captive)) c -= 0.05f;
            c -= CaptivityUtility.CountNearbyCaptors(comp, 12f) * 0.12f;
            return Mathf.Clamp(c, 0.03f, 0.95f);
        }

        public static bool EscapeToWilderness(GameComponent_Captivity comp)
        {
            Pawn captive = comp?.captive;
            if (captive == null || !captive.Spawned) return false;

            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, 2, 25))
                return false;

            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, WorldObjectDefOf.Camp);
            if (map == null) return false;

            ClearCaptivityMarks(captive);

            IntVec3 cell = CaptivityUtility.FindHoldingCell(map);
            if (captive.Spawned) captive.DeSpawn(DestroyMode.Vanish);
            GenSpawn.Spawn(captive, cell, map);
            foreach (IntVec3 c in GenRadial.RadialCellsAround(cell, 9f, true))
                if (c.InBounds(map)) map.fogGrid.Unfog(c);

            PerspectiveShift.State.SetAvatar(captive);
            Current.Game.CurrentMap = map;
            Find.CameraDriver?.JumpToCurrentMapLoc(cell);

            comp.status = CaptivityStatus.Escaped;
            comp.resistStreak = 0;
            captive.needs?.mood?.thoughts?.memories?.TryGainMemory(PSCDefOf.PSC_Freed);

            Find.LetterStack.ReceiveLetter(
                "PSC_Letter_EscapedTitle".Translate(),
                "PSC_Letter_EscapedBody".Translate(captive.LabelShortCap),
                LetterDefOf.PositiveEvent, new LookTargets(captive));
            CaptivityLog.Msg("captive escaped to the wilderness.");
            return true;
        }

        public static void CaughtEscaping(GameComponent_Captivity comp)
        {
            if (comp?.captive == null) return;
            comp.heat = 100;
            comp.resistStreak++;
            CaptivityCompat.UpdateTemperament(comp.captive, comp.obedience);
            Messages.Message("PSC_Msg_CaughtEscaping".Translate(), comp.captive, MessageTypeDefOf.ThreatSmall, historical: false);
            TryStartAction(comp, CaptorAction.Discipline);
        }

        public static void StartLockpick(GameComponent_Captivity comp, Building_Door door)
        {
            Pawn captive = comp?.captive;
            if (captive == null || door == null) return;

            bool watched = CaptivityUtility.CountNearbyCaptors(comp, 14f) > 0;
            int pins = DoorPinCount(door);

            Find.WindowStack.Add(new Dialog_Lockpick(pins, watched, outcome =>
            {
                if (outcome == LockpickOutcome.Success)
                {
                    comp.MarkDoorPicked(door.thingIDNumber);
                    if (door.Spawned) door.StartManualOpenBy(captive);
                    Messages.Message("PSC_Msg_LockPicked".Translate(), captive, MessageTypeDefOf.PositiveEvent, false);
                }
                else if (outcome == LockpickOutcome.Caught)
                {
                    comp.heat = 100;
                    CaptivityCompat.UpdateTemperament(captive, comp.obedience);
                    Messages.Message("PSC_Msg_LockCaught".Translate(), captive, MessageTypeDefOf.ThreatSmall, false);
                    TryStartAction(comp, CaptorAction.Discipline);
                }
            }));
        }

        private static int DoorPinCount(Building_Door door)
        {
            int pins = 3;
            int hp = door.MaxHitPoints;
            if (hp >= 330) pins++;
            if (hp >= 600) pins++;
            return Mathf.Clamp(pins, 3, 5);
        }

        public static void RemoveOneRestraint(GameComponent_Captivity comp)
        {
            Pawn captive = comp?.captive;
            if (captive == null) return;
            Hediff sh = captive.health?.hediffSet?.GetFirstHediffOfDef(PSCDefOf.PSC_Shackled);
            if (sh != null)
            {
                captive.health.RemoveHediff(sh);
                Messages.Message("PSC_Msg_ShacklesOff".Translate(), captive, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            if (CaptivityCompat.IsCollared(captive))
            {
                CaptivityCompat.RemoveCollar(captive);
                Messages.Message("PSC_Msg_CollarOff".Translate(), captive, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        // Odds of stealing unseen. Shown on the button before the player commits.
        public static float StealChance(GameComponent_Captivity comp)
        {
            float c = 0.9f;
            c -= (comp.heat / 100f) * 0.4f;
            c -= CaptivityUtility.CountNearbyCaptors(comp, 10f) * 0.2f;
            return Mathf.Clamp(c, 0.05f, 0.95f);
        }

        public static void TryStealFood(GameComponent_Captivity comp, Thing food)
        {
            Pawn captive = comp?.captive;
            if (captive == null || food == null || !food.Spawned) return;

            if (Rand.Value >= StealChance(comp))
            {
                comp.heat = 100;
                CaptivityCompat.UpdateTemperament(captive, comp.obedience);
                Messages.Message("PSC_Msg_StealCaught".Translate(), captive, MessageTypeDefOf.ThreatSmall, false);
                TryStartAction(comp, CaptorAction.Discipline);
                return;
            }

            float nutrition = food.GetStatValue(StatDefOf.Nutrition);
            string label = food.LabelNoCount;
            if (food.stackCount > 1) food.SplitOff(1).Destroy();
            else food.Destroy();

            Need_Food need = captive.needs?.food;
            if (need != null)
                need.CurLevel = Mathf.Min(need.MaxLevel, need.CurLevel + Mathf.Max(0.05f, nutrition));

            comp.heat = CaptivityUtility.Clamp0100(comp.heat + 3);
            CaptivityUtility.ThrowCaptivityMote(captive, "PSC_Mote_Stole".Translate(), new Color(0.7f, 0.75f, 0.5f));
            Messages.Message("PSC_Msg_Stole".Translate(label), captive, MessageTypeDefOf.PositiveEvent, false);
        }

        public static bool TryFeed(GameComponent_Captivity comp)
        {
            Pawn captive = comp?.captive;
            if (captive == null || !captive.Spawned || captive.Dead) return false;

            Pawn feeder = CaptivityUtility.FindAvailableCaptor(comp, needsViolence: false);
            if (feeder == null) return false;

            Job job = JobMaker.MakeJob(PSCDefOf.PSC_FeedCaptive, captive);
            feeder.jobs.StartJob(job, JobCondition.InterruptForced, null,
                resumeCurJobAfterwards: true, cancelBusyStances: true);
            return true;
        }

        public static void OnFed(Pawn captive)
        {
            Need_Food food = captive?.needs?.food;
            if (food == null) return;
            food.CurLevel = Mathf.Min(food.MaxLevel, food.CurLevel + food.MaxLevel * 0.55f);
            CaptivityUtility.ThrowCaptivityMote(captive, "PSC_Mote_Fed".Translate(), new Color(0.7f, 0.75f, 0.5f));
            Messages.Message("PSC_Msg_Fed".Translate(), captive, MessageTypeDefOf.NeutralEvent, false);
        }

        // Obedience shifts from captor actions, dampened by Rimpsyche will (iron-willed
        // captives resist being broken). Refreshes the temperament in one place.
        private static void ShiftObedience(GameComponent_Captivity comp, int delta)
        {
            float will = CaptivityCompat.WillStrength(comp.captive);
            int applied = Mathf.RoundToInt(delta * (1f - 0.6f * will));
            comp.obedience = CaptivityUtility.Clamp0100(comp.obedience + applied);
            CaptivityCompat.UpdateTemperament(comp.captive, comp.obedience);
        }

        public static bool HarvestOrgan(GameComponent_Captivity comp)
        {
            Pawn captive = comp?.captive;
            if (captive?.health == null || captive.Dead) return false;

            BodyPartRecord part = FindHarvestablePart(captive);
            if (part == null) return false;

            captive.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            captive.needs?.mood?.thoughts?.memories?.TryGainMemory(PSCDefOf.PSC_Harvested);
            CaptivityUtility.ThrowCaptivityMote(captive, "PSC_Mote_Harvested".Translate(), new Color(0.7f, 0.1f, 0.1f));

            Find.LetterStack.ReceiveLetter(
                "PSC_Letter_HarvestTitle".Translate(),
                "PSC_Letter_HarvestBody".Translate(captive.LabelShortCap, part.Label),
                LetterDefOf.NegativeEvent, new LookTargets(captive));
            CaptivityLog.Msg("organ harvested from captive: " + part.Label);
            return true;
        }

        // A spare paired organ only, so removing it never kills.
        private static BodyPartRecord FindHarvestablePart(Pawn p)
        {
            string[] wanted = { "Kidney", "Lung" };
            for (int i = 0; i < wanted.Length; i++)
            {
                BodyPartDef bpd = DefDatabase<BodyPartDef>.GetNamedSilentFail(wanted[i]);
                if (bpd == null) continue;
                var parts = p.health.hediffSet.GetNotMissingParts().Where(bp => bp.def == bpd).ToList();
                if (parts.Count >= 2)
                    return parts.RandomElement();
            }
            return null;
        }

        // ----- Seize + struggle flow -----

        public static bool TryStartAction(GameComponent_Captivity comp, CaptorAction action)
        {
            Pawn captive = comp?.captive;
            if (captive == null || !captive.Spawned || captive.Downed || captive.Dead) return false;

            Pawn captor = CaptivityUtility.FindAvailableCaptor(comp, needsViolence: false);
            if (captor == null) return false;

            Job job = JobMaker.MakeJob(PSCDefOf.PSC_SeizeCaptive, captive);
            job.count = (int)action;
            captor.jobs.StartJob(job, JobCondition.InterruptForced, null,
                resumeCurJobAfterwards: true, cancelBusyStances: true);
            return true;
        }

        public static string ActionLabel(CaptorAction a)
        {
            switch (a)
            {
                case CaptorAction.Discipline: return "PSC_Action_Discipline".Translate();
                case CaptorAction.Harass: return "PSC_Action_Harass".Translate();
                case CaptorAction.Sell: return "PSC_Action_Sell".Translate();
                case CaptorAction.Harvest: return "PSC_Action_Harvest".Translate();
                default: return "";
            }
        }

        // 0.15 (easy) .. 0.95 (nearly impossible) chance-weighting for the struggle.
        public static float ComputeStruggleDifficulty(Pawn captive, Pawn captor)
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            float d = 0.25f;
            if (comp != null)
            {
                d += (comp.heat / 100f) * 0.35f;
                d += 0.08f * Mathf.Min(comp.resistStreak, 5);
            }
            if (captive?.health != null)
            {
                if (captive.health.hediffSet.HasHediff(PSCDefOf.PSC_Shackled)) d += 0.2f;
                float manip = captive.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
                d += (1f - Mathf.Clamp01(manip)) * 0.15f;
            }
            if (captor != null) d += Mathf.Clamp01(captor.BodySize - 1f) * 0.1f;
            return Mathf.Clamp(d, 0.15f, 0.95f);
        }

        public static void ResolveSeize(Pawn captor, Pawn captive, CaptorAction action, bool brokeFree)
        {
            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null || captive == null) return;

            // Put the captive down.
            if (captor?.carryTracker?.CarriedThing == captive)
                captor.carryTracker.TryDropCarriedThing(captor.Position, ThingPlaceMode.Near, out _);

            if (brokeFree) { OnStruggleBreakFree(comp, captor, captive); return; }

            switch (action)
            {
                case CaptorAction.Discipline: ApplyBeating(captor, captive); break;
                case CaptorAction.Harass: DoHarass(captor, captive); break;
                case CaptorAction.Sell: SellToNewOwner(comp); break;
                case CaptorAction.Harvest: HarvestOrgan(comp); break;
            }
            comp.resistStreak = 0;
        }

        private static void OnStruggleBreakFree(GameComponent_Captivity comp, Pawn captor, Pawn captive)
        {
            comp.resistStreak++;
            comp.heat = 100;
            comp.obedience = CaptivityUtility.Clamp0100(comp.obedience - 10);
            CaptivityCompat.UpdateTemperament(captive, comp.obedience);

            CaptivityUtility.ThrowCaptivityMote(captive, "PSC_Mote_BrokeFree".Translate(), new Color(0.4f, 0.85f, 0.4f));
            Messages.Message("PSC_Msg_BrokeFree".Translate(), captive, MessageTypeDefOf.PositiveEvent, historical: false);

            bool lethal = CaptivityMod.Settings != null && CaptivityMod.Settings.lethalOnEscape && comp.resistStreak >= 4;
            if (comp.resistStreak >= 2 && captor != null && !captor.Dead && !captor.Downed)
                SubdueBeating(captor, captive, lethal);
        }

        private static void ApplyBeating(Pawn captor, Pawn captive)
        {
            if (captive == null || captive.Dead) return;
            int hits = Rand.RangeInclusive(2, 3);
            for (int i = 0; i < hits; i++)
            {
                if (captive.Dead || captive.Downed) break;
                if (captive.health.summaryHealth.SummaryHealthPercent < 0.45f) break;
                captive.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Rand.Range(3f, 6f), 0f, -1f, captor));
            }
            OnDisciplined(captive);
        }

        private static void SubdueBeating(Pawn captor, Pawn captive, bool lethal)
        {
            if (captive == null || captive.Dead) return;
            int hits = Rand.RangeInclusive(3, 6);
            for (int i = 0; i < hits; i++)
            {
                if (captive.Dead) break;
                if (!lethal && (captive.Downed || captive.health.summaryHealth.SummaryHealthPercent < 0.2f)) break;
                captive.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Rand.Range(6f, 12f), 0f, -1f, captor));
            }
            Messages.Message("PSC_Msg_Subdued".Translate(), captive, MessageTypeDefOf.ThreatBig, historical: false);
        }

        private static void DoHarass(Pawn captor, Pawn captive)
        {
            if (captive == null) return;
            if (captor != null && captive.Spawned)
                Find.PlayLog.Add(new PlayLogEntry_Interaction(PSCDefOf.PSC_Harass, captor, captive, null));
            CaptivityUtility.ThrowCaptivityMote(captive, "PSC_Mote_Harassed".Translate(), new Color(0.85f, 0.7f, 0.35f));
            ApplyHarassEffects(captive);
        }
    }
}
