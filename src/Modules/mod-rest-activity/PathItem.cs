// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace mod_rest_activity
{
    public class PathItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("events")]
        public List<EventItem> Events { get; set; }


        public PathItem()
        {
            Events = new List<EventItem>();
        }
    }
}
