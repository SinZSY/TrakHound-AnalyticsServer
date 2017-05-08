// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Web;

namespace mod_rest_oee
{
    /// <summary>
    /// Contains the query parameters for a request
    /// </summary>
    class RequestQuery
    {
        public string DeviceId { get; set; }

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public int Interval { get; set; }

        public int Increment { get; set; }

        public bool Details { get; set; }

        private bool _isValid = false;
        public bool IsValid { get { return _isValid; } }

        public RequestQuery(Uri uri)
        {
            if (uri != null)
            {
                var segments = uri.Segments;
                if (segments.Length > 1)
                {
                    bool valid = false;
                    int i = 2;

                    // Get the requested Query and SubQuery
                    string query = segments[segments.Length - 1].ToLower().Trim('/');
                    if (query == "oee") valid = true;

                    if (valid)
                    {
                        // Get the Device Id as the resource owner
                        DeviceId = segments[segments.Length - i].Trim('/');
                        if (!string.IsNullOrEmpty(DeviceId))
                        {
                            // From
                            string s = HttpUtility.ParseQueryString(uri.Query).Get("from");
                            DateTime from = DateTime.MinValue;
                            if (DateTime.TryParse(s, out from)) from = from.ToUniversalTime();
                            From = from;

                            // To
                            s = HttpUtility.ParseQueryString(uri.Query).Get("to");
                            DateTime to = DateTime.MinValue;
                            if (DateTime.TryParse(s, out to)) to = to.ToUniversalTime();
                            To = to;

                            // Interval
                            s = HttpUtility.ParseQueryString(uri.Query).Get("interval");
                            int interval = 0;
                            int.TryParse(s, out interval);
                            Interval = interval;

                            // Increment
                            s = HttpUtility.ParseQueryString(uri.Query).Get("increment");
                            int increment = 0;
                            int.TryParse(s, out increment);
                            Increment = increment;

                            // Details
                            s = HttpUtility.ParseQueryString(uri.Query).Get("details");
                            Details = !string.IsNullOrEmpty(s) && s.ToLower() == "true"; 

                            _isValid = From > DateTime.MinValue || To > DateTime.MinValue;
                        }
                    }
                }
            }
        }
    }
}
