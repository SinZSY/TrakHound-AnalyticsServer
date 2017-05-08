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
    class Quality
    {
        public const string EVENT_NAME = "Program Status";
        public const string EVENT_VALUE = "Completed";

        private static Logger log = LogManager.GetCurrentClassLogger();


        [JsonProperty("total_parts")]
        public int TotalParts { get; set; }

        [JsonProperty("good_parts")]
        public int GoodParts { get; set; }

        [JsonProperty("value")]
        public double Value
        {
            get
            {
                if (TotalParts > 0) return Math.Round(((double)GoodParts / TotalParts), 5);
                return 0;
            }
        }

        public Quality(int goodParts, int totalParts)
        {
            GoodParts = goodParts;
            TotalParts = totalParts;
        }

        public static List<Part> GetParts(DateTime from, DateTime to, Event e, List<Sample> samples, List<DataItemInfo> dataItemInfos)
        {
            if (!samples.IsNullOrEmpty())
            {
                // Program Name DataItem
                var programNameItem = dataItemInfos.Find(o => o.Type == "PROGRAM");

                // Execution DataItem
                var executionItem = dataItemInfos.Find(o => o.Type == "EXECUTION");

                // Get the initial timestamp
                DateTime timestamp;
                if (from > DateTime.MinValue) timestamp = from;
                else timestamp = samples.Select(o => o.Timestamp).OrderByDescending(o => o).First();

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
                        string programName = null;
                        if (currentSamples.Exists(o => o.Id == programNameItem.Id))
                        {
                            programName = currentSamples.Find(o => o.Id == programNameItem.Id).CDATA;
                        }

                        // Execution
                        string execution = null;
                        if (currentSamples.Exists(o => o.Id == executionItem.Id))
                        {
                            execution = currentSamples.Find(o => o.Id == executionItem.Id).CDATA;
                        }

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

                    var toTime = to > DateTime.MinValue ? to : DateTime.UtcNow;

                    if (part != null)
                    {
                        part.Stop = toTime;
                        parts.Add(part);
                    }

                    return parts;
                }
            }

            return null;
        }
    }
}
