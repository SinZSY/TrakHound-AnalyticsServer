// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace mod_rest_oee
{
    class Oee
    {
        [JsonProperty("oee")]
        public double Value
        {
            get
            {
                if (Quality != null && Performance != null) return Math.Round(Availability.Value * Performance.Value * Quality.Value, 5);
                if (Performance != null) return Math.Round(Availability.Value * Performance.Value, 5);
                else if (Availability != null) return Math.Round(Availability.Value, 5);

                return 0;
            }
        }

        [JsonProperty("from")]
        public DateTime From { get; set; }

        [JsonProperty("to")]
        public DateTime To { get; set; }

        [JsonProperty("availability")]
        public Availability Availability { get; set; }

        [JsonProperty("performance")]
        public Performance Performance { get; set; }

        [JsonProperty("quality")]
        public Quality Quality { get; set; }

        [JsonIgnore]
        public List<Part> Parts { get; set; }
    }
}
