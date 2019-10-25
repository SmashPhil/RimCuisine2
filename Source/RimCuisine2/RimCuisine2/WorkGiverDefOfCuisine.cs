using RimWorld;

namespace RimCuisine2
{
    [DefOf]
    public static class WorkGiverDefOfCuisine
    {
        static WorkGiverDefOfCuisine()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WorkGiverDefOfCuisine));
        }

        public static WorkGiverDef Refill;
    }
}
