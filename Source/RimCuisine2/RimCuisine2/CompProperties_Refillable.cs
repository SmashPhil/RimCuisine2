using UnityEngine;
using Verse;

namespace RimCuisine2
{
    public class CompProperties_Refillable : CompProperties
    {
        public CompProperties_Refillable()
        {
            this.compClass = typeof(CompRefillable);
        }

        public int itemCapacity;

        public int autoRefillCount;

        public bool drawOutOfItemsOverlay;

        public ThingFilter itemFilter;
    }
}
