using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using UnityEngine;
using Verse;
using System.Linq;
using Verse.AI;
using RimWorld;

namespace Advanced_Cultivation
{
    [DefOf]
    public static class ThingDefOf
    {
        public static ThingDef AC_RawCompost;
        public static ThingDef AC_FermentedCompost;
        public static ThingDef AC_CompostBin;
    }

    [DefOf]
    public static class JobDefOf
    {
        public static JobDef AC_EmptyCompostBin;
        public static JobDef AC_FillCompostBin;
    }

    public class CompProperties_AC_Fermenter : CompProperties
    {
        public float fermenterDaystoFerment = 1f;
        public ThingDef fermentedThing;

        public CompProperties_AC_Fermenter()
        {
            this.compClass = typeof(AC_CompFermenter);
        }
    }

    // Fermenter Comps //
    
    public class AC_CompFermenter : ThingComp
    {
        public float fermentProgress;

        public CompProperties_AC_Fermenter Props
        {
            get
            {
                return (CompProperties_AC_Fermenter)this.props;
            }
        }

        private CompTemperatureRuinable FreezerComp
        {
            get
            {
                return this.parent.GetComp<CompTemperatureRuinable>();
            }
        }

        public bool TemperatureDamaged
        {
            get
            {
                CompTemperatureRuinable freezerComp = this.FreezerComp;
                return freezerComp != null && this.FreezerComp.Ruined;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<float>(ref this.fermentProgress, "AC.FermentProgress".Translate(), 0f, false);
        }

        public override void CompTick()
        {
            if (!this.TemperatureDamaged)
            {
                float fermentPerTick = 1f / (this.Props.fermenterDaystoFerment * 60000f);
                this.fermentProgress += fermentPerTick;
                if (this.fermentProgress >= 1f)
                {
                    this.Ferment();
                }
            }
            else
            {
                this.parent.Destroy(DestroyMode.Vanish);
            }
        }

        public void Ferment() // Create Fermented Compost on tile
        {
            Thing thing = ThingMaker.MakeThing(this.Props.fermentedThing, null);
            thing.stackCount = this.parent.stackCount;
            GenSpawn.Spawn(thing, parent.Position, parent.Map);
            this.parent.Destroy(DestroyMode.Vanish);
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            float t = (float)count / (float)(this.parent.stackCount + count);
            AC_CompFermenter comp = ((ThingWithComps)otherStack).GetComp<AC_CompFermenter>();
            float b = comp.fermentProgress;
            this.fermentProgress = Mathf.Lerp(this.fermentProgress, b, t);
        }

        public override void PostSplitOff(Thing piece)
        {
            AC_CompFermenter comp = ((ThingWithComps)piece).GetComp<AC_CompFermenter>();
            comp.fermentProgress = this.fermentProgress;
        }

        public override string CompInspectStringExtra()
        {
            if (!this.TemperatureDamaged)
            {
                return "AC.FermentProgress".Translate() + this.fermentProgress.ToStringPercent();
            }
            return null;
        }
    }

    // AC_Buildings //

    [StaticConstructorOnStartup]
    public class Building_AC_CompostBin : Building
    {
        // Graphics stuff... //
        [Unsaved]
        public static Graphic compostBinEmpty = GraphicDatabase.Get<Graphic_Single>("CompostBin", ShaderDatabase.Cutout);
        [Unsaved]
        public static Graphic compostBinRaw = GraphicDatabase.Get<Graphic_Single>("CompostBinRaw", ShaderDatabase.Cutout);
        [Unsaved]
        public static Graphic compostBinFermented = GraphicDatabase.Get<Graphic_Single>("CompostBinFermented", ShaderDatabase.Cutout);

        private float progressInt;
        private int compostCount;
        private Material barFilledCachedMat;
        private const int BaseFermentationDuration = 360000;
        private bool fermentFlag = false;
        public const int Capacity = 25;
        public const float minFermentTemperature = 10f;
        public const float maxFermentTemperature = 71f;
        private static readonly Vector2 BarSize = new Vector2(0.55f, 0.1f);
        private static readonly Color BarZeroProgressColor = new Color(0.4f, 0.27f, 0.22f);
        private static readonly Color BarFermentedColor = new Color(1.0f, 0.8f, 0.3f);
        private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(
            new Color(0.3f, 0.3f, 0.3f), false);

        public float Progress
        {
            get
            {
                return this.progressInt;
            }
            set
            {
                if (value == this.progressInt)
                {
                    return;
                }
                this.progressInt = value;
                this.barFilledCachedMat = null;
            }
        }

