using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using UnityEngine;
using System.Reflection;
using Verse;
using Harmony;
using Verse.AI;
using RimWorld;

namespace Advanced_Cultivation
{
    [StaticConstructorOnStartup]
    internal static class AC_Initializer
    {
        static AC_Initializer()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("net.gasch.advanced_cultivation.tilling");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [StaticConstructorOnStartup]
    public static class AC_TexLoader
    {
        public static readonly Texture2D Plow = ContentFinder<Texture2D>.Get("Plow", true);
    }

    public class TillingDict : MapComponent
    {
        public Dictionary<string, bool> TillingLabelDict;
        public Dictionary<Zone_Growing, bool> TillingZoneDict;

        public TillingDict(Map map) : base(map)
        {
        }
    }

    [HarmonyPatch(typeof(Zone_Growing))]
    [HarmonyPatch(new Type[] { })]
    public static class Zone_Growing_Patch
    {
        static void Postfix()
        {
            
        }
    }

    [HarmonyPatch(typeof(Zone_Growing))]
    [HarmonyPatch("GetGizmos")]
    public static class Zone_Growing_GetGizmos_Patch
    {
        static void Postfix(IEnumerable<Gizmo> __result, Zone_Growing __instance)
        {
            Map activeMap = __instance.Map;
            bool isActive = activeMap.components.TillingDict;
            Command_Toggle TillToggle = new Command_Toggle
            {
                defaultLabel = "AC.CommandAllowTill".Translate(),
                defaultDesc = "AC.CommandAllowTillDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc1,
                icon = AC_TexLoader.Plow,
                isActive = (() => ),
                toggleAction = delegate ()
                {
                    this.allowTill = !this.allowTill;
                }
            };
            return 
        }
    }

}