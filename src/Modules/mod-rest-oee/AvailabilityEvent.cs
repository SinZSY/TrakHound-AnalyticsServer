// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Newtonsoft.Json;
using System;

namespace mod_rest_oee
{
    class AvailabilityEvent
    {
        [JsonProperty("event")]
        public string Event { get; set; }

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


        public AvailabilityEvent(string e, DateTime start, DateTime stop)
        {
            Event = e;
            Start = start;
            Stop = stop;
        }
    }
}
