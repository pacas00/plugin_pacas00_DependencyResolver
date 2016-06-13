using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace net.PeterCashel.DependencyResolver
{

    public delegate void DependencyLogHandler(object source, DependencyLogEventArgs e);
    public delegate void RegisterAssembliesHandler(object source, EventArgs e);


    public class DependencyResolver
    {
        private static bool useCache = true;


        /// <summary>
        /// Register against this event to receive logging information from the resolver. Once someone registers, it stops logging to Debug.Log
        /// </summary>
        public static event DependencyLogHandler logEvent;

        /// <summary>
        /// Register against this event to be notifed when the DependencyResolver is ready for assemby registration
        /// </summary>
        public static event RegisterAssembliesHandler registerEvent;

        private static Dictionary<string, string> knownAssemblies = new Dictionary<string, string>();
        private static Dictionary<string, string> webAssemblies = new Dictionary<string, string>();
        private static List<string> failedAssemblies = new List<string>(); //If we fail to get them, don't try again.

        private static string cacheDir = "";


        public DependencyResolver()
        {
            isReadyForRegistration = false;
        }

        public static void Setup()
        {
            Setup(null);
        }

        public static void Setup(string localCache)
        {
            if (localCache != null)
            {
                cacheDir = localCache;
            }
            else {
                useCache = false;
            }

            //Register this classes AssemblyResolve event handler in the current app domain
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            if (useCache) ResolveLocalLibs();

            isReadyForRegistration = true;

            registerEvent.Invoke(null, new EventArgs());
        }

        public static bool isReadyForRegistration
        {
            get; private set;
        }

        static void WriteLine(string v)
        {

#if DEBUG
            if (logEvent.GetInvocationList().Length == 0) Debug.Log(v);
#endif

            logEvent.Invoke(null, new DependencyLogEventArgs(v));
        }

        private static void ResolveLocalLibs()
        {
            DirectoryInfo dInfo = new DirectoryInfo(cacheDir);

            foreach (FileInfo file in dInfo.GetFiles())
            {
                try
                {
                    Assembly assembly = Assembly.LoadFile(file.FullName);
                    AddAssemblyFromFile(assembly.FullName, file.FullName);

                    WriteLine("Adding locally cached DLL " + file.Name);

                }
                catch (BadImageFormatException BIFEx)
                {
                    //get rid of damaged DLL
                    WriteLine("Deleting Damaged DLL " + file.Name);
                    file.Delete();
                }


            }
        }

        //Local Method for adding cached files.
        private static void AddAssemblyFromFile(string assemblyName, string assemblyPath)
        {
            WriteLine("Adding File Dependency " + assemblyName);
            knownAssemblies.Add(assemblyName, assemblyPath);
        }

        /// <summary>
        /// Adds the assembly from web. http://, https:// and file:// protocols are supported
        /// </summary>
        /// <returns>The Assembly from web.</returns>
        /// <param name="assemblyName">Assembly name.</param>
        /// <param name="assemblyURL">Assembly URL.</param>
        public static void AddAssemblyFromWeb(string assemblyName, string assemblyURL)
        {
            WriteLine("Adding Web Dependency " + assemblyName);
            webAssemblies.Add(assemblyName, assemblyURL);
        }

        public static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            #region File DLL
            if (knownAssemblies.ContainsKey(args.Name))
            {
                string assemblyPath = knownAssemblies[args.Name];
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
            if (webAssemblies.ContainsKey(args.Name) && !failedAssemblies.Contains(args.Name))
            {
                string assemblyURL = webAssemblies[args.Name];

                WriteLine("Resolving Web Dependency " + args.Name);
                {

                    string[] parts = assemblyURL.Split('/');

                    string filename = parts[parts.Length - 1];

                    WriteLine("URL " + assemblyURL);
                    WriteLine("Filename " + filename);

                    byte[] bytes = new byte[1]; //1b, trying to stop any nulls


                    WWW www = new WWW(assemblyURL);

                    float timeOut = Time.time + 10f; //I believe this is 10 seconds
                    bool failed = false;
                    while (!www.isDone)
                    {
                        if (Time.time > timeOut) { failed = true; break; }
                    }

                    if (failed && !www.isDone)
                    {
                        WriteLine("We failed to get the byte data in a reasonable time.");
                        WriteLine("Downloaded " + www.bytesDownloaded + "  bytes of " + www.size);
                        failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
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
                        failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }
                    WriteLine("Downloaded " + www.bytesDownloaded + "  bytes of " + www.size);

                    bytes = new byte[www.bytes.Length];
                    www.bytes.CopyTo(bytes, 0);

                    www.Dispose();



                    if (bytes == null || bytes.Length == 1)
                    {
                        WriteLine("NULL OR 1 BYTE ARRAY");
                        failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }

                    File.WriteAllBytes(cacheDir + Path.DirectorySeparatorChar + filename, bytes);

                    try
                    {
                        Assembly loaded = Assembly.Load(bytes);
                        if (loaded != null)
                        {
                            WriteLine("Sucessfully Resolved " + loaded.FullName);
                            return loaded;
                        }
                    }
                    catch (ArgumentNullException ANEx)
                    {
                        //I hate nulls
                        failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }
                    catch (BadImageFormatException BIFEx)
                    {
                        //assemblyPath is not a valid assembly; for example, a 32-bit assembly in a 64-bit process. 
                        failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }
                    catch (NullReferenceException nullex)
                    {
                        WriteLine(nullex.Message);
                        WriteLine(nullex.StackTrace);
                        failedAssemblies.Add(args.Name); //we tried, we failed, don't try again
                        return null;
                    }

                }

            }
            #endregion

            //Default return if we dont recognise the assembly or we fall though to here if we load a null assembly.
            return null;
        }

        public static void CopyStream(Stream input, Stream output)
        {
            byte[] b = new byte[32768];
            int r;
            while ((r = input.Read(b, 0, b.Length)) > 0)
                output.Write(b, 0, r);
        }
    }
}
