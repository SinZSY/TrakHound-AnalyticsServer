// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using TrakHound.Api.v2;
using TrakHound.Api.v2.Data;
using TrakHound.Api.v2.Events;

namespace mod_rest_activity
{
    [InheritedExport(typeof(IRestModule))]
    public class Module : IRestModule
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        private static EventsConfiguration v12EventsConfiguration;
        private static EventsConfiguration v13EventsConfiguration;

        public string Name { get { return "Activity"; } }


        public bool GetResponse(Uri requestUri, Stream stream)
        {
            var query = new RequestQuery(requestUri);
            if (query.IsValid)
            {
                try
                {
                    var previousEvents = new List<EventItem>();

                    List<ComponentDefinition> components = null;
                    List<DataItemDefinition> dataItems = null;
                    List<Sample> samples = null;

                    // Get Current Agent
                    var agent = Database.ReadAgent(query.DeviceId);
                    if (agent != null)
                    {
                        // Get Components
                        components = Database.ReadComponents(query.DeviceId, agent.InstanceId);

                        // Get Data Items
                        dataItems = Database.ReadDataItems(query.DeviceId, agent.InstanceId);
                    }

                    if (!dataItems.IsNullOrEmpty())
                    {
                        DateTime from = query.From;

                        while (stream != null)
                        {
                            var activityItem = new ActivityItem();
                            var pathItems = new List<PathItem>();

                            // Get Samples
                            samples = Database.ReadSamples(null, query.DeviceId, from, query.To, query.At, query.Count);

                            if (!samples.IsNullOrEmpty())
                            {
                                var events = GetEvents(agent.Version);
                                if (events != null)
                                {
                                    // Get the initial timestamp
                                    DateTime timestamp;
                                    if (query.From > DateTime.MinValue) timestamp = query.From;
                                    else if (query.At > DateTime.MinValue) timestamp = query.At;
                                    else timestamp = samples.Select(o => o.Timestamp).OrderByDescending(o => o).First();

                                    // Create a list of DataItemInfos (DataItems with Parent Component info)
                                    var dataItemInfos = DataItemInfo.CreateList(dataItems, components);

                                    // Get Path Components
                                    var paths = components.FindAll(o => o.Type == "Path");

                                    foreach (var e in events)
                                    {
                                        // Check if Event relies on Path and there are multiple paths
                                        if (ContainsPath(e, components, dataItems) && paths.Count > 1)
                                        {
                                            foreach (var path in paths)
                                            {
                                                // Find all DataItemInfo descendants of this Path
                                                var pathInfos = dataItemInfos.FindAll(o => !o.Parents.Exists(x => x.Type == "Path") || o.ParentId == path.Id);

                                                var pathItem = pathItems.Find(o => o.Id == path.Id);
                                                if (pathItem == null)
                                                {
                                                    // Create new PathItem
                                                    pathItem = new PathItem();
                                                    pathItem.Id = path.Id;
                                                    pathItem.Name = path.Name;
                                                    pathItems.Add(pathItem);
                                                }

                                                // Get a list of EventItems for the Path
                                                pathItem.Events.AddRange(GetEvents(e, pathInfos, samples, timestamp));
                                            }
                                        }
                                        else
                                        {
                                            activityItem.Add(GetEvents(e, dataItemInfos, samples, timestamp));
                                        }
                                    }

                                    activityItem.Add(pathItems);

                                    bool send = false;

                                    // Filter out old events (for streaming)
                                    foreach (var eventItem in activityItem.Events)
                                    {
                                        int i = previousEvents.FindIndex(o => o.Name == eventItem.Name);
                                        if (i < 0)
                                        {
                                            send = true;
                                            previousEvents.Add(eventItem);
                                        }
                                        else
                                        {
                                            if (previousEvents[i].Value != eventItem.Value)
                                            {
                                                //previousEvents[i] = eventItem;
                                                previousEvents.RemoveAt(i);
                                                previousEvents.Add(eventItem);
                                                send = true;
                                            }
                                        }
                                    }

                                    if (send)
                                    {
                                        // Write JSON to stream
                                        string json = TrakHound.Api.v2.Json.Convert.ToJson(activityItem, query.Interval == 0);
                                        if (query.Interval > 0) json += "\r\n";
                                        var bytes = Encoding.UTF8.GetBytes(json);
                                        stream.Write(bytes, 0, bytes.Length);
                                    }
                                    else stream.WriteByte(32);
                                }
                            }

                            if (from > DateTime.MinValue) from = from.AddMilliseconds(query.Interval);

                            if (query.Interval <= 0) break;
                            else Thread.Sleep(query.Interval);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Info("Activity Stream Closed");
                    log.Trace(ex);
                }

                return true;
            }

            return false;
        }

        public bool SendData(Uri requestUri, Stream stream)
        {
            return false;
        }

        public bool DeleteData(Uri requestUri)
        {
            return false;
        }

        private List<Event> GetEvents(string agentVersion)
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

                return config.Events;
            }

            return null;
        }

