// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using TrakHound.Api.v2;
using TrakHound.Api.v2.Data;
using TrakHound.Api.v2.Events;
using Json = TrakHound.Api.v2.Json;

namespace mod_rest_parts
{
    [InheritedExport(typeof(IRestModule))]
    public class Module : IRestModule
    {
        private static Logger log = LogManager.GetCurrentClassLogger();
        private static EventsConfiguration v12EventsConfiguration;
        private static EventsConfiguration v13EventsConfiguration;

        public string Name { get { return "Parts"; } }


        public bool GetResponse(Uri requestUri, Stream stream)
        {
            var query = new RequestQuery(requestUri);
            if (query.IsValid)
            {
                try
                {
                    List<ComponentDefinition> components = null;
                    List<DataItemDefinition> dataItems = null;

                    // Get Current Agent
                    var agent = Database.ReadAgent(query.DeviceId);
                    if (agent != null)
                    {
                        var e = GetEvent("Program Status", agent.Version);
                        if (e != null)
                        {
                            // Get Components
                            components = Database.ReadComponents(query.DeviceId, agent.InstanceId);

                            // Get Data Items
                            dataItems = Database.ReadDataItems(query.DeviceId, agent.InstanceId);

                            if (!dataItems.IsNullOrEmpty())
                            {
                                var ids = GetEventIds(e, dataItems, components);
                                if (!ids.IsNullOrEmpty())
                                {
                                    // Program Name DataItem
                                    var programNameItem = dataItems.Find(o => o.Type == "PROGRAM");
                                    if (programNameItem != null) ids.Add(programNameItem.Id);

                                    // Execution DataItem
                                    var executionItem = dataItems.Find(o => o.Type == "EXECUTION");
                                    if (executionItem != null) ids.Add(executionItem.Id);

                                    // Get Samples
                                    var samples = Database.ReadSamples(ids.ToArray(), query.DeviceId, query.From, query.To, DateTime.MinValue, 0);
                                    if (!samples.IsNullOrEmpty())
                                    {
                                        // Get the initial timestamp
                                        DateTime timestamp;
                                        if (query.From > DateTime.MinValue) timestamp = query.From;
                                        else timestamp = samples.Select(o => o.Timestamp).OrderByDescending(o => o).First();

                                        // Create a list of DataItemInfos (DataItems with Parent Component info)
                                        var dataItemInfos = DataItemInfo.CreateList(dataItems, components);

                                        // Get Path Components
                                        var paths = components.FindAll(o => o.Type == "Path");

                                        var currentSamples = samples.FindAll(o => o.Timestamp <= timestamp);

                                        if (programNameItem != null && executionItem != null)
                                        {
                                            // Previous variables
                                            DateTime previousTime = DateTime.MinValue;
                                            string previousValue = null;
                                            string previousProgramName = null;

                                            // Stored variables
                                            var parts = new List<Part>();
                                            Part part = null;

                                            // Get distinct timestamps
                                            var timestamps = samples.FindAll(o => o.Timestamp >= timestamp).OrderBy(o => o.Timestamp).Select(o => o.Timestamp).Distinct().ToList();
                                            for (int i = 0; i < timestamps.Count; i++)
                                            {
                                                var time = timestamps[i];

                                                // Update CurrentSamples
                                                foreach (var sample in samples.FindAll(o => o.Timestamp == time))
                                                {
                                                    int j = currentSamples.FindIndex(o => o.Id == sample.Id);
                                                    if (j >= 0) currentSamples[j] = sample;
                                                    else currentSamples.Add(sample);
                                                }


                                                // Program Name
                                                var programName = currentSamples.Find(o => o.Id == programNameItem.Id).CDATA;

                                                // Execution
                                                var execution = currentSamples.Find(o => o.Id == executionItem.Id).CDATA;

                                                // Create a list of SampleInfo objects with DataItem information contained
                                                var infos = SampleInfo.Create(dataItemInfos, currentSamples);

                                                // Evaluate the Event and get the Response
                                                var response = e.Evaluate(infos);
                                                if (response != null)
                                                {
                                                    if (part != null)
                                                    {
                                                        // Update the program stop time
                                                        part.Stop = time;

                                                        // Check if program changed
                                                        if (part != null && programName != previousProgramName)
                                                        {
                                                            part = null;
                                                            previousValue = null;
                                                        }
                                                    }


                                                    if (part == null && !string.IsNullOrEmpty(programName) && programName != "UNAVAILABLE" &&
                                                        response.Value != "Stopped" && response.Value != "Completed")
                                                    {
                                                        // Create a new Part object
                                                        part = new Part();
                                                        part.ProgramName = programName;
                                                        part.Start = time;
                                                    }


                                                    if (part != null)
                                                    {
                                                        if (response.Value != previousValue)
                                                        {
                                                            if (response.Value == "Stopped" || response.Value == "Completed")
                                                            {
                                                                parts.Add(part);
                                                                part = null;
                                                            }
                                                        }
                                                    }

                                                    previousValue = response.Value;
                                                    previousTime = time;
                                                }

                                                previousProgramName = programName;
                                            }

                                            var toTime = query.To > DateTime.MinValue ? query.To : DateTime.UtcNow;

                                            if (part != null)
                                            {
                                                part.Stop = toTime;
                                                parts.Add(part);
                                            }

                                            if (!parts.IsNullOrEmpty())
                                            {
                                                var partIds = parts.Select(o => o.Id).ToArray();

                                                // Get Rejected Parts
                                                var rejectedParts = Database.ReadRejectedParts(query.DeviceId, partIds, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue);       
                                                if (!rejectedParts.IsNullOrEmpty())
                                                {
                                                    foreach (var matchedPart in parts)
                                                    {
                                                        var rejectedPart = rejectedParts.Find(o => o.PartId == matchedPart.Id);
                                                        if (rejectedPart != null)
                                                        {
                                                            var rejection = new Rejection(rejectedPart.Message, rejectedPart.Timestamp);
                                                            matchedPart.Rejection = rejection;
                                                        }
                                                    }
                                                }

                                                // Get Verified Parts
                                                var verifiedParts = Database.ReadVerifiedParts(query.DeviceId, partIds, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue);
                                                if (!verifiedParts.IsNullOrEmpty())
                                                {
                                                    foreach (var matchedPart in parts)
                                                    {
                                                        var verifiedPart = verifiedParts.Find(o => o.PartId == matchedPart.Id);
                                                        if (verifiedPart != null)
                                                        {
                                                            var verification = new Verification(verifiedPart.Message, verifiedPart.Timestamp);
                                                            matchedPart.Verification = verification;
                                                        }
                                                    }
                                                }

                                                // Write JSON to stream
                                                string json = TrakHound.Api.v2.Json.Convert.ToJson(parts, true);
                                                var bytes = Encoding.UTF8.GetBytes(json);
                                                stream.Write(bytes, 0, bytes.Length);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Info("Parts Stream Closed");
                    log.Trace(ex);
                }

                return true;
            }

            return false;
        }

        public bool SendData(Uri requestUri, Stream stream)
        {
            var query = new SendQuery(requestUri);
            if (query.IsValid)
            {
                string json;
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    json = reader.ReadToEnd();
                }

                if (!string.IsNullOrEmpty(json))
                {
                    json = HttpUtility.UrlDecode(json);

                    if (query.Rejected)
                    {
                        var rejectedParts = Json.Convert.FromJson<List<RejectedPart>>(json);
                        if (!rejectedParts.IsNullOrEmpty())
                        {
                            Database.Write(rejectedParts);
                        }
                    }

                    if (query.Verified)
                    {
                        var verifiedParts = Json.Convert.FromJson<List<VerifiedPart>>(json);
                        if (!verifiedParts.IsNullOrEmpty())
                        {
                            Database.Write(verifiedParts);
                        }
                    }          
                }

                return true;
            }

            return false;
        }

        public bool DeleteData(Uri requestUri)
        {
            var query = new DeleteQuery(requestUri);
            if (query.IsValid)
            {
                if (query.Rejected) Database.DeleteRejectedPart(query.DeviceId, query.PartId);

                if (query.Verified) Database.DeleteVerifiedPart(query.DeviceId, query.PartId);

                return true;
            }

            return false;
        }

        private Event GetEvent(string eventName, string agentVersion)
        {
            var version = new Version(agentVersion);
            var version13 = new Version("1.3.0");
            var version12 = new Version("1.2.0");
            var version11 = new Version("1.1.0");
            var version10 = new Version("1.0.0");

            // Version 1.2
            var config = v12EventsConfiguration;
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "v12events.config");

            // Version 1.3
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

        private static List<string> GetEventIds(Event e, List<DataItemDefinition> dataItems, List<ComponentDefinition> components)
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

            return ids;
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
