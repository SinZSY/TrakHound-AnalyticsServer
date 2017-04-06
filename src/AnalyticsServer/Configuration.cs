// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace TrakHound.AnalyticsServer
{
    [XmlRoot("AnalyticsServer")]
    public class Configuration
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static Configuration Current { get; set; }

        [XmlIgnore]
        public const string FILENAME = "server.config";

        [XmlElement("EndPoints")]
        public EndPointRange EndPoints { get; set; }

        [XmlElement("DatabaseConfigurationPath")]
        public string DatabaseConfigurationPath { get; set; }

        [XmlArray("Prefixes")]
        [XmlArrayItem("Prefix")]
        public List<string> Prefixes { get; set; }


        public static Configuration Get(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(Configuration));
                    using (var fileReader = new FileStream(path, FileMode.Open))
                    using (var xmlReader = XmlReader.Create(fileReader))
                    {
                        var config = (Configuration)serializer.Deserialize(xmlReader);
                        Current = config;

                        return config;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }

            return null;
        }
    }
}