        private List<EventItem> GetEvents(Event e, List<DataItemInfo> dataItemInfos, List<Sample> samples, DateTime from)
        {
            var l = new List<EventItem>();

            if (!samples.IsNullOrEmpty())
            {
                // Create a list of SampleInfo objects with DataItem information contained
                var infos = SampleInfo.Create(dataItemInfos, samples);

                // Get a list of instance values
                var instance = infos.FindAll(o => o.Timestamp <= from);

                // Find all distinct timestamps greater than or equal to 'from'
                var timestamps = infos.FindAll(o => o.Timestamp > from).Select(o => o.Timestamp).Distinct().OrderBy(o => o).ToList();

                int i = 0;
                DateTime timestamp = from;

                do
                {
                    // Evaluate Event
                    var response = e.Evaluate(instance);
                    if (response != null)
                    {
                        var item = new EventItem();
                        item.Timestamp = response.Timestamp;
                        item.Name = e.Name;
                        item.Description = e.Description;
                        item.Value = response.Value;
                        item.ValueDescription = response.Description;

                        l.Add(item);
                    }

                    if (timestamps.Count > 0)
                    {
                        // Update instance values
                        var atTimestamp = infos.FindAll(o => o.Timestamp == timestamps[i]);
                        foreach (var sample in atTimestamp)
                        {
                            var match = instance.Find(o => o.Id == sample.Id);
                            if (match != null) instance.Remove(match);
                            instance.Add(sample);
                        }
                    }
                    else break;

                    i++;

                } while (i < timestamps.Count - 1);
            }

            return l;
        }

        /// <summary>
        /// Get whether or not the Event is triggered by an item that is part of a Path component
        /// </summary>
        private static bool ContainsPath(Event e, List<ComponentDefinition> components, List<DataItemDefinition> dataItems)
        {
            foreach (var response in e.Responses)
            {
                foreach (var trigger in response.Triggers.OfType<Trigger>())
                {
                    string filter = trigger.Filter;

                    bool match = false;

                    if (filter.Contains("Path")) return true;
                    else
                    {
                        var parts = filter.Split('/');
                        string dataType = parts[parts.Length - 1];

                        foreach (var path in components.FindAll(o => o.Type == "Path"))
                        {
                            var pathDataItems = dataItems.FindAll(o => o.ParentId == path.Id);
                            match = pathDataItems.Exists(o => NormalizeType(o.Type) == NormalizeType(dataType));
                            if (match) return true;
                        }
                    }
                }
            }

            return false;
        }

        private static string NormalizeType(string s)
        {
            string debug = s;

            if (!string.IsNullOrEmpty(s))
            {
                if (s.ToUpper() != s)
                {
                    // Split string by Uppercase characters
                    var parts = Regex.Split(s, @"(?<!^)(?=[A-Z])");
                    s = string.Join("_", parts);
                    s = s.ToUpper();
                }

                // Return to Pascal Case
                s = s.Replace("_", " ");
                s = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());

                s = s.Replace(" ", "");
            }

            return s;
        }

    }
}
