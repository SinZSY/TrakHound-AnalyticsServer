// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Newtonsoft.Json;
using System;

namespace mod_rest_oee
{
    class OverrideEvent
    {
        [JsonProperty("feedrate_override")]
        public double FeedrateOverride { get; set; }

        [JsonProperty("start")]
        public DateTime Start { get; set; }

        [JsonProperty("stop")]
        public DateTime Stop { get; set; }

        [JsonProperty("duration")]
        public double Duration
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


        public OverrideEvent(double feedrateOverride, DateTime start, DateTime stop)
        {
            FeedrateOverride = feedrateOverride;
            Start = start;
            Stop = stop;
        }
    }
}
