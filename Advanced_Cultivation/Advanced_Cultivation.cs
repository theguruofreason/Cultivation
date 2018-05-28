using UnityEngine;
using Verse;
using RimWorld;

namespace Advanced_Cultivation
{
    [DefOf]
    public static class ThingDefOf
    {
        public static ThingDef AC_RawCompost;
        public static ThingDef AC_FermentedCompost;
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
    
    public class AC_CompFermenter : ThingComp
    {
        private float fermentProgress;

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

        public void Ferment()
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
}
