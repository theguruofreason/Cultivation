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
    public class TillingDict : MapComponent
    {
        public TillingDict(Map map) : base(map)
        {
            this.map = map;
        }

        public Dictionary<Zone_Growing, bool> zoneDictionary;
        public Dictionary<string, bool> labelDictionary;

        public override void MapComponentUpdate()
        {

        }


        public class WorkGiver_Till : WorkGiver_Scanner
        {
        }

        // public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        // {
        // }
    }
}
