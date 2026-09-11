using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PerspectiveShiftCaptivity
{
    // A captor walks up and verbally degrades the captive (SFW). Emits a play-log
    // interaction so bubbles/voice mods react, then applies mood and meter effects.
    public class JobDriver_HarassCaptive : JobDriver
    {
        private const int TauntDurationTicks = 160;

        private Pawn Victim => job.GetTarget(TargetIndex.A).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => { Pawn v = Victim; return v == null || v.Dead || v.Downed; });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedOrNull(TargetIndex.A);

            Toil taunt = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = TauntDurationTicks,
                handlingFacing = true
            };
            taunt.initAction = () =>
            {
                Pawn victim = Victim;
                if (victim == null) return;
                pawn.rotationTracker.FaceTarget(victim);
                Find.PlayLog.Add(new PlayLogEntry_Interaction(PSCDefOf.PSC_Harass, pawn, victim, null));
                CaptivityUtility.ThrowCaptivityMote(victim, "PSC_Mote_Harassed".Translate(), new Color(0.85f, 0.7f, 0.35f));
            };
            taunt.tickAction = () =>
            {
                Pawn victim = Victim;
                if (victim != null) pawn.rotationTracker.FaceTarget(victim);
            };
            taunt.AddFinishAction(() => CaptivityDirector.ApplyHarassEffects(Victim));
            yield return taunt;
        }
    }
}
