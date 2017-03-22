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
using System.Threading;
using TrakHound.Api.v2;
using TrakHound.Api.v2.Data;
using TrakHound.Api.v2.Events;

namespace mod_rest_programs
{
    [InheritedExport(typeof(IRestModule))]
    public class Programs : IRestModule
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public string Name { get { return "Programs"; } }


        public bool GetResponse(Uri requestUri, Stream stream)
        {
            var query = new RequestQuery(requestUri);
            if (query.IsValid)
            {
                try
                {
                    while (stream != null)
                    {
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

                            // Get Samples
                            samples = Database.ReadSamples(null, query.DeviceId, query.From, query.To, query.At, query.Count);
                        }

                        if (!dataItems.IsNullOrEmpty() && !samples.IsNullOrEmpty())
                        {
                            var e = GetEvent("Program Status");
                            if (e != null)
                            {
                                // Get the initial timestamp
                                DateTime timestamp;
                                if (query.From > DateTime.MinValue) timestamp = query.From;
                                else if (query.At > DateTime.MinValue) timestamp = query.At;
                                else timestamp = samples.Select(o => o.Timestamp).OrderByDescending(o => o).First();

                                // Create a list of DataItemInfos (DataItems with Parent Component info)
                                var dataItemInfos = DataItemInfo.CreateList(dataItems, components);

                                // Program Name DataItem
                                var programNameItem = dataItems.Find(o => o.Type == "PROGRAM");

                                // Execution DataItem
                                var executionItem = dataItems.Find(o => o.Type == "EXECUTION");

                                // Get Path Components
                                var paths = components.FindAll(o => o.Type == "Path");

                                var currentSamples = samples.FindAll(o => o.Timestamp <= timestamp);

                                string previousProgramName = null;
                                var programs = new List<Program>();
                                Program program = null;
                                ProgramEvent programEvent = null;

                                var timestamps = samples.FindAll(o => o.Timestamp >= timestamp).OrderBy(o => o.Timestamp).Select(o => o.Timestamp).Distinct();
                                foreach (var time in timestamps)
                                {
                                    // Update CurrentSamples
                                    foreach (var sample in samples.FindAll(o => o.Timestamp == time))
                                    {
                                        int i = currentSamples.FindIndex(o => o.Id == sample.Id);
                                        if (i >= 0) currentSamples[i] = sample;
                                        else currentSamples.Add(sample);
                                    }

                                    if (programNameItem != null && executionItem != null)
                                    {
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
                                            response.Timestamp = time;

                                            if (!string.IsNullOrEmpty(programName) && programName != "UNAVAILABLE" && 
                                                program == null && response.Value != "Stopped" && response.Value != "Completed")
                                            {
                                                // Create a new Program object
                                                var prog = new Program();
                                                prog.Name = programName;
                                                prog.Start = time;
                                                programs.Add(prog);

                                                program = prog;
                                            }

                                            // Check if program has changed
                                            if (programName != previousProgramName)
                                            {
                                                // Set the stop time for the previous ProgramEvent
                                                if (programEvent != null)
                                                {
                                                    programEvent.Stop = time;
                                                    program.Stop = time;
                                                }

                                                if (!string.IsNullOrEmpty(programName) && programName != "UNAVAILABLE" &&
                                                    response.Value != "Stopped" && response.Value != "Completed")
                                                {
                                                    // Create a new Program object
                                                    var prog = new Program();
                                                    prog.Name = programName;
                                                    prog.Start = time;
                                                    programs.Add(prog);

                                                    program = prog;
                                                }
                                                else program = null;

                                                previousProgramName = programName;
                                            }


                                            if (program != null)
                                            {
                                                // Set the stop time for the previous ProgramEvent
                                                if (programEvent != null)
                                                {
                                                    programEvent.Stop = response.Timestamp;
                                                    program.Stop = response.Timestamp;
                                                }

                                                if (response.Value != "Stopped" && response.Value != "Completed")
                                                {
                                                    // Create a new ProgramEvent
                                                    var progEvent = new ProgramEvent();
                                                    progEvent.Name = response.Value;
                                                    progEvent.Description = response.Description;
                                                    progEvent.ExecutionMode = execution;
                                                    progEvent.Start = response.Timestamp;
                                                    program.Events.Add(progEvent);
                                                    programEvent = progEvent;
                                                }
                                                else if (response.Value == "Completed")
                                                {
                                                    program.Completed = true;
                                                }
                                            }
                                        }
                                    }
                                }

                                if (programs != null)
                                {
                                    // Write JSON to stream
                                    string json = TrakHound.Api.v2.Json.Convert.ToJson(programs, true);
                                    var bytes = Encoding.UTF8.GetBytes(json);
                                    stream.Write(bytes, 0, bytes.Length);
                                }
                            }
                        }

                        if (query.Interval <= 0) break;
                        else Thread.Sleep(query.Interval);
                    }
                }
                catch (Exception ex)
                {
                    log.Info("Programs Stream Closed");
                    log.Trace(ex);
                }

                return true;
            }

            return false;
        }

        private Event GetEvent(string eventName)
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EventsConfiguration.FILENAME);

            // Read the EventsConfiguration file
            var config = EventsConfiguration.Get(configPath);
            if (config != null)
            {
                if (!string.IsNullOrEmpty(eventName)) return config.Events.Find(o => o.Name.ToLower() == eventName.ToLower());
            }

            return null;
        }
        
    }
}
