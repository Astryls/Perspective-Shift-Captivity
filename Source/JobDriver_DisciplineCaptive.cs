using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShiftCaptivity
{
    // A captor walks to the captive and lands non-lethal unarmed strikes, stopping
    // before the captive is downed or badly hurt. Real melee, so it animates and logs.
    public class JobDriver_DisciplineCaptive : JobDriver
    {
        private const int BeatDurationTicks = 240;
        private const int StrikeIntervalTicks = 34;
        private const float StopHealthPct = 0.4f;

        private int lastStrikeTick = -1;
        private bool struck;

        private Pawn Victim => job.GetTarget(TargetIndex.A).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => { Pawn v = Victim; return v == null || v.Dead; });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedOrNull(TargetIndex.A);

            Toil beat = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = BeatDurationTicks,
                handlingFacing = true
            };
            beat.tickAction = () =>
            {
                Pawn victim = Victim;
                if (victim == null || victim.Dead || victim.Downed
                    || victim.health.summaryHealth.SummaryHealthPercent < StopHealthPct)
                {
                    ReadyForNextToil();
                    return;
                }

                pawn.rotationTracker.FaceTarget(victim);

                int now = Find.TickManager.TicksGame;
                if (lastStrikeTick < 0 || now - lastStrikeTick >= StrikeIntervalTicks)
                {
                    lastStrikeTick = now;
                    Verb verb = CaptivityUtility.UnarmedMeleeVerb(pawn);
                    if (pawn.meleeVerbs.TryMeleeAttack(victim, verb))
                        struck = true;
                }
            };
            beat.AddFinishAction(() =>
            {
                if (struck)
                    CaptivityDirector.OnDisciplined(Victim);
            });
            yield return beat;
        }
    }
}
