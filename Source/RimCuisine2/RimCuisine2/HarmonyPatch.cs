using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace RimCuisine2
{

    [StaticConstructorOnStartup]
    internal static class RimCuisineHarmony
    {
        static RimCuisineHarmony()
        {
            var harmony = HarmonyInstance.Create("rimworld.smashphil.rimcuisine2");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            

            #region FunctionDefs
            //Building
            harmony.Patch(original: AccessTools.Property(type: typeof(Building_NutrientPasteDispenser), name: nameof(Building_NutrientPasteDispenser.DispensableDef)).GetGetMethod(),
                prefix: new HarmonyMethod(type: typeof(RimCuisineHarmony), 
                name: nameof(DispensableDefCustom)));
            /*BUG FIX: CUSTOM NPDs DONT UPDATE PRISONER COLOR */
            harmony.Patch(original: AccessTools.Method(type: typeof(Building_NutrientPasteDispenser), name: nameof(Building_NutrientPasteDispenser.TryDispenseFood)),
                prefix: new HarmonyMethod(type: typeof(RimCuisineHarmony), 
                name: nameof(TryDispenseCustomFood)));
            harmony.Patch(original: AccessTools.Method(type: typeof(Building_NutrientPasteDispenser), name: nameof(Building_NutrientPasteDispenser.HasEnoughFeedstockInHoppers)),
                prefix: new HarmonyMethod(type: typeof(RimCuisineHarmony), 
                name: nameof(HasEnoughFeedstockInStorage)));

            //Jobs
            harmony.Patch(original: AccessTools.Method(type: typeof(JobDriver_FoodDeliver), name: nameof(JobDriver_FoodDeliver.GetReport)), prefix: null,
                postfix: new HarmonyMethod(type: typeof(RimCuisineHarmony), 
                name: nameof(GetReportModified)));
            harmony.Patch(original: AccessTools.Method(type: typeof(JobDriver_Ingest), name: nameof(JobDriver_Ingest.GetReport)), prefix: null,
                postfix: new HarmonyMethod(type: typeof(RimCuisineHarmony),
                name: nameof(GetReportIngestModified)));

            harmony.Patch(original: AccessTools.Method(type: typeof(Alert_PasteDispenserNeedsHopper), name: nameof(Alert_PasteDispenserNeedsHopper.GetReport)), prefix: null, postfix: null,
                transpiler: new HarmonyMethod(type: typeof(RimCuisineHarmony), 
                name: nameof(BadDispenserReportModified)));
            harmony.Patch(original: AccessTools.Method(type: typeof(ThingListGroupHelper), name: nameof(ThingListGroupHelper.Includes)), prefix: null, postfix: null,
                transpiler: new HarmonyMethod(type: typeof(RimCuisineHarmony),
                name: nameof(ThingRequestGroupTranspiler)));
            harmony.Patch(original: AccessTools.Method(type: typeof(WorkGiver_DoBill), name: nameof(WorkGiver_DoBill.JobOnThing)), prefix: null,
                postfix: new HarmonyMethod(type: typeof(RimCuisineHarmony),
                name: nameof(JobOnThingRefill)));
            #endregion #FunctionDefs
        }

        public static System.Random rand = new System.Random();

        public static bool DispensableDefCustom(ref ThingDef __result, Building_NutrientPasteDispenser __instance)
        {
            if(__instance.def.HasModExtension<NPDModExtension>())
            {

                __result = __instance.def.GetModExtension<NPDModExtension>().customMeal[rand.Next(__instance.def.GetModExtension<NPDModExtension>().customMeal.Count)];
                return false;
            }
            return true;
        }

        public static bool TryDispenseCustomFood(ref Thing __result, Building_NutrientPasteDispenser __instance, ref List<IntVec3> ___cachedAdjCellsCardinal)
        {
            if (__instance.def.HasModExtension<NPDModExtension>())
            {
                if (!__instance.CanDispenseNow)
                {
                    __result = null;
                    return false;
                }
                List<ThingDef> list = new List<ThingDef>();
                if (!(__instance.def.GetCompProperties<CompProperties_Refillable>() is null))
                {
                    List<IngredientAndCostClass> ingredients = __instance.def.GetModExtension<NPDModExtension>().ingredientList;
                    for(int i = 0; i < ingredients.Count; i++)
                    {
                        __instance.GetComp<CompRefillable>().ConsumeAmount(i, (int)(ingredients[i].nutritionCost * 10));
                        list.Add(ingredients[i].thingDef);
                    }
                    goto Block_3;
                }
                List<IngredientAndCostClass> ingredientList = __instance.def.GetModExtension<NPDModExtension>().ingredientList;
                bool empty = !__instance.def.GetModExtension<NPDModExtension>().ingredientList.Any();
                if (!empty)
                {            
                    float[] nutritionLeft = new float[ingredientList.Count];
                    for (int i = 0; i < nutritionLeft.Length; i++)
                    {
                        nutritionLeft[i] = ingredientList[i].nutritionCost;
                    }
                    for (; ;)
                    {
                        Thing thing = __instance.def.GetModExtension<NPDModExtension>().FindNextIngredientInHopper(___cachedAdjCellsCardinal, __instance, nutritionLeft);
                        if (thing is null) break;
                        int index = ingredientList.FindIndex(x => x.thingDef == thing.def);
                        int num2 = Mathf.Min(thing.stackCount, Mathf.CeilToInt(nutritionLeft[index] / thing.GetStatValue(StatDefOf.Nutrition, true)));
                        nutritionLeft[index] -= (float)num2 * thing.GetStatValue(StatDefOf.Nutrition, true);
                        list.Add(thing.def);
                        thing.SplitOff(num2);
                        if(!nutritionLeft.Any(x => x > 0f))
                        {
                            goto Block_3;
                        }
                    }
                }
                else
                {
                    float num = __instance.def.building.nutritionCostPerDispense - 0.0001f;
                    for (; ; )
                    {
                        Thing thing = __instance.FindFeedInAnyHopper();
                        if (thing is null)
                        {
                            break;
                        }
                        int num2 = Mathf.Min(thing.stackCount, Mathf.CeilToInt(num / thing.GetStatValue(StatDefOf.Nutrition, true)));
                        num -= (float)num2 * thing.GetStatValue(StatDefOf.Nutrition, true);
                        list.Add(thing.def);
                        thing.SplitOff(num2);
                        if (num <= 0f)
                        {
                            goto Block_3;
                        }
                    }
                }
                
                Log.Error("Did not find enough food in hoppers while trying to dispense.", false);
                __result = null;
                return false;

                Block_3:
                __instance.def.building.soundDispense.PlayOneShot(new TargetInfo(__instance.Position, __instance.Map, false));
                Thing thing2 = ThingMaker.MakeThing(__instance.def.GetModExtension<NPDModExtension>().customMeal[rand.Next(__instance.def.GetModExtension<NPDModExtension>().customMeal.Count)], null);
                CompIngredients compIngredients = thing2.TryGetComp<CompIngredients>();
                if(!(compIngredients is null))
                {
                    foreach (ThingDef ingredient in list)
                    {
                        if (!__instance.def.GetModExtension<NPDModExtension>().mysteryIngredients)
                        {
                            compIngredients.RegisterIngredient(ingredient);
                        }

                    }
                }
                __result = thing2;
                return false;
            }
            return true;
        }

        public static bool HasEnoughFeedstockInStorage(ref bool __result, Building_NutrientPasteDispenser __instance)
        {
            if (!(__instance.def.GetModExtension<NPDModExtension>() is null))
            {
                float num = 0f;
                bool empty = __instance.def.GetModExtension<NPDModExtension>().ingredientList is null ||
                    !__instance.def.GetModExtension<NPDModExtension>().ingredientList.Any();
                if (!empty && !(__instance.def.GetCompProperties<CompProperties_Refillable>() is null))
                {
                    __result = false;
                    List<IngredientAndCostClass> ingredientList = __instance.def.GetModExtension<NPDModExtension>().ingredientList;

                    for(int i = 0; i < ingredientList.Count; i++)
                    {
                        IngredientAndCostClass ingredient = ingredientList[i];
                        if (__instance.GetComp<CompRefillable>().items[i] < ingredient.nutritionCost)
                        {
                            __result = false;
                            return false;
                        }
                    }
                    __result = true;
                    return false;
                }
                Log.Error("Must include ingredient list if using CompRefillable comp class");
            }
            return true;
        }
        public static void GetReportModified(ref string __result, JobDriver_FoodDeliver __instance)
        {
            Thing targetBuilding = __instance.job.GetTarget(TargetIndex.A).Thing;
            Pawn deliveree = (Pawn)__instance.job.targetB.Thing;
            if (targetBuilding is Building_NutrientPasteDispenser && deliveree != null)
            {
                __result = __instance.job.def.reportString.Replace("TargetA", targetBuilding.def.GetModExtension<NPDModExtension>().customMeal[rand.Next(
                    targetBuilding.def.GetModExtension<NPDModExtension>().customMeal.Count)].label)
                    .Replace("TargetB", deliveree.LabelShort);
            }
            //__result = base.GetReport();
        }

        public static void GetReportIngestModified(ref string __result, JobDriver_Ingest __instance)
        {
            if(__instance.job.GetTarget(TargetIndex.A).Thing is Building_NutrientPasteDispenser && !((Pawn)__instance.job.targetB.Thing is null))
            {
                __result = __instance.job.def.reportString.Replace("TargetA", __instance.job.GetTarget(TargetIndex.A).Thing.def.GetModExtension<NPDModExtension>().customMeal[rand.Next(
                    __instance.job.GetTarget(TargetIndex.A).Thing.def.GetModExtension<NPDModExtension>().customMeal.Count)].label).Replace("TargetB", ((Pawn)__instance.job.targetB.Thing).LabelShort);
            }
        }

        private static IEnumerable<CodeInstruction> BadDispenserReportModified(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            bool firstcall = true;
            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];

                if(instruction.opcode == OpCodes.Call && firstcall)
                {
                    yield return instruction;
                    yield return new CodeInstruction(opcode: OpCodes.Call, operand: AccessTools.Method(type: typeof(RimCuisineHarmony), name: nameof(RimCuisineHarmony.ModifyBadDispensers)));
                    firstcall = false;
                    instruction = instructionList[++i];
                }
                yield return instruction;
            }
        }

        public static IEnumerable<CodeInstruction> ThingRequestGroupTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            List<CodeInstruction> instructionList = instructions.ToList();
            Label label = ilg.DefineLabel();
            Label label2 = ilg.DefineLabel();

            for(int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];
                if(instruction.opcode == OpCodes.Ldtoken && instruction.operand == typeof(CompRefuelable))
                {
                    for(;;)
                    {
                        instruction = instructionList[++i];
                        if (instruction.opcode == OpCodes.Ret) break;
                    }
                    yield return new CodeInstruction(opcode: OpCodes.Brtrue, operand: label);

                    yield return new CodeInstruction(opcode: OpCodes.Ldarg_1);
                    yield return new CodeInstruction(opcode: OpCodes.Ldtoken, operand: typeof(CompRefuelable));
                    yield return new CodeInstruction(opcode: OpCodes.Call, operand: AccessTools.Method(type: typeof(System.Type), name: nameof(System.Type.GetTypeFromHandle)));
                    yield return new CodeInstruction(opcode: OpCodes.Callvirt, operand: AccessTools.Method(type: typeof(ThingDef), name: nameof(ThingDef.HasComp)));
                    yield return new CodeInstruction(opcode: OpCodes.Br_S, operand: label2);

                    yield return new CodeInstruction(opcode: OpCodes.Ldc_I4_1) { labels = new List<Label>() { label } };

                    yield return new CodeInstruction(opcode: OpCodes.Ret) { labels = new List<Label>() { label2 } };
                }
                yield return instruction;
            }
        }

        public static void JobOnThingRefill(Pawn pawn, Thing thing, ref Job __result, WorkGiver_DoBill __instance, bool forced = false)
        {
            if(__result is null)
            {
                IBillGiver billGiver = thing as IBillGiver;
                if(!(billGiver is null) && __instance.ThingIsUsableBillGiver(thing) && billGiver.BillStack.AnyShouldDoNow && billGiver.UsableForBillsAfterFueling())
                {
                    LocalTargetInfo target = thing;
                    if(pawn.CanReserve(target, 1, -1, null, forced) && !thing.IsBurning() && !thing.IsForbidden(pawn))
                    {
                        CompRefillable compRefillable = thing.TryGetComp<CompRefillable>();
                        if(compRefillable is null || compRefillable.IsFull())
                        {
                            billGiver.BillStack.RemoveIncompletableBills();
                            __result = (Job)AccessTools.Method(type: typeof(WorkGiver_DoBill), name: "StartOrResumeBillJob").Invoke(__instance, new object[] { pawn, billGiver });
                            return;
                        }
                        if (!RefillWorkGiverUtility.CanRefill(pawn, thing, forced))
                        {
                            __result = null;
                            return;
                        }  
                        __result = RefillWorkGiverUtility.RefillJob(pawn, thing, forced);
                    }
                }
                return;
            }
        }
        public static IEnumerable<Thing> ModifyBadDispensers(IEnumerable<Thing> dispensers)
        {
            List<Thing> results = dispensers.ToList();
            foreach(Thing t in dispensers)
            {
                if(t.def.HasComp(typeof(CompRefillable)))
                {
                    results.Remove(t);
                }
            }
            return results;
        }
    }
}
