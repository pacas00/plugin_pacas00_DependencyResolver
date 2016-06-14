using System;
using System.Collections.Generic;
using net.PeterCashel.DependencyResolver.Event;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace net.PeterCashel.DependencyResolver
{
    public delegate void DependencyLogHandler(object source, DependencyLogEventArgs e);

    public delegate void RegisterAssembliesHandler(object source, EventArgs e);


    public static class DependencyResolver
    {
        #region Variables
        private static bool _useCache = true;
        static bool _initComplete = false;
        private static ConcurrentQueue<string[]> _processingQueue = new ConcurrentQueue<string[]>();

        /// <summary>
        /// Register against this event to receive logging information from the resolver. Once someone registers, it stops logging to Debug.Log
        /// </summary>
        public static event DependencyLogHandler LogEvent;

        /// <summary>
        /// Register against this event to be notifed when the DependencyResolver is ready for assemby registration
        /// </summary>
        public static event RegisterAssembliesHandler RegisterEvent;

        private static Dictionary<string, string> _knownAssemblies = new Dictionary<string, string>();
        private static Dictionary<string, string> _webAssemblies = new Dictionary<string, string>();
        private static List<string> _failedAssemblies = new List<string>(); //If we fail to get them, don't try again.

        private static string _cacheDir = "";

        public static bool IsReadyForRegistration { get; private set; } = false;

        #endregion



        private static void DoInit()
        {
            if (_initComplete) return;
            _initComplete = true;

            while (!_processingQueue.IsEmpty)
            {
                string[] queueStrings;
                if (!_processingQueue.TryDequeue(out queueStrings)) break;
                AddAssemblyFromWeb(queueStrings[0], queueStrings[1]);
            }
        }

        //Fired by mod class when its time to startup.
        public static void StartupEvent(object source, ModStartupEventArgs e)
        {
            Directory.CreateDirectory(e.WorkingDir + Path.DirectorySeparatorChar + "cache" + Path.DirectorySeparatorChar +
                                      "dll");
            string localCache = e.WorkingDir + Path.DirectorySeparatorChar + "cache" + Path.DirectorySeparatorChar +
                                "dll";

            if (localCache != null)
            {
                _cacheDir = localCache;
            }
            else
            {
                _useCache = false;
            }

            //Register this classes AssemblyResolve event handler in the current app domain
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            if (_useCache) ResolveLocalLibs();

            IsReadyForRegistration = true;

            if (RegisterEvent != null) RegisterEvent.Invoke(null, new EventArgs());

            DoInit();
        }

        //Used across plugins.
        internal static void WriteLine(string v)
        {
#if DEBUG
            if (LogEvent != null && LogEvent.GetInvocationList().Length == 0) Debug.Log(v);
#endif
            LogEvent?.Invoke(null, new DependencyLogEventArgs(v));
        }

        private static void ResolveLocalLibs()
        {
            DirectoryInfo dInfo = new DirectoryInfo(_cacheDir);
            foreach (FileInfo file in dInfo.GetFiles())
            {
                try
                {
                    Assembly assembly = Assembly.LoadFile(file.FullName);
                    AddAssemblyFromFile(assembly.FullName, file.FullName);

                    WriteLine("Adding locally cached DLL " + file.Name);
                }
                catch (BadImageFormatException bifEx)
                {
                    //get rid of damaged DLL
                    WriteLine("Deleting Damaged DLL " + file.Name);
                    file.Delete();
                }
            }
        }

        //Local Method for adding cached files.
        internal static void AddAssemblyFromFile(string assemblyName, string assemblyPath)
        {
            WriteLine("Adding File Dependency " + assemblyName);
            _knownAssemblies.Add(assemblyName, assemblyPath);
        }

        /// <summary>
        /// Adds the assembly from web. http://, https:// and file:// protocols are supported
        /// </summary>
        /// <returns>The Assembly from web.</returns>
        /// <param name="assemblyName">Assembly name.</param>
        /// <param name="assemblyUrl">Assembly URL.</param>
        public static void AddAssemblyFromWeb(string assemblyName, string assemblyUrl)
        {
            WriteLine("Adding Web Dependency " + assemblyName);
            if (!_initComplete) _processingQueue.Enqueue(new string[2] { assemblyName, assemblyUrl });
            else _webAssemblies.Add(assemblyName, assemblyUrl);
        }

        public static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            #region File DLL
            if (_knownAssemblies.ContainsKey(args.Name))
            {
                string assemblyPath = _knownAssemblies[args.Name];
                WriteLine("Resolving File Dependency " + args.Name);
                try
                {
                    Assembly loaded = Assembly.LoadFile(assemblyPath);
                    if (loaded != null)
                    {
                        WriteLine("Sucessfully Resolved " + loaded.FullName);
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    //path is not a valid assembly. 
                    return null;
                }
            }

            #endregion

            #region Web DLL

            if (_webAssemblies.ContainsKey(args.Name) && !_failedAssemblies.Contains(args.Name))
            {
                string assemblyUrl = _webAssemblies[args.Name];
                WriteLine("Resolving Web Dependency " + args.Name);
                {
                    string[] parts = assemblyUrl.Split('/');
                    string filename = parts[parts.Length - 1];

                    WriteLine("URL " + assemblyUrl);
                    WriteLine("Filename " + filename);

                    byte[] bytes = new byte[1]; //1b, trying to stop any nulls
                    WWW www = new WWW(assemblyUrl);

                    float timeOut = Time.time + 10f; //I believe this is 10 seconds
                    bool failed = false;
                    while (!www.isDone)
                    {
                        if (Time.time > timeOut)
                        {
                            failed = true;
                            break;
                        }
                    }

                    if (failed && !www.isDone)
                    {
                        WriteLine("We failed to get the byte data in a reasonable time.");
                        WriteLine("Downloaded " + www.bytesDownloaded + "  bytes of " + www.size);
                        _failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        if (www.error.Length > 2)
                        {
                            WriteLine(www.error);
                        }
                        www.Dispose();
                        return null; //We failed to get the byte data in a reasonable time.
                    }

                    if (www != null && www.error != null && www.error.Length > 2)
                    {
                        WriteLine(www.error);
                        _failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }
                    WriteLine("Downloaded " + www.bytesDownloaded + "  bytes of " + www.size);

                    bytes = new byte[www.bytes.Length];
                    www.bytes.CopyTo(bytes, 0);
                    www.Dispose();


                    if (bytes == null || bytes.Length == 1)
                    {
                        WriteLine("NULL OR 1 BYTE ARRAY");
                        _failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }

                    File.WriteAllBytes(_cacheDir + Path.DirectorySeparatorChar + filename, bytes);
                    try
                    {
                        Assembly loaded = Assembly.Load(bytes);
                        if (loaded != null)
                        {
                            WriteLine("Sucessfully Resolved " + loaded.FullName);
                            return loaded;
                        }
                    }
                    catch (ArgumentNullException anEx)
                    {
                        //I hate nulls
                        _failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }
                    catch (BadImageFormatException bifEx)
                    {
                        //assemblyPath is not a valid assembly; for example, a 32-bit assembly in a 64-bit process. 
                        _failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }
                    catch (NullReferenceException nullex)
                    {
                        WriteLine(nullex.Message);
                        WriteLine(nullex.StackTrace);
                        _failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }
                }
            }

            #endregion

            //Default return if we dont recognise the assembly or we fall though to here if we load a null assembly.
            return null;
        }
    }
}
