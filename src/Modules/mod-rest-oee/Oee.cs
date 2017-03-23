// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace mod_rest_oee
{
    class Oee
    {
        [JsonProperty("oee")]
        public double Value
        {
            get
            {
                return Math.Round(Availability.Value * Performance.Value, 5);
            }
        }

        [JsonProperty("availability")]
        public Availability Availability { get; set; }

        [JsonProperty("performance")]
        public Performance Performance { get; set; }

        //[JsonProperty("quality")]
        //public Quality Quality { get; set; }
    }
}
