using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimCuisine2
{
    public class JobDriver_Refill : JobDriver
    {
        protected Thing Refillable => this.job.GetTarget(TargetIndex.A).Thing;

        protected CompRefillable RefillableComp => this.Refillable.TryGetComp<CompRefillable>();

        protected Thing Stuff => this.job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = this.Refillable;
            Job job = this.job;
            bool result;
            if(pawn.Reserve(target, job, 1, -1, null, errorOnFailed))
            {
                pawn = this.pawn;
                target = this.Stuff;
                job = this.job;
                result = pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
            }
            else
            {
                result = false;
            }
            return result;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            base.AddEndCondition(() => (!this.RefillableComp.IsFull()) ? JobCondition.Ongoing : JobCondition.Succeeded);
            base.AddFailCondition(() => !this.job.playerForced && !this.RefillableComp.ShouldAutoRefill);
            yield return Toils_General.DoAtomic(delegate
            {
                this.job.count = this.RefillableComp.CountToRefill(this.RefillableComp.ThingDefIndex(this.Stuff.def));
            });
            Toil reserveStuff = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
            yield return reserveStuff;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveStuff, TargetIndex.B, TargetIndex.None, true, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(RefillingDuration, TargetIndex.None).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(
                TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return FinalizeRefilling(TargetIndex.A, TargetIndex.B);
            yield break;
        }

        public Toil FinalizeRefilling(TargetIndex A, TargetIndex B)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.CurJob;
                Thing thing = curJob.GetTarget(A).Thing;
                int amount = curJob.GetTarget(B).Thing.stackCount;
                thing.TryGetComp<CompRefillable>().Refill(this.RefillableComp.ThingDefIndex(this.Stuff.def), amount);
                Stuff.Destroy();
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private const int RefillingDuration = 300;
    }
}
