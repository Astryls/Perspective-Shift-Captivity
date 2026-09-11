using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PerspectiveShiftCaptivity
{
    // Unified captor action: walk to the captive, physically carry them off to a processing
    // spot, run the struggle minigame, then either the captive breaks free (escalation) or
    // the seized-for action is applied. The action is carried in job.count.
    public class JobDriver_SeizeCaptive : JobDriver
    {
        private bool struggleResolved;
        private bool brokeFree;
        private IntVec3 destCell;

        private Pawn Captive => job.GetTarget(TargetIndex.A).Thing as Pawn;
        private CaptorAction Action => (CaptorAction)job.count;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // No global FailOnDespawnedOrNull: the captive is intentionally despawned (carried)
            // for most of this job. Only fail on null/dead. The goto toil (pre-grab) still fails
            // if the captive despawns before we reach them.
            this.FailOn(() => { Pawn c = Captive; return c == null || c.Dead; });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedOrNull(TargetIndex.A);

            // Grab the captive into the captor's arms.
            Toil grab = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
            grab.initAction = () =>
            {
                Pawn c = Captive;
                if (c == null || c.Dead || !c.Spawned) { EndJobWith(JobCondition.Incompletable); return; }
                if (pawn.carryTracker.TryStartCarry(c, 1, reserve: false) <= 0) { EndJobWith(JobCondition.Incompletable); return; }
                destCell = CaptivityUtility.FindProcessingSpot(pawn);
            };
            yield return grab;

            // Carry them off.
            Toil carry = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            carry.initAction = () => pawn.pather.StartPath(destCell, PathEndMode.OnCell);
            carry.tickAction = () =>
            {
                if (!pawn.pather.Moving || pawn.Position == destCell)
                    ReadyForNextToil();
            };
            yield return carry;

            // Struggle minigame, then resolve.
            Toil struggle = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            struggle.initAction = () =>
            {
                Pawn c = Captive;
                if (c == null) { EndJobWith(JobCondition.Incompletable); return; }
                if (CaptivityMod.Settings != null && !CaptivityMod.Settings.enableStruggleMinigame)
                {
                    brokeFree = false;
                    struggleResolved = true;
                    return;
                }
                float diff = CaptivityDirector.ComputeStruggleDifficulty(c, pawn);
                Find.WindowStack.Add(new Dialog_Struggle(
                    "PSC_Struggle_Title".Translate(),
                    "PSC_Struggle_Desc".Translate(CaptivityDirector.ActionLabel(Action)),
                    diff, result => { brokeFree = result; struggleResolved = true; }));
            };
            struggle.tickAction = () =>
            {
                if (!struggleResolved) return;
                CaptivityDirector.ResolveSeize(pawn, Captive, Action, brokeFree);
                ReadyForNextToil();
            };
            yield return struggle;
        }
    }
}