        private Material BarFilledMat
        {
            get
            {
                if (this.barFilledCachedMat == null)
                {
                    this.barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp
                        (Building_AC_CompostBin.BarZeroProgressColor,
                        Building_AC_CompostBin.BarFermentedColor, this.Progress), false);
                }
                return this.barFilledCachedMat;
            }
        }

        public int SpaceLeftForCompost
        {
            get
            {
                if (this.Fermented)
                {
                    return 0;
                }
                return Building_AC_CompostBin.Capacity - this.compostCount;
            }
        }

        private bool Empty
        {
            get
            {
                return this.compostCount <= 0;
            }
        }

        public bool Fermented
        {
            get
            {
                return !this.Empty && this.Progress >= 1f;
            }
        }

        private float CurrentTempProgressSpeedFactor
        {
            get
            {
                CompProperties_TemperatureRuinable compProperties =
                    this.def.GetCompProperties<CompProperties_TemperatureRuinable>();
                float ambientTemperature = base.AmbientTemperature;
                if (ambientTemperature <= compProperties.minSafeTemperature)
                {
                    return 0.0f;
                }
                if (ambientTemperature <= Building_AC_CompostBin.minFermentTemperature)
                {
                    return GenMath.LerpDouble(compProperties.minSafeTemperature,
                        Building_AC_CompostBin.minFermentTemperature,
                        0.0f, 1f, ambientTemperature);
                }
                if (ambientTemperature <= Building_AC_CompostBin.maxFermentTemperature)
                {
                    return 1.0f;
                }
                if (ambientTemperature <= compProperties.maxSafeTemperature)
                {
                    return GenMath.LerpDouble(Building_AC_CompostBin.maxFermentTemperature,
                        compProperties.maxSafeTemperature,
                        1f, 0.0f, ambientTemperature);
                }
                return 0.0f;
            }
        }

        private float ProgressPerTickAtCurrentTemp
        {
            get
            {
                return 2.777777778E-6f * this.CurrentTempProgressSpeedFactor;
            }
        }

        private int EstimatedTicksLeft
        {
            get
            {
                return Mathf.Max(Mathf.RoundToInt((1f - this.Progress) /
                    this.ProgressPerTickAtCurrentTemp), 0);
            }
        }

        public override Graphic Graphic
        {
            get
            {
                if (this.Empty)
                {
                    if (Building_AC_CompostBin.compostBinEmpty != null)
                    {
                        return Building_AC_CompostBin.compostBinEmpty;
                    }
                    Log.Warning(this.def + " has no graphic data for its empty state!");
                }
                if (!this.Fermented)
                {
                    if (Building_AC_CompostBin.compostBinRaw != null)
                    {
                        return Building_AC_CompostBin.compostBinRaw;
                    }
                    Log.Warning(this.def + " has no graphic data for its filled with raw compost state!");
                }
                if (Building_AC_CompostBin.compostBinFermented != null)
                {
                    return Building_AC_CompostBin.compostBinFermented;
                }
                if (Building_AC_CompostBin.compostBinFermented == null)
                {
                    Log.Warning(this.def + " has no graphic data for its filled with fermented compost state!");
                }
                Log.ErrorOnce("Couldn't retrieve proper graphic data for " + this.def + ".", 764532);
                return BaseContent.BadGraphic;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.compostCount, "compostCount", 0, false);
            Scribe_Values.Look<float>(ref this.progressInt, "progress", 0, false);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!this.Empty)
            {
                this.Progress = Mathf.Min(this.Progress + 250f * this.ProgressPerTickAtCurrentTemp, 1f);
            }
        }

        public void AddCompost(int count, Thing addedCompost)
        {
            base.GetComp<CompTemperatureRuinable>().Reset();
            if (this.Fermented)
            {
                Log.Warning("Tried to add compost to a bin with fermented compost in it. " +
                    "Colonists should harvest the bin first.");
                return;
            }
            int numToAdd = Mathf.Min(count, this.SpaceLeftForCompost);
            if (numToAdd <= 0)
            {
                return;
            }
            float addedFermenterCompProgress = ((ThingWithComps)addedCompost).GetComp<AC_CompFermenter>().fermentProgress;
            this.Progress = GenMath.WeightedAverage(this.Progress, (float)this.compostCount, addedFermenterCompProgress, (float)count);
            this.compostCount += numToAdd;
            base.Map.mapDrawer.MapMeshDirty(base.Position, MapMeshFlag.Things);
        }

        protected override void ReceiveCompSignal(string signal)
        {
            if (signal == "RuinedByTemperature")
            {
                this.Reset();
            }
        }

