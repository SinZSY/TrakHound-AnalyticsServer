// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Web;

namespace mod_rest_programs
{
    /// <summary>
    /// Contains the query parameters for a request
    /// </summary>
    class RequestQuery
    {
        public string DeviceId { get; set; }

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        private bool _isValid = false;
        public bool IsValid { get { return _isValid; } }

        public RequestQuery(Uri uri)
        {
            if (uri != null)
            {
                var segments = uri.Segments;
                if (segments.Length > 1)
                {
                    // Check if Programs is the resource that is requested
                    if (segments[segments.Length - 1].ToLower().Trim('/') == "programs")
                    {
                        // Get the Device Id as the resource owner
                        DeviceId = segments[segments.Length - 2].Trim('/');
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

                            _isValid = From > DateTime.MinValue || To > DateTime.MinValue;
                        }
                    }
                }
            }
        }
    }
}
