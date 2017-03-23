// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrakHound.Api.v2;
using TrakHound.Api.v2.Data;
using TrakHound.Api.v2.Events;


namespace mod_rest_oee
{
    class Availability
    {
        private const string EVENT_NAME = "Status";
        private const string EVENT_VALUE = "Active";

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

        public static Availability Get(RequestQuery query, List<DataItemDefinition> dataItems, List<ComponentDefinition> components)
        {
            var e = GetEvent(EVENT_NAME);
            if (e != null)
            {
                List<Sample> samples = null;

                if (!dataItems.IsNullOrEmpty())
                {
                    var ids = GetEventIds(e, dataItems, components);
                    if (!ids.IsNullOrEmpty())
                    {
                        // Get Samples
                        samples = Database.ReadSamples(ids.ToArray(), query.DeviceId, query.From, query.To, DateTime.MinValue, 0);
                    }
                }

                if (!samples.IsNullOrEmpty())
                {
                    // Get the initial timestamp
                    DateTime timestamp;
                    if (query.From > DateTime.MinValue) timestamp = query.From;
                    else timestamp = samples.Select(o => o.Timestamp).OrderByDescending(o => o).First();

                    // Create a list of DataItemInfos (DataItems with Parent Component info)
                    var dataItemInfos = DataItemInfo.CreateList(dataItems, components);

                    var instanceSamples = samples.FindAll(o => o.Timestamp <= timestamp);

                    double operatingTime = 0;
                    bool addPrevious = false;
                    string previousEvent = null;

                    var events = new List<AvailabilityEvent>();

                    // Calculate the Operating Time
                    var timestamps = samples.FindAll(o => o.Timestamp >= timestamp).OrderBy(o => o.Timestamp).Select(o => o.Timestamp).Distinct().ToList();
                    if (!timestamps.IsNullOrEmpty())
                    {
                        for (int i = 0; i < timestamps.Count; i++)
                        {
                            var time = timestamps[i];

                            // Update CurrentSamples
                            foreach (var sample in samples.FindAll(o => o.Timestamp == time))
                            {
                                int j = instanceSamples.FindIndex(o => o.Id == sample.Id);
                                if (j >= 0) instanceSamples[j] = sample;
                                else instanceSamples.Add(sample);
                            }

                            // Create a list of SampleInfo objects with DataItem information contained
                            var infos = SampleInfo.Create(dataItemInfos, instanceSamples);

                            // Evaluate the Event and get the Response
                            var response = e.Evaluate(infos);
                            if (response != null)
                            {
                                if (addPrevious && i > 0)
                                {
                                    var previousTime = timestamps[i - 1] < query.From ? query.From : timestamps[i - 1];
                                    double seconds = (time - previousTime).TotalSeconds;
                                    events.Add(new AvailabilityEvent(previousEvent, previousTime, time));
                                    operatingTime += seconds;
                                }

                                addPrevious = response.Value == EVENT_VALUE;
                                previousEvent = response.Value;
                            }
                        }

                        var toTimestamp = query.To > DateTime.MinValue ? query.To : DateTime.UtcNow;

                        if (addPrevious)
                        {
                            operatingTime += (toTimestamp - timestamps[timestamps.Count - 1]).TotalSeconds;
                            events.Add(new AvailabilityEvent(previousEvent, timestamps[timestamps.Count - 1], toTimestamp));
                        }

                        // Calculate the TotalTime that is being evaluated
                        var totalTime = (toTimestamp - timestamps[0]).TotalSeconds;

                        var availability = new Availability(operatingTime, totalTime, events);
                        if (query.Details) availability.Events = availability._events;

                        return availability;
                    }
                }
            }

            return null;
        }
        
        private static Event GetEvent(string eventName)
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EventsConfiguration.FILENAME);

            // Read the EventsConfiguration file
            var config = EventsConfiguration.Get(configPath);
            if (config != null)
            {
                var e = config.Events.Find(o => o.Name.ToLower() == eventName.ToLower());
                if (e != null) return e;
            }

            return null;
        }

        private static string[] GetEventIds(Event e, List<DataItemDefinition> dataItems, List<ComponentDefinition> components)
        {
            var ids = new List<string>();

            foreach (var response in e.Responses)
            {
                foreach (var trigger in response.Triggers.OfType<Trigger>())
                {
                    foreach (var id in GetFilterIds(trigger.Filter, dataItems, components))
                    {
                        if (!ids.Exists(o => o == id)) ids.Add(id);
                    }
                }
            }

            return ids.ToArray();
        }

        private static string[] GetFilterIds(string filter, List<DataItemDefinition> dataItems, List<ComponentDefinition> components)
        {
            var ids = new List<string>();

            foreach (var dataItem in dataItems)
            {
                var dataFilter = new DataFilter(filter, dataItem, components);
                if (dataFilter.IsMatch() && !ids.Exists(o => o == dataItem.Id))
                {
                    ids.Add(dataItem.Id);
                }
            }

            return ids.ToArray();
        }

    }
}
