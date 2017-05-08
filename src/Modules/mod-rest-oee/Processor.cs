// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;
using TrakHound.Api.v2;
using TrakHound.Api.v2.Data;
using TrakHound.Api.v2.Events;
using System.IO;

namespace mod_rest_oee
{
    class Processor
    {
        private static EventsConfiguration v12EventsConfiguration;
        private static EventsConfiguration v13EventsConfiguration;


        public RequestQuery RequestQuery { get; set; }

        public List<DataItemDefinition> DataItems { get; set; }

        public List<ComponentDefinition> Components { get; set; }

        public string AgentVersion { get; set; }

        private Event availabilityEvent;
        private Event qualityEvent;
        private string[] availabilityIds;

        private DateTime from;
        private DateTime to;
        private DateTime next;

        private List<Sample> instanceSamples = new List<Sample>();
        private List<DataItemInfo> dataItemInfos;


        public Processor(RequestQuery query, List<DataItemDefinition> dataItems, List<ComponentDefinition> components, string agentVersion)
        {
            RequestQuery = query;
            DataItems = dataItems;
            Components = components;
            AgentVersion = agentVersion;
        }

        private List<Sample> GetSamples()
        {
            if (!DataItems.IsNullOrEmpty() && !Components.IsNullOrEmpty())
            {
                var ids = new List<string>();

                // Read the Availability Event
                availabilityEvent = GetEvent(Availability.EVENT_NAME, AgentVersion);
                if (availabilityEvent != null)
                {
                    availabilityIds = GetEventIds(availabilityEvent, DataItems, Components);
                    foreach (var id in availabilityIds)
                    {
                        if (!ids.Exists(o => o == id)) ids.Add(id);
                    }
                }

                // Find all of the Overrides by DataItemId (used for Performance)
                var overrideItems = DataItems.FindAll(o => o.Type == "PATH_FEEDRATE_OVERRIDE" || (o.Type == "PATH_FEEDRATE" && o.Units == "PERCENT"));
                foreach (var id in overrideItems.Select(o => o.Id).ToArray())
                {
                    if (!ids.Exists(o => o == id)) ids.Add(id);
                }

                // Read the Quality Event
                qualityEvent = GetEvent(Quality.EVENT_NAME, AgentVersion);
                if (qualityEvent != null)
                {
                    // Program Name DataItem
                    var programNameItem = DataItems.Find(o => o.Type == "PROGRAM");
                    if (programNameItem != null) if (!ids.Exists(o => o == programNameItem.Id)) ids.Add(programNameItem.Id);

                    // Execution DataItem
                    var executionItem = DataItems.Find(o => o.Type == "EXECUTION");
                    if (executionItem != null) if (!ids.Exists(o => o == executionItem.Id)) ids.Add(executionItem.Id);

                    foreach (var id in GetEventIds(qualityEvent, DataItems, Components))
                    {
                        if (!ids.Exists(o => o == id)) ids.Add(id);
                    }
                }

                if (!ids.IsNullOrEmpty())
                {
                    // Get Samples
                    return Database.ReadSamples(ids.ToArray(), RequestQuery.DeviceId, from, to, DateTime.MinValue, long.MaxValue);
                }
            }

            return null;
        }


