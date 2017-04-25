// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace mod_rest_parts
{
    /// <summary>
    /// Contains the query parameters for a request
    /// </summary>
    class SendQuery
    {
        public string DeviceId { get; set; }

        private bool _isValid = false;
        public bool IsValid { get { return _isValid; } }

        public bool Rejected { get; set; }
        public bool Verified { get; set; }

        public SendQuery(Uri uri)
        {
            if (uri != null)
            {
                var segments = uri.Segments;
                if (segments.Length > 2)
                {
                    // Check if Parts is the resource that is requested
                    if (segments[segments.Length - 2].ToLower().Trim('/') == "parts")
                    {
                        Rejected = segments[segments.Length - 1].ToLower().Trim('/') == "rejected";
                        Verified = segments[segments.Length - 1].ToLower().Trim('/') == "verified";

                        if (Rejected || Verified)
                        {
                            // Get the Device Id as the resource owner
                            DeviceId = segments[segments.Length - 3].Trim('/');
                            _isValid = !string.IsNullOrEmpty(DeviceId);
                        }
                    }
                }
            }
        }
    }
}
