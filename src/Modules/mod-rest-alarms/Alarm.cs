// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Newtonsoft.Json;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TrakHound.Api.v2;
using TrakHound.Api.v2.Data;

namespace mod_rest_alarms
{
    class Alarm
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("data_item_id")]
        public string DataItemId { get; set; }

        [JsonProperty("condition")]
        public string Condition { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }


        public Alarm(Sample sample)
        {
            Id = GenerateId(sample.Id, sample.Timestamp);
            DataItemId = sample.Id;
            Condition = sample.Condition;
            Message = sample.CDATA;
            Timestamp = sample.Timestamp;
        }

        public static string GenerateId(string dataItemId, DateTime timestamp)
        {
            // Create Identifier input
            string s = string.Format("{0}|{1}", dataItemId, timestamp.ToUnixTime());
            s = Uri.EscapeDataString(s);

            // Create Hash
            var b = Encoding.UTF8.GetBytes(s);
            var h = SHA1.Create();
            b = h.ComputeHash(b);
            var l = b.ToList();
            l.Reverse();
            b = l.ToArray();

            // Convert to Base64 string
            s = Convert.ToBase64String(b);

            // Remove non alphanumeric characters
            var regex = new Regex("[^a-zA-Z0-9 -]");
            s = regex.Replace(s, "");
            s = s.ToUpper();

            return s;
        }
    }
}
