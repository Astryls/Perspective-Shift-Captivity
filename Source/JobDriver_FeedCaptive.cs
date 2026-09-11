using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShiftCaptivity
{
    // A captor walks over and tosses the captive a ration. Keeps a held captive alive
    // (AI factions run no warden jobs, so without this the captive would just starve).
    public class JobDriver_FeedCaptive : JobDriver
    {
        private const int FeedTicks = 120;

        private Pawn Captive => job.GetTarget(TargetIndex.A).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => { Pawn c = Captive; return c == null || c.Dead || !c.Spawned; });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedOrNull(TargetIndex.A);

            Toil feed = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = FeedTicks,
                handlingFacing = true
            };
            feed.tickAction = () =>
            {
                Pawn c = Captive;
                if (c != null) pawn.rotationTracker.FaceTarget(c);
            };
            feed.AddFinishAction(() => CaptivityDirector.OnFed(Captive));
            yield return feed;
        }
    }
}
