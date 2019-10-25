using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
namespace RimCuisine2
{
    [StaticConstructorOnStartup]
    public class CompRefillable : ThingComp
    {
        public CompProperties_Refillable Props => (CompProperties_Refillable)this.props;

        public float ItemPercent(int index)
        {
            return Mathf.Ceil( this.items[index] / Props.itemCapacity);
        }
        
        public bool IsFull()
        {
            foreach(int i in items)
            {
                if (i != this.Props.itemCapacity) return false;
            }
            return true;
        }
        public bool IsFull(int index)
        {
            return items[index] == this.Props.itemCapacity;
        }

        public bool IsEmpty => items.All(x => x == 0);

        public int CountToRefill(int index)
        {
            return this.Props.itemCapacity - items[index];
        }

        public int ThingDefIndex(ThingDef td)
        {
            return this.parent.def.GetModExtension<NPDModExtension>().ingredientList.FindIndex(x => x.thingDef == td);
        }

        public bool HasEnoughItems()
        {
            if(!(this.parent.def.GetModExtension<NPDModExtension>().ingredientList is null) && this.parent.def.GetModExtension<NPDModExtension>().ingredientList.Count > 0)
            {
                for(int i = 0; i < this.parent.def.GetModExtension<NPDModExtension>().ingredientList.Count; i++)
                {
                    IngredientAndCostClass ing = this.parent.def.GetModExtension<NPDModExtension>().ingredientList[i];
                    if (this.items[i] < ing.nutritionCost) return false;
                }
                return true;
            }
            Log.Error("Must have Mod Extension if using Comp Refillable CompClass");
            return false;
        }

        public bool ShouldRefill(int index)
        {
            return this.ItemPercent(index) * Props.autoRefillCount <= this.items[index];
        }

        public bool ShouldAutoRefill
        {
            get
            {
                return !this.parent.IsBurning() && (this.flickComp is null || this.flickComp.SwitchIsOn) && this.parent.Map.designationManager.DesignationOn(this.parent, DesignationDefOf.Flick) == null && 
                    this.parent.Map.designationManager.DesignationOn(this.parent, DesignationDefOf.Deconstruct) == null;
            }
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            this.items = new List<int>();
            for(int i = 0; i < this.parent.def.GetModExtension<NPDModExtension>().ingredientList.Count; i++)
            {
                items.Add(0);
            }
            this.flickComp = this.parent.GetComp<CompFlickable>();
        }

        /*public override void PostDraw()
        {
            base.PostDraw();
            if(!this.HasEnoughItems())
            {
                this.parent.Map.overlayDrawer.DrawOverlay(this.parent, OverlayTypes.OutOfFuel);
            }
        }*/

        public override string CompInspectStringExtra()
        {
            string text = "";
            for (int i = 0; i < this.parent.def.GetModExtension<NPDModExtension>().ingredientList.Count; i++)
            {
                if (i > 0) text += "\n";
                text += string.Concat(new string[]
                {
                    this.parent.def.GetModExtension<NPDModExtension>().ingredientList[i].thingDef.LabelCap,
                    ": ", this.items[i].ToString(), " / ",
                    this.Props.itemCapacity.ToString()
                });
                
            }
            return text;
        }

        public void ConsumeAmount(int index, int amount)
        {
            if(!this.HasEnoughItems())
            {
                return;
            }
            this.items[index] -= amount;
            if(this.items[index] <= 0)
            {
                this.items[index] = 0;
                this.parent.BroadcastCompSignal("RanOutOfFuel");
            }
        }

        public void Refill(int index, int amount)
        {
            this.items[index] += amount;
            if (this.items[index] > this.Props.itemCapacity) this.items[index] = this.Props.itemCapacity;
            this.parent.BroadcastCompSignal("Refilled");
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if(items.Any(x => x < this.Props.itemCapacity))
            {
                if(!pawn.CanReach(this.parent, PathEndMode.InteractionCell, Danger.Deadly, false, TraverseMode.ByPawn))
                {
                    FloatMenuOption failer = new FloatMenuOption("CannotUseNoPath".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    yield return failer;
                }
                else if(this.IsFull())
                {
                    FloatMenuOption failer = new FloatMenuOption("Does not need to be refilled", null, MenuOptionPriority.Default, null, null, 0f, null, null);
                    yield return failer;
                }
                else
                {
                    string jobStr = "Filling Machine";
                    Action jobAction = delegate ()
                    {
                        Job job = RefillWorkGiverUtility.RefillJob(pawn, this.parent, true);
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    };
                    yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(jobStr, jobAction, MenuOptionPriority.Default, null, null, 0f, null, null), pawn, this.parent, "ReservedBy");
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if(Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Debug: Set all item counts to 0",
                    action = delegate ()
                    {
                        for(int i = 0; i < this.items.Count; i++) this.items[i] = 0;
                        this.parent.BroadcastCompSignal("Refilled");
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "Debug: Set all item counts to HALF",
                    action = delegate ()
                    {
                        for(int i = 0; i < this.items.Count; i++) this.items[i] = (int)(this.Props.itemCapacity / 2);
                        this.parent.BroadcastCompSignal("Refilled");
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "Debug: Set all item counts to FULL",
                    action = delegate ()
                    {
                        for(int i = 0; i < this.items.Count; i++) items[i] = this.Props.itemCapacity;
                        this.parent.BroadcastCompSignal("Refilled");
                    }
                };
            }
            yield break;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref items, "items");
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if(!(previousMap is null) && !this.IsEmpty)
            {
                List<ThingDef> thingDefs = new List<ThingDef>();
                List<int> amount = new List<int>();
                List<IngredientAndCostClass> ingredients = this.parent.def.GetModExtension<NPDModExtension>().ingredientList;

                for(int i = 0; i < items.Count; i++)
                {
                    if(items[i] > 0)
                    {
                        thingDefs.Add(ingredients[i].thingDef);
                        amount.Add(items[i]);
                    }
                }
                List<Thing> things = new List<Thing>();
                for(int i = 0; i < thingDefs.Count; i++)
                {
                    things.Add(ThingMaker.MakeThing(thingDefs[i]));
                    things[i].stackCount = amount.Pop();
                }
                things.ForEach(x => GenPlace.TryPlaceThing(x, this.parent.Position, previousMap, ThingPlaceMode.Near, null, null));
            }
        }

        public List<int> items = new List<int>();

        private CompFlickable flickComp;

        public const string RefilledSignal = "Refilled";

        public const string RanOutOfItems = "RanOutOfItems";
    }
}
