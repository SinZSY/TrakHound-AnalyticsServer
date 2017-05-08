// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using TrakHound.Api.v2;
using TrakHound.Api.v2.Data;
using TrakHound.Api.v2.Events;

namespace mod_rest_oee
{
    class Availability
    {
        public const string EVENT_NAME = "Status";
        public const string EVENT_VALUE = "Active";

        private static Logger log = LogManager.GetCurrentClassLogger();

        [JsonProperty("operating_time")]
        public double OperatingTime { get; set; }

        [JsonProperty("planned_production_time")]
        public double PlannedProductionTime { get; set; }

        [JsonProperty("value")]
        public double Value
        {
            get
            {
                if (PlannedProductionTime > 0) return Math.Round(OperatingTime / PlannedProductionTime, 5);
                return 0;
            }
        }

        internal List<AvailabilityEvent> _events;
        [JsonProperty("events")]
        public List<AvailabilityEvent> Events { get; set; }


        public Availability(double operatingTime, double plannedProductionTime, List<AvailabilityEvent> events)
        {
            OperatingTime = Math.Round(operatingTime, 3);
            PlannedProductionTime = Math.Round(plannedProductionTime, 3); ;

            if (!events.IsNullOrEmpty()) _events = events;
        }

        public static Availability Get(DateTime from, DateTime to, Event e, List<Sample> samples, List<DataItemInfo> dataItemInfos, bool details)
        {
            if (!samples.IsNullOrEmpty())
            {
                // Get the initial timestamp
                DateTime timestamp;
                if (from > DateTime.MinValue) timestamp = from;
                else timestamp = samples.Select(o => o.Timestamp).OrderByDescending(o => o).First();

                var instanceSamples = samples.FindAll(o => o.Timestamp <= timestamp);

                double operatingTime = 0;
                bool addPrevious = false;
                string previousEvent = null;

                var events = new List<AvailabilityEvent>();

                // Get Distinct Timestamps
                var timestamps = new List<DateTime>();
                timestamps.Add(from);
                timestamps.AddRange(samples.FindAll(o => o.Timestamp >= from).Select(o => o.Timestamp).Distinct().ToList());
                timestamps.Add(to);
                timestamps.Sort();

                var filteredSamples = samples.ToList();

                // Calculate the Operating Time
                for (int i = 0; i < timestamps.Count; i++)
                {
                    var time = timestamps[i];

                    // Update CurrentSamples
                    foreach (var sample in filteredSamples.FindAll(o => o.Timestamp == time))
                    {
                        int j = instanceSamples.FindIndex(o => o.Id == sample.Id);
                        if (j >= 0) instanceSamples[j] = sample;
                        else instanceSamples.Add(sample);
                    }

                    //Create a list of SampleInfo objects with DataItem information contained
                    var infos = SampleInfo.Create(dataItemInfos, instanceSamples);

                    // Evaluate the Event and get the Response
                    var response = e.Evaluate(infos);
                    if (response != null)
                    {
                        if (addPrevious && i > 0)
                        {
                            var previousTime = timestamps[i - 1] < from ? from : timestamps[i - 1];
                            double seconds = (time - previousTime).TotalSeconds;
                            events.Add(new AvailabilityEvent(previousEvent, previousTime, time));
                            operatingTime += seconds;
                        }

                        addPrevious = response.Value == EVENT_VALUE;
                        previousEvent = response.Value;
                    }
                }

                var toTimestamp = to > DateTime.MinValue ? to : DateTime.UtcNow;

                if (addPrevious)
                {
                    var eventDuration = (toTimestamp - timestamps[timestamps.Count - 1]).TotalSeconds;
                    if (eventDuration > 0)
                    {
                        operatingTime += eventDuration;
                        events.Add(new AvailabilityEvent(previousEvent, timestamps[timestamps.Count - 1], toTimestamp));
                    }
                }

                // Calculate the TotalTime that is being evaluated
                var totalTime = (toTimestamp - timestamps[0]).TotalSeconds;

                var availability = new Availability(operatingTime, totalTime, events);
                if (details) availability.Events = availability._events;

                return availability;
            }

            return null;
        }
    }
}
