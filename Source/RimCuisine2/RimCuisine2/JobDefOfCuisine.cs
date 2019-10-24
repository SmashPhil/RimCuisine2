using RimWorld;
using Verse;

namespace RimCuisine2
{
    [DefOf]
    public static class JobDefOfCuisine
    {
        static JobDefOfCuisine()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOfCuisine));
        }

        public static JobDef Refill;
    }
}
