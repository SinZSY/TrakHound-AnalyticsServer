// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Newtonsoft.Json;
using System;

namespace mod_rest_oee
{
    class PerformanceEvent
    {
        [JsonProperty("feedrate_override")]
        public double FeedrateOverride { get; set; }

        [JsonProperty("start")]
        public DateTime Start { get; set; }

        [JsonProperty("stop")]
        public DateTime Stop { get; set; }

        [JsonProperty("total_time")]
        public double TotalTime
        {
            get
            {
                if (Start > DateTime.MinValue && Stop > DateTime.MinValue)
                {
                    return Math.Round((Stop - Start).TotalSeconds, 3);
                }
                else return 0;
            }
        }

        [JsonProperty("operating_time")]
        public double OperatingTime { get; set; }

        [JsonProperty("ideal_operating_time")]
        public double IdealOperatingTime { get; set; }


        public PerformanceEvent(double feedrateOverride, double operatingTime, double idealOperatingTime, DateTime start, DateTime stop)
        {
            FeedrateOverride = Math.Round(feedrateOverride, 4);
            OperatingTime = Math.Round(operatingTime, 3);
            IdealOperatingTime = Math.Round(idealOperatingTime, 3);
            Start = start;
            Stop = stop;
        }
    }
}
