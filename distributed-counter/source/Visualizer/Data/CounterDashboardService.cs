using ChartJs.Blazor.ChartJS.Common.Enums;
using ChartJs.Blazor.ChartJS.Common.Wrappers;
using ChartJs.Blazor.ChartJS.LineChart;
using ChartJs.Blazor.ChartJS.PieChart;
using ChartJs.Blazor.Util;
using CosmosDistributedCounter;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DistributedCounterDashboard.Data
{
    public static class DashboardService
    {
        private static int timers;
        private static PrimaryCounter pc;
        public static Dictionary<string, CounterVisualization> DCounterVisualizationItems;
        private static DistributedCounterManagementService dc_mgmtService;


        private static string[] colors = new string[] {"MediumVioletRed", "Green","Orange",
        "DodgerBlue","LightSteelBlue","DarkMagenta","Fuchsia","MediumOrchid",
        "Chocolate","BlanchedAlmond", "Cornsilk", "DarkGreen", "IndianRed","DarkOliveGreen",
        "ForestGreen","MediumAquamarine","GreenYellow", "CadelBlue", "Cyan","MediumBlue","LightPink", "MediumVioletRed", "Green","Orange",
        "MediumVioletRed", "Green","Orange","Black"};


        public static async Task Init(string parentCounterId, DistributedCounterManagementService dcms)
        {
            dc_mgmtService = dcms;
            pc = await dc_mgmtService.GetPrimaryCounterAsync(parentCounterId);

            DCounterVisualizationItems = new Dictionary<string, CounterVisualization>();
            List<DistributedCounter> dcList = await dc_mgmtService.GetDistributedCountersAsync(pc);

            foreach (DistributedCounter d in dcList) 
            {
                string colorName = colors[DCounterVisualizationItems.Count % colors.Count()];
                CounterVisualization dc = new CounterVisualization(colorName, 0, d.Value);
                DCounterVisualizationItems.Add(d.Id, dc);

            }

        }
      
        public static async Task UpdateCounterValues(int elapsedSeconds)
        {           
            if (DCounterVisualizationItems == null) { return;}

            //Get all DC  from DB, loop and set CounterVisualization value for chart
            List<DistributedCounter> dcList = await dc_mgmtService.GetDistributedCountersAsync(pc,false);

            foreach (var dc in dcList)
            {
                CounterVisualization dcVisObj;
                if (DCounterVisualizationItems.ContainsKey(dc.Id))
                {
                    dcVisObj = DCounterVisualizationItems[dc.Id]; //search  in list                    
                }
                else
                {
                    string colorName = colors[DCounterVisualizationItems.Count % colors.Count()];
                    dcVisObj = new CounterVisualization(colorName,elapsedSeconds, dc.Value);
                    DCounterVisualizationItems.Add(dc.Id, dcVisObj);
                }

                if (dc.Status == CounterStatus.deleted)
                {
                    dcVisObj.Terminated = true;
                }
               
                if (!dcVisObj.Terminated)
                {
                    long val = dc.Value;  //set dc value               
                    dcVisObj.AddValueThisSec(elapsedSeconds, val);
                }
                else
                {
                    dcVisObj.AddValueThisSec(elapsedSeconds, 0);
                }
            }
           
        }

        public static async Task CreateDCounterMangementObjects(int elapsedSeconds, int countersToSplit)
        {

            await dc_mgmtService.SplitDistributedCountersAsync(pc, countersToSplit);
            await UpdateCounterValues(elapsedSeconds);

        }

        public static async Task RemoveDCounterMangementObjects(int elapsedSeconds, int countersToMerge)
        {
            await dc_mgmtService.MergeDistributedCountersAsync(pc, countersToMerge);
            await UpdateCounterValues(elapsedSeconds);
        }

    }

    public class CounterVisualization
    {

        public List<long> Values { get; set; }

        public string Name { get; set; }

        public bool Terminated { get; set; }

        public CounterVisualization( string name, int CurrentSec, long initVal)
        {
            Values = new List<long>();
            Name= name;

            int max = 0;

            if (CurrentSec > 119)
                max = 119;
            else
                max = CurrentSec;

            for (int i = 1; i < max; i++)//Fill 0 till Current time
            {
                Values.Add(0);
            }
            Values.Add(initVal);
        }
    
        public void AddValueThisSec(int CurrentSec, long Value)
        {

            if (this.Terminated)
            {
                Values.Add(0);
            }
            else
            {
                Values.Add(Value);
            } 
            if (this.Values.Count > 120)
            { 
                this.Values.RemoveAt(0);
            }
        }
    }
}