        public List<Oee> Run()
        {
            // Set timestamps
            from = RequestQuery.From;
            to = RequestQuery.To;
            if (RequestQuery.To == DateTime.MinValue) to = DateTime.UtcNow;

            // Set the Next timestamp (for Incremental processing)
            next = to;
            if (RequestQuery.Increment > 0) next = from.AddSeconds(RequestQuery.Increment);
            if (next > to) next = to;

            // Create a list of DataItemInfos (DataItems with Parent Component info)
            dataItemInfos = DataItemInfo.CreateList(DataItems, Components);

            var samples = GetSamples();
            if (!samples.IsNullOrEmpty())
            {
                // Get a list of distinct ids retrieved from Database
                var sampleIds = samples.Select(o => o.Id).Distinct().ToList();

                var instanceSamples = new List<Sample>();
                var oees = new List<Oee>();

                int i = 0;

                do
                {
                    i++;

                    // Update Instance Samples
                    UpdateInstanceSamples(sampleIds, samples);

                    // Create and Add a new OEE object to list
                    oees.Add(CreateOee(samples));

                    // Increment time
                    if (next == to) break;
                    from = next;
                    next = next.AddSeconds(RequestQuery.Increment);
                    if (next > to) next = to;

                } while (next <= to);

                // Get list of Part IDs
                var partIds = new List<string>();
                foreach (var oee in oees) partIds.AddRange(oee.Parts.Select(o => o.Id));
                var verifiedParts = Database.ReadVerifiedParts(RequestQuery.DeviceId, partIds.ToArray(), DateTime.MinValue, DateTime.MinValue, DateTime.MinValue);

                // Add Quality Component
                foreach (var oee in oees)
                {
                    if (!oee.Parts.IsNullOrEmpty())
                    {
                        int goodParts = 0;

                        if (!verifiedParts.IsNullOrEmpty())
                        {
                            foreach (var part in verifiedParts)
                            {
                                if (oee.Parts.Exists(o => o.Id == part.PartId)) goodParts++;
                            }
                        }

                        oee.Quality = new Quality(goodParts, oee.Parts.Count);
                    }
                }

                return oees;
            }

            return null;
        }

        private Oee CreateOee(List<Sample> samples)
        {
            var oee = new Oee();
            oee.From = from;
            oee.To = next;

            // Create a list of samples to process
            var currentSamples = new List<Sample>();
            currentSamples.AddRange(instanceSamples);
            currentSamples.AddRange(samples.FindAll(o => o.Timestamp > from && o.Timestamp < next));

            // Get Availability
            var availability = Availability.Get(from, next, availabilityEvent, currentSamples, dataItemInfos, RequestQuery.Details);
            if (availability != null)
            {
                oee.Availability = availability;

                // Get Performance
                var performance = Performance.Get(currentSamples, from, next, availability._events, RequestQuery.Details);
                if (performance != null)
                {
                    oee.Performance = performance;
                }

                // Get Parts list for Quality 
                oee.Parts = Quality.GetParts(from, next, qualityEvent, currentSamples, dataItemInfos);
            }

            return oee;
        }

        private void UpdateInstanceSamples(List<string> sampleIds, List<Sample> samples)
        {
            foreach (var id in sampleIds)
            {
                var latestSamples = samples.FindAll(o => o.Id == id && o.Timestamp <= next);
                if (!latestSamples.IsNullOrEmpty())
                {
                    var latestSample = latestSamples.OrderBy(o => o.Timestamp).First();
                    if (latestSample != null)
                    {
                        var index = instanceSamples.FindIndex(o => o.Id == id);
                        if (index >= 0) instanceSamples.RemoveAt(index);
                        instanceSamples.Add(latestSample);
                    }
                }
            }
        }

        private static Event GetEvent(string eventName, string agentVersion)
        {
            var version = new Version(agentVersion);
            var version13 = new Version("1.3.0");
            var version12 = new Version("1.2.0");
            var version11 = new Version("1.1.0");
            var version10 = new Version("1.0.0");

            var config = v12EventsConfiguration;
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "v12events.config");

            if (version >= version13)
            {
                config = v13EventsConfiguration;
                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "v13events.config");
            }

            // Read the EventsConfiguration file
            if (config == null) config = EventsConfiguration.Get(configPath);
            if (config != null)
            {
                if (version >= version13) v13EventsConfiguration = config;
                else v12EventsConfiguration = config;

                if (!string.IsNullOrEmpty(eventName)) return config.Events.Find(o => o.Name.ToLower() == eventName.ToLower());
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
