// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Newtonsoft.Json;
using System.Collections.Generic;
using TrakHound.Api.v2;
using TrakHound.Api.v2.Data;

namespace mod_rest_model
{
    class DeviceItem : Device
    {
        [JsonProperty("connection", Order = 4)]
        public Connection Connection { get; set; }

        [JsonProperty("agent", Order = 5)]
        public Agent Agent { get; set; }

        [JsonProperty("data_items", Order = 6)]
        public List<DataItem> DataItems { get; set; }

        [JsonProperty("components", Order = 7)]
        public List<ComponentItem> Components { get; set; }


        public DeviceItem(DeviceDefinition device, ConnectionDefinition connection, AgentDefinition agent)
        {
            Connection = new Connection();
            Connection.Address = connection.Address;
            Connection.Port = connection.Port;
            Connection.PhysicalAddress = connection.PhysicalAddress;

            Agent = new Agent();
            Agent.BufferSize = agent.BufferSize;
            Agent.InstanceId = agent.InstanceId;
            Agent.Sender = agent.Sender;
            Agent.TestIndicator = agent.TestIndicator;
            Agent.Timestamp = agent.Timestamp;
            Agent.Version = agent.Version;

            Id = device.Id;
            Uuid = device.Uuid;
            Name = device.Name;
            Iso841Class = device.Iso841Class;
            NativeName = device.NativeName;
            SampleInterval = device.SampleInterval;
            SampleRate = device.SampleRate;
            Manufacturer = device.Manufacturer;
            Model = device.Model;
            SerialNumber = device.SerialNumber;
            Description = device.Description;
        }

        public void Add(ComponentItem component)
        {
            if (Components == null) Components = new List<ComponentItem>();
            Components.Add(component);
        }

        public void Add(List<ComponentItem> components)
        {
            if (!components.IsNullOrEmpty())
            {
                if (Components == null) Components = new List<ComponentItem>();
                Components.AddRange(components);
            }
        }

        public void Add(DataItem dataItem)
        {
            if (DataItems == null) DataItems = new List<DataItem>();
            DataItems.Add(dataItem);
        }

        public void Add(List<DataItem> dataItems)
        {
            if (!dataItems.IsNullOrEmpty())
            {
                if (DataItems == null) DataItems = new List<DataItem>();
                DataItems.AddRange(dataItems);
            }
        }
    }
}
