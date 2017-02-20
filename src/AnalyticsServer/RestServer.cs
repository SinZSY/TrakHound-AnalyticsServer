﻿// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Messaging = TrakHound.Api.v2.Messaging;

namespace TrakHound.AnalyticsServer
{
    internal class RestServer
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        private HttpListener listener;
        private Thread thread;
        private ManualResetEvent stop;
        private Configuration configuration;

        public List<string> Prefixes { get; set; }

        public RestServer(Configuration config)
        {
            configuration = config;
            Prefixes = config.Prefixes;

            // Load the REST Modules
            Modules.Load();
        }

        public void Start()
        {
            log.Info("REST Server Started..");

            if (Prefixes != null && Prefixes.Count > 0)
            {
                stop = new ManualResetEvent(false);

                thread = new Thread(new ThreadStart(Worker));
                thread.Start();
            }
            else
            {
                var ex = new Exception("No URL Prefixes are defined!");
                log.Error(ex);
                throw ex;
            }

            if (configuration.SendMessages)
            {
                Messaging.Message.Send("trakhound-analyticsserver-menu", "Notify", "Started");
                Messaging.Message.Send("trakhound-analyticsserver-menu", "Status", "Running");
            }
        }

        public void Stop()
        {
            if (stop != null) stop.Set();

            if (configuration.SendMessages)
            {
                Messaging.Message.Send("trakhound-analyticsserver-menu", "Notify", "Stopped");
                Messaging.Message.Send("trakhound-analyticsserver-menu", "Status", "Stopped");
            }
        }

        private void Worker()
        {
            do
            {
                try
                {
                    // (Access Denied - Exception)
                    // Must grant permissions to use URL (for each Prefix) in Windows using the command below
                    // CMD: netsh http add urlacl url = "http://localhost/" user = everyone

                    // (Service Unavailable - HTTP Status)
                    // Multiple urls are configured using netsh that point to the same place
 
                    listener = new HttpListener();

                    // Add Prefixes
                    foreach (var prefix in Prefixes)
                    {
                        listener.Prefixes.Add(prefix);
                    }
                    
                    // Start Listener
                    listener.Start();

                    foreach (var prefix in Prefixes) log.Info("Rest Server : Listening at " + prefix + "..");

                    while (listener.IsListening && !stop.WaitOne(0, true))
                    {
                        var context = listener.GetContext();

                        // Handle the request on a new thread
                        ThreadPool.QueueUserWorkItem((o) =>
                        {
                            HandleRequest(context);
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            } while (!stop.WaitOne(1000, true));
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                log.Info("Connected to : " + context.Request.LocalEndPoint.ToString());

                var uri = context.Request.Url;
                using (var stream = context.Response.OutputStream)
                {
                    context.Response.StatusCode = 400;

                    foreach (var module in Modules.LoadedModules)
                    {
                        var m = Modules.Get(module.GetType());
                        if (m.GetResponse(uri, stream))
                        {
                            context.Response.StatusCode = 200;
                            break;
                        }
                    }

                    log.Info("Rest Response : " + context.Response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                log.Error(ex);
            }
            finally
            {
                context.Response.OutputStream.Close();
                context.Response.Close();
                log.Info("Output Stream Closed : " + context.Request.LocalEndPoint.ToString());
            }
        }

    }
}
