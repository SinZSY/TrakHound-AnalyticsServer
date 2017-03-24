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
using Json = TrakHound.Api.v2.Json;

namespace mod_rest_alarms
{
    [InheritedExport(typeof(IRestModule))]
    public class Module : IRestModule
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public string Name { get { return "Alarms"; } }


        public bool GetResponse(Uri requestUri, Stream stream)
        {
            var query = new RequestQuery(requestUri);
            if (query.IsValid)
            {
                try
                {
                    while (stream != null)
                    {
                        List<DataItemDefinition> dataItems = null;
                        List<Sample> samples = null;

                        // Get Current Agent
                        var agent = Database.ReadAgent(query.DeviceId);
                        if (agent != null)
                        {
                            // Get Data Items
                            dataItems = Database.ReadDataItems(query.DeviceId, agent.InstanceId);
                            if (dataItems != null)
                            {
                                // Find all of the Conditions by DataItemId
                                var errorIds = dataItems.FindAll(o => o.Category == "CONDITION").Select(o => o.Id);
                                if (!errorIds.IsNullOrEmpty())
                                {
                                    // Get Samples
                                    samples = Database.ReadSamples(errorIds.ToArray(), query.DeviceId, query.From, query.To, query.At, query.Count);
                                    if (!samples.IsNullOrEmpty())
                                    {
                                        var alarms = new List<Alarm>();

                                        // Create a new Alarm object for each Sample
                                        foreach (var sample in samples)
                                        {
                                            if (sample.Condition != "NORMAL")
                                            {
                                                alarms.Add(new Alarm(sample));
                                            }
                                        }

                                        if (!alarms.IsNullOrEmpty())
                                        {
                                            // Write Error JSON to stream
                                            string json = Json.Convert.ToJson(alarms, true);
                                            var bytes = Encoding.UTF8.GetBytes(json);
                                            stream.Write(bytes, 0, bytes.Length);
                                        }
                                    }
                                }
                            }
                        }


                        if (query.Interval <= 0) break;
                        else Thread.Sleep(query.Interval);
                    }
                }
                catch (Exception ex)
                {
                    log.Info("Alarms Stream Closed");
                    log.Trace(ex);
                }

                return true;
            }

            return false;
        }

    }
}