        private void Reset()
        {
            Log.Message($"{ThingID} reset.");
            this.compostCount = 0;
            this.Progress = 0f;
            fermentFlag = false;
            base.Map.mapDrawer.MapMeshDirty(base.Position, MapMeshFlag.Things);
        }

        public void AddCompost(Thing compost)
        {
            int numToAdd = Mathf.Min(compost.stackCount, this.SpaceLeftForCompost);
            if (numToAdd > 0)
            {
                this.AddCompost(numToAdd, compost);
                compost.SplitOff(numToAdd).Destroy(DestroyMode.Vanish);
            }
        }

        public override string GetInspectString()
        {
            CompProperties_TemperatureRuinable compProperties =
                this.def.GetCompProperties<CompProperties_TemperatureRuinable>();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (stringBuilder.Length != 0)
            {
                stringBuilder.AppendLine();
            }
            CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
            if (!this.Empty && !comp.Ruined)
            {
                if (this.Fermented)
                {
                    stringBuilder.AppendLine(string.Concat(new string[]
                        {
                        "AC.ContainsFermentedCompost".Translate(),
                        this.compostCount.ToString(),
                        " / ",
                        Building_AC_CompostBin.Capacity.ToString()
                        }));
                }
                else
                {
                    stringBuilder.AppendLine(string.Concat(new string[]
                        {
                        "AC.ContainsRawCompost".Translate(),
                        this.compostCount.ToString(),
                        " / ",
                        Building_AC_CompostBin.Capacity.ToString()
                        }));
                }
            }
            if (!this.Empty)
            {
                if (this.Fermented)
                {
                    stringBuilder.AppendLine("AC.Fermented".Translate());
                }
                else
                {
                    stringBuilder.AppendLine(string.Concat(new string[]
                        {
                        "AC.FermentationProgress".Translate(),
                        this.Progress.ToStringPercent(),
                        "AC.CompletesIn".Translate(),
                        this.EstimatedTicksLeft.ToStringTicksToPeriod(true, false, true)
                        }));
                    if (this.CurrentTempProgressSpeedFactor != 1f)
                    {
                        stringBuilder.AppendLine(string.Concat(new string[]
                            {
                            "AC.OutOfTemp".Translate(),
                            this.CurrentTempProgressSpeedFactor.ToStringPercent()
                            }));
                    }
                }
                if (comp.Ruined)
                {
                }
            }
            stringBuilder.AppendLine("AC.Temp".Translate() + ": " +
                base.AmbientTemperature.ToStringTemperature("F0"));
            stringBuilder.AppendLine(string.Concat(new string[]
            {
                "AC.IdealTemp".Translate(),
                ": ",
                Building_AC_CompostBin.minFermentTemperature.ToStringTemperature("F0"),
                "-",
                Building_AC_CompostBin.maxFermentTemperature.ToStringTemperature("F0")
            }));
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public Thing TakeOutCompost()
        {
            if (!this.Fermented)
            {
                Log.Warning("AC.NotReady".Translate());
                return null;
            }
            Thing thing = ThingMaker.MakeThing(ThingDefOf.AC_FermentedCompost, null);
            thing.stackCount = this.compostCount;
            this.Reset();
            return thing;
        }

        [DebuggerHidden]
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }
            if (Prefs.DevMode && !this.Empty)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Debug: Set progress to 1",
                    action = delegate ()
                    {
                        this.Progress = 1f;
                    }
                };
            }
            yield break;
        }

        public override void Draw()
        {
            if (this.Fermented && !this.fermentFlag)
            {
                this.fermentFlag = true;
                base.Map.mapDrawer.MapMeshDirty(base.Position, MapMeshFlag.Things);
            }
            base.Draw();
            Vector3 drawPos = this.DrawPos;
            drawPos.y += 0.1f;
            drawPos.z -= 0.4f;
            if (!this.Empty)
            {
                GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
                {
                    center = drawPos,
                    size = Building_AC_CompostBin.BarSize,
                    fillPercent = (float)this.compostCount / 25f,
                    filledMat = this.BarFilledMat,
                    unfilledMat = Building_AC_CompostBin.BarUnfilledMat,
                    margin = 0.1f,
                    rotation = Rot4.North
                });
            }
        }
    }

    public class WorkGiver_FillCompostBin : WorkGiver_Scanner
    {
        private static string TemperatureTrans = "AC.BadTemp".Translate();
        private static string NoCompostTrans = "AC.NoRawCompost".Translate();

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForDef(ThingDefOf.AC_CompostBin);
            }
        }

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AC_CompostBin CompostBin = t as Building_AC_CompostBin;
            if (CompostBin == null ||
                CompostBin.Fermented ||
                CompostBin.SpaceLeftForCompost <= 0)
            {
                return false;
            }
            float temperature = CompostBin.Position.GetTemperature(CompostBin.Map);
            CompProperties_TemperatureRuinable compProperties = CompostBin.def.GetCompProperties<CompProperties_TemperatureRuinable>();
            if (temperature < compProperties.minSafeTemperature || temperature > compProperties.maxSafeTemperature)
            {
                JobFailReason.Is(WorkGiver_FillCompostBin.TemperatureTrans);
                return false;
            }
            if (t.IsForbidden(pawn) ||
                !pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1))
            {
                return false;
            }
            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }
            if (this.FindCompost(pawn, CompostBin) == null)
            {
                JobFailReason.Is(WorkGiver_FillCompostBin.NoCompostTrans);
                return false;
            }
            return !t.IsBurning();
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AC_CompostBin CompostBin = t as Building_AC_CompostBin;
            Thing t2 = this.FindCompost(pawn, CompostBin);
            return new Job(JobDefOf.AC_FillCompostBin, t, t2)
            {
                count = CompostBin.SpaceLeftForCompost
            };
        }

        private Thing FindCompost(Pawn pawn, Building_AC_CompostBin bin)
        {
            Predicate<Thing> predicate = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1);
            Predicate<Thing> validator = predicate;
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(ThingDefOf.AC_RawCompost),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false),
                9999f,
                validator,
                null,
                0,
                -1,
                false);
        }
    }

    public class WorkGiver_EmptyCompostBin : WorkGiver_Scanner
    {

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForDef(ThingDefOf.AC_CompostBin);
            }
        }

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AC_CompostBin CompostBin = t as Building_AC_CompostBin;
            return CompostBin != null &&
                CompostBin.Fermented &&
                !t.IsBurning() &&
                !t.IsForbidden(pawn) &&
                pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return new Job(JobDefOf.AC_EmptyCompostBin, t);
        }
    }

    public class JobDriver_FillCompostBin : JobDriver
    {
        protected Building_AC_CompostBin CompostBin
        {
            get
            {
                return (Building_AC_CompostBin)this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        protected Thing RawCompost
        {
            get
            {
                return this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        public override bool TryMakePreToilReservations()
        {
            return this.pawn.Reserve(this.CompostBin, this.job, 1, -1, null) &&
                this.pawn.Reserve(this.RawCompost, this.job, 1, -1, null);
        }

        [DebuggerHidden]
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            base.AddEndCondition(() => (this.CompostBin.SpaceLeftForCompost > 0) ? JobCondition.Ongoing : JobCondition.Succeeded);
            yield return Toils_General.DoAtomic(delegate {
                this.job.count = this.CompostBin.SpaceLeftForCompost;
            });
            Toil reserveRawCompost = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
            yield return reserveRawCompost;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveRawCompost, TargetIndex.B, TargetIndex.None, true, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return new Toil
            {
                initAction = delegate {
                    this.CompostBin.AddCompost(this.RawCompost);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }

    public class JobDriver_EmptyCompostBin : JobDriver
    {
        protected Building_AC_CompostBin CompostBin
        {
            get
            {
                return (Building_AC_CompostBin)this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        protected Thing FermentedCompost
        {
            get
            {
                return this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        public override bool TryMakePreToilReservations()
        {
            return this.pawn.Reserve(this.CompostBin, this.job, 1, -1, null);
        }

        [DebuggerHidden]
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).FailOn(() => !this.CompostBin.Fermented).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return new Toil
            {
                initAction = delegate {
                    Thing thing = this.CompostBin.TakeOutCompost();
                    GenPlace.TryPlaceThing(thing, this.pawn.Position, this.Map, ThingPlaceMode.Near, null);
                    StoragePriority currentPriority = HaulAIUtility.StoragePriorityAtFor(thing.Position, thing);
                    IntVec3 c;
                    if (StoreUtility.TryFindBestBetterStoreCellFor(thing, this.pawn, this.Map, currentPriority, this.pawn.Faction, out c, true))
                    {
                        this.job.SetTarget(TargetIndex.C, c);
                        this.job.SetTarget(TargetIndex.B, thing);
                        this.job.count = thing.stackCount;
                    }
                    else
                    {
                        this.EndJobWith(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
            yield return Toils_Reserve.Reserve(TargetIndex.C, 1, -1, null);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false);
            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
            yield return carryToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, carryToCell, true);
        }
    }
}
