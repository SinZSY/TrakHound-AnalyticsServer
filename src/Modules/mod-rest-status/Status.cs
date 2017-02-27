// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using NLog;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using TrakHound.Api.v2;
using Json = TrakHound.Api.v2.Json;

namespace mod_rest_status
{
    [InheritedExport(typeof(IRestModule))]
    public class Status : IRestModule
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public string Name { get { return "Status"; } }


        public bool GetResponse(Uri requestUri, Stream stream)
        {
            var query = new RequestQuery(requestUri);
            if (query.IsValid)
            {
                log.Info("Status Request Received : " + query.DeviceId);

                try
                {
                    if (!string.IsNullOrEmpty(query.DeviceId))
                    {
                        var status = Database.ReadStatus(query.DeviceId);
                        if (status != null)
                        {
                            // Write DeviceItem JSON to stream
                            string json = Json.Convert.ToJson(status, true);
                            var bytes = Encoding.UTF8.GetBytes(json);
                            stream.Write(bytes, 0, bytes.Length);
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    log.Trace(ex);
                }
            }

            return false;
        }
    }
}
