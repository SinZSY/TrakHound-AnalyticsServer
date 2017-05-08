// Copyright (c) 2017 TrakHound Inc., All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace TrakHound.AnalyticsServer
{
    internal class RestServer
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

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

                var thread = new Thread(new ThreadStart(Worker));
                thread.Start();
            }
            else
            {
                var ex = new Exception("No URL Prefixes are defined!");
                log.Error(ex);
                throw ex;
            }
        }

        public void Stop()
        {
            if (stop != null) stop.Set();
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

                    var listener = new HttpListener();

                    // Add Prefixes
                    foreach (var prefix in Prefixes)
                    {
                        listener.Prefixes.Add(prefix);
                    }

                    // Start Listener
                    listener.Start();

                    foreach (var prefix in Prefixes) log.Info("Rest Server : Listening at " + prefix + "..");

                    // Listen for Requests
                    while (listener.IsListening && !stop.WaitOne(0, true))
                    {
                        var result = listener.BeginGetContext(ListenerCallback, listener);
                        result.AsyncWaitHandle.WaitOne();
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            } while (!stop.WaitOne(1000, true));
        }

        private void ListenerCallback(IAsyncResult result)
        {
            try
            {
                var listenerClosure = (HttpListener)result.AsyncState;
                var contextClosure = listenerClosure.EndGetContext(result);

                ThreadPool.QueueUserWorkItem(
                    ctx =>
                    {
                        try
                        {
                            var response = (HttpListenerResponse)ctx;

                            log.Info("Connected to : " + contextClosure.Request.LocalEndPoint.ToString() + " : " + contextClosure.Request.Url.ToString());

                            response.Headers.Add("Access-Control-Allow-Origin", "*");
                            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, DELETE");

                            var uri = contextClosure.Request.Url;
                            var method = contextClosure.Request.HttpMethod;

                            switch (method)
                            {
                                case "GET":

                                    using (var stream = contextClosure.Response.OutputStream)
                                    {
                                        contextClosure.Response.StatusCode = 200;
                                        bool found = false;

                                        foreach (var module in Modules.LoadedModules)
                                        {
                                            try
                                            {
                                                var m = Modules.Get(module.GetType());
                                                if (m.GetResponse(uri, stream))
                                                {
                                                    found = true;
                                                    break;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                log.Info(module.Name + " : ERROR : " + ex.Message);
                                            }
                                        }

                                        if (!found) contextClosure.Response.StatusCode = 400;

                                        log.Info("Rest Response : " + contextClosure.Response.StatusCode);
                                    }

                                    break;

                                case "POST":

                                    using (var stream = contextClosure.Request.InputStream)
                                    {
                                        contextClosure.Response.StatusCode = 200;
                                        bool found = false;

                                        foreach (var module in Modules.LoadedModules)
                                        {
                                            var m = Modules.Get(module.GetType());
                                            if (m.SendData(uri, stream))
                                            {
                                                found = true;
                                                break;
                                            }
                                        }

                                        if (!found) contextClosure.Response.StatusCode = 400;

                                        log.Info("Rest Response : " + contextClosure.Response.StatusCode);
                                    }

                                    break;

                                case "DELETE":

                                    contextClosure.Response.StatusCode = 400;

                                    foreach (var module in Modules.LoadedModules)
                                    {
                                        var m = Modules.Get(module.GetType());
                                        if (m.DeleteData(uri))
                                        {
                                            contextClosure.Response.StatusCode = 200;
                                            break;
                                        }
                                    }

                                    log.Info("Rest Response : " + contextClosure.Response.StatusCode);

                                    break;
                            }

                            response.Close();
                        }
                        catch (Exception ex)
                        {
                            log.Debug(ex);
                        }

                    }, contextClosure.Response);
            }
            catch (Exception ex)
            {
                log.Info(ex);
            }
        }
    }
}
