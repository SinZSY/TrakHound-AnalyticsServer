// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Threading;
using TrakHound.Api.v2;
using Json = TrakHound.Api.v2.Json;

namespace mod_rest_samples
{
    [InheritedExport(typeof(IRestModule))]
    public class Module : IRestModule
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public string Name { get { return "Samples"; } }

        public bool GetResponse(Uri requestUri, Stream stream)
        {
            var query = new RequestQuery(requestUri);
            if (query.IsValid)
            {
                try
                {
                    var sent = new List<Item>();

                    DateTime from = query.From;

                    while (stream != null)
                    {
                        log.Debug("Retrieving Samples : " + requestUri.ToString());

                        // Read Samples from Database
                        var samples = Database.ReadSamples(query.DataItems, query.DeviceId, from, query.To, query.At, query.Count);
                        if (!samples.IsNullOrEmpty())
                        {
                            foreach (var sample in samples)
                            {
                                bool write = true;
                                var item = new Item(sample);

                                // Only write to output stream if new
                                var x = sent.Find(o => o.Id == sample.Id);
                                if (x != null)
                                {
                                    if (sample.Timestamp > x.Timestamp)
                                    {
                                        sent.Remove(x);
                                        sent.Add(item);
                                    }
                                    else write = false;
                                }
                                else sent.Add(item);

                                if (write)
                                {
                                    string json = Json.Convert.ToJson(item);
                                    json += "\r\n";
                                    var bytes = Encoding.UTF8.GetBytes(json);
                                    stream.Write(bytes, 0, bytes.Length);
                                    stream.Flush();
                                }
                                else stream.WriteByte(32);
                            }
                        }

                        if (from > DateTime.MinValue) from = DateTime.UtcNow;

                        if (query.Interval <= 0) break;
                        else Thread.Sleep(query.Interval);
                    }
                }
                catch (Exception ex)
                {
                    log.Info("Samples Stream Closed");
                    log.Trace(ex);
                }

                return true;
            }

            return false;
        }
    }
}
