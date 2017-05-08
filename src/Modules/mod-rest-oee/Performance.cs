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

namespace mod_rest_oee
{
    class Performance
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        [JsonProperty("operating_time")]
        public double OperatingTime { get; set; }

        [JsonProperty("ideal_operating_time")]
        public double IdealOperatingTime { get; set; }

        [JsonProperty("value")]
        public double Value
        {
            get
            {
                if (IdealOperatingTime > 0) return Math.Round(IdealOperatingTime / OperatingTime, 5);
                return 0;
            }
        }

        internal List<PerformanceEvent> _events;
        [JsonProperty("events")]
        public List<PerformanceEvent> Events { get; set; }


        public Performance(double operatingTime, double idealOperatingTime, List<PerformanceEvent> events)
        {
            OperatingTime = Math.Round(operatingTime, 3);
            IdealOperatingTime = Math.Round(idealOperatingTime, 3);

            if (!events.IsNullOrEmpty()) _events = events;
        }

        public static Performance Get(List<Sample> samples, DateTime from, DateTime to, List<AvailabilityEvent> availabilityEvents, bool details)
        {
            samples = samples.OrderBy(o => o.Timestamp).ToList();

            var overrideEvents = new List<OverrideEvent>();
            double previousOverride = 0;
            DateTime previousTime = DateTime.MinValue;

            for (int i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];

                double feedrateOverride = -1;
                if (double.TryParse(sample.CDATA, out feedrateOverride))
                {
                    if (previousOverride != feedrateOverride)
                    {
                        if (i > 0 && previousOverride >= 0)
                        {
                            overrideEvents.Add(new OverrideEvent(previousOverride, previousTime, samples[i].Timestamp));
                        }

                        previousTime = sample.Timestamp < from ? from : sample.Timestamp;
                    }

                    previousOverride = feedrateOverride;
                }
            }

            var toTimestamp = to > DateTime.MinValue ? to : DateTime.UtcNow;

            // Add the last Event
            overrideEvents.Add(new OverrideEvent(previousOverride, previousTime, toTimestamp));

            if (overrideEvents != null)
            {
                var performanceEvents = new List<PerformanceEvent>();
                double operatingTime = 0;
                double idealOperatingTime = 0;

                foreach (var overrideEvent in overrideEvents)
                {
                    double eventOperatingTime = 0;

                    foreach (var availEvent in availabilityEvents)
                    {
                        if ((availEvent.Start >= overrideEvent.Start && availEvent.Start < overrideEvent.Stop) || (availEvent.Stop <= overrideEvent.Stop && availEvent.Stop > overrideEvent.Start))
                        {
                            // Set Start Time
                            var startTime = availEvent.Start < overrideEvent.Start ? overrideEvent.Start : availEvent.Start;

                            // Set Stop Time
                            var stopTime = availEvent.Stop > overrideEvent.Stop ? overrideEvent.Stop : availEvent.Stop;

                            var seconds = (stopTime - startTime).TotalSeconds;
                            eventOperatingTime += seconds;
                        }
                    }

                    double eventIdealOperatingTime = eventOperatingTime * (overrideEvent.FeedrateOverride / 100);

                    operatingTime += eventOperatingTime;
                    idealOperatingTime += eventIdealOperatingTime;

                    if (operatingTime > 0)
                    {
                        performanceEvents.Add(new PerformanceEvent(overrideEvent.FeedrateOverride, eventOperatingTime, eventIdealOperatingTime, overrideEvent.Start, overrideEvent.Stop));
                    }
                }

                var performance = new Performance(operatingTime, idealOperatingTime, performanceEvents);
                if (details) performance.Events = performanceEvents;
                return performance;
            }

            return null;
        }
    }
}
