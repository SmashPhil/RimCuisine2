using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCuisine2
{
    public static class RefillWorkGiverUtility
    {
        public static bool CanRefill(Pawn pawn, Thing t, bool forced = false)
        {
            CompRefillable compRefill = t.TryGetComp<CompRefillable>();
            if (compRefill is null || compRefill.IsFull()) return false;
            bool flag = !forced;
            if (flag && !compRefill.ShouldAutoRefill) return false;
            if(!t.IsForbidden(pawn))
            {
                LocalTargetInfo target = t;
                if(pawn.CanReserve(target, 1, -1, null, forced))
                {
                    if (t.Faction != pawn.Faction) return false;
                    if(RefillWorkGiverUtility.FindNextFuelItem(pawn, t) is null)
                    {
                        JobFailReason.Is("No items available to refill", null);
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public static Job RefillJob(Pawn pawn, Thing t, bool forced = false)
        {
            Thing t2 = RefillWorkGiverUtility.FindNextFuelItem(pawn, t);
            return new Job(JobDefOfCuisine.Refill, t, t2);
        }

        private static Thing FindNextFuelItem(Pawn pawn, Thing refuelable)
        {
            List<IngredientAndCostClass> ingredients = refuelable.def.GetModExtension<NPDModExtension>().ingredientList;
            int i = 0;
            bool found = false;
            for (i = 0; i < refuelable.TryGetComp<CompRefillable>().items.Count; i++)
            {
                if (refuelable.TryGetComp<CompRefillable>().items[i] < refuelable.def.GetCompProperties<CompProperties_Refillable>().itemCapacity)
                {
                    found = true;
                    break;
                }
            }
            ThingDef item = null;
            int quantity = 0;
            if (found)
            {
                Log.Message("found : " + i);
                Log.Message("-> " + refuelable.TryGetComp<CompRefillable>().items.Count);
                item = ingredients[i].thingDef;
                quantity = refuelable.TryGetComp<CompRefillable>().CountToRefill(i);
            }
            Predicate<Thing> validator = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false) && x.def == item;
            IntVec3 position = pawn.Position;
            Map map = pawn.Map;
            ThingRequest itemThingRequest = ThingRequest.ForDef(item);
            PathEndMode peMode = PathEndMode.ClosestTouch;
            TraverseParms traverseParams = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false);
            return GenClosest.ClosestThingReachable(position, map, itemThingRequest, peMode, traverseParams, 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false);

        }
        private static List<Thing> FindAllFuelItem(Pawn pawn, Thing refuelable)
        {
            List<IngredientAndCostClass> ingredients = refuelable.def.GetModExtension<NPDModExtension>().ingredientList;
            int i = 0;
            bool found = false;
            for(i = 0; i < refuelable.TryGetComp<CompRefillable>().items.Count; i++)
            {
                if (refuelable.TryGetComp<CompRefillable>().items[i] < refuelable.def.GetCompProperties<CompProperties_Refillable>().itemCapacity)
                {
                    found = true;
                    break;
                }
            }
            ThingDef item = null;
            int quantity = 0;
            if (found)
            {
                item = ingredients[i].thingDef;
                quantity = refuelable.TryGetComp<CompRefillable>().CountToRefill(i);
            }
            Predicate<Thing> validator = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false) && x.def == item;
            IntVec3 position = refuelable.Position;
            Region region = position.GetRegion(pawn.Map, RegionType.Set_Passable);
            TraverseParms traverseParams = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false);
            RegionEntryPredicate entryCondition = (Region from, Region r) => r.Allows(traverseParams, false);
            List<Thing> chosenThings = new List<Thing>();
            int accumulatedQuantity = 0;
            RegionProcessor regionProcessor = delegate (Region r)
            {
                List<Thing> list = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                for (int j = 0; j < list.Count; j++)
                {
                    Thing thing = list[j];
                    if (validator(thing))
                    {
                        if (!chosenThings.Contains(thing))
                        {
                            if (ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch, pawn))
                            {
                                chosenThings.Add(thing);
                                accumulatedQuantity += thing.stackCount;
                                if (accumulatedQuantity >= quantity)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(region, entryCondition, regionProcessor, 99999, RegionType.Set_Passable);
            if(accumulatedQuantity >= quantity)
            {
                return chosenThings;
            }
            return null;
        }
    }
}
