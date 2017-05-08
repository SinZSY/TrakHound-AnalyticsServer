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

namespace mod_rest_oee
{
    [InheritedExport(typeof(IRestModule))]
    public class Module : IRestModule
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public string Name { get { return "Oee"; } }


        public bool GetResponse(Uri requestUri, Stream stream)
        {
            var query = new RequestQuery(requestUri);
            if (query.IsValid)
            {
                // Get Current Agent
                var agent = Database.ReadAgent(query.DeviceId);
                if (agent != null)
                {
                    // Get Components
                    var components = Database.ReadComponents(query.DeviceId, agent.InstanceId);

                    // Get Data Items
                    var dataItems = Database.ReadDataItems(query.DeviceId, agent.InstanceId);

                    var processor = new Processor(query, dataItems, components, agent.Version);
                    var oees = processor.Run();

                    // Write JSON to stream
                    string json = Json.Convert.ToJson(oees, true);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
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
    }
}
