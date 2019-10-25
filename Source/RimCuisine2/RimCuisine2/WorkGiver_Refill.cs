using RimWorld;
using Verse;
using Verse.AI;

namespace RimCuisine2
{
    public class WorkGiver_Refill : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForGroup(ThingRequestGroup.Refuelable);
            }
        }

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        public JobDef JobStandard
        {
            get
            {
                return JobDefOfCuisine.Refill;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return RefillWorkGiverUtility.CanRefill(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return RefillWorkGiverUtility.RefillJob(pawn, t, forced);
        }
    }
}
