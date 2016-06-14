using System;
using System.IO;
using System.Reflection;
using Ionic.Zip;
using net.PeterCashel.DependencyResolver.Event;
using UnityEngine;

namespace net.PeterCashel.DependencyResolver.Plugins
{
    public static class NUPKGPackageHandler
    {
        private static string _packageDir = "";
        static bool _initComplete = false;
        private static ConcurrentQueue<string[]> _processingQueue = new ConcurrentQueue<string[]>();

        private static void DoInit()
        {
            if (_initComplete) return;
            _initComplete = true;

            while (!_processingQueue.IsEmpty)
            {
                string[] queueStrings;
                if (!_processingQueue.TryDequeue(out queueStrings)) continue;
                AddNuPkg(queueStrings[0], queueStrings[1]);
            }
        }

        //Fired by mod class when its time to startup.
        public static void StartupEvent(object source, ModStartupEventArgs e)
        {
            Directory.CreateDirectory(e.WorkingDir + Path.DirectorySeparatorChar + "cache" + Path.DirectorySeparatorChar + "nupkg");
            _packageDir = (e.WorkingDir + Path.DirectorySeparatorChar + "cache" + Path.DirectorySeparatorChar + "nupkg");
            DoInit();
        }

        /// <summary>
        /// Add a NuPkg to be processed. Will automatically try to find the libs in the package.
        /// </summary>
        /// <param name="filePathToNupkg">the file path to the nupkg file</param>
        public static void AddNuPkg(string filePathToNupkg)
        {
            AddNuPkg(filePathToNupkg, null);
        }

        /// <summary>
        /// Add a NuPkg to be processed, Note, the internal path expects no preceding or trailing directory seperator.
        /// </summary>
        /// <param name="filePathToNupkg">the file path to the nupkg file</param>
        /// <param name="internalPath">the internal path in the nupkg to the dll libraries for .net 3.5</param>
        public static void AddNuPkg(string filePathToNupkg, string internalPath)
        {
            if (!_initComplete) _processingQueue.Enqueue(new string[2] { filePathToNupkg, internalPath });
            else ProcessNuPkg(filePathToNupkg, internalPath);
        }

        private static void ProcessNuPkg(string filePathToNupkg, string internalPath)
        {
            bool searchForDll = internalPath == null;
            string[] parts = filePathToNupkg.Split('\\');
            string filename = parts[parts.Length - 1];
            string libFolder = _packageDir + Path.DirectorySeparatorChar + filename.Substring(0, filename.LastIndexOf('.'));

            if (!Directory.Exists(libFolder))
            {
                Directory.CreateDirectory(libFolder);
                Ionic.Zip.ZipFile pkg = new ZipFile(_packageDir + Path.DirectorySeparatorChar + filename);
                pkg.ExtractAll(libFolder);
            }

            if (!searchForDll)
            {
                if (Directory.Exists(libFolder + Path.DirectorySeparatorChar + internalPath + Path.DirectorySeparatorChar))
                {
                    string[] files = Directory.GetFiles(libFolder + Path.DirectorySeparatorChar + internalPath + Path.DirectorySeparatorChar, "*.dll");
                    foreach (var filepath in files)
                    {
                        try
                        {
                            Assembly assembly = Assembly.LoadFile(filepath);
                            DependencyResolver.AddAssemblyFromFile(assembly.FullName, filepath);

                            WriteLine("Adding locally cached DLL " + filepath);

                        }
                        catch (BadImageFormatException bifEx)
                        {
                            //get rid of damaged DLL
                            WriteLine("Deleting Damaged DLL " + filepath);
                            File.Delete(filepath);
                        }
                    }
                }
            }
            else
            {
                string[] paths = new string[] { "lib\\net35-client", "lib\\net35", "lib\\35-client", "lib\\35", "net35-client", "net35", "35-client", "35" };
                foreach (var dirPath in paths)
                {
                    if (Directory.Exists(libFolder + Path.DirectorySeparatorChar + dirPath))
                    {
                        string[] files = Directory.GetFiles(libFolder + Path.DirectorySeparatorChar + dirPath, "*.dll");
                        foreach (var filepath in files)
                        {
                            try
                            {
                                Assembly assembly = Assembly.LoadFile(filepath);
                                DependencyResolver.AddAssemblyFromFile(assembly.FullName, filepath);

                                WriteLine("Adding locally cached DLL " + filepath);

                            }
                            catch (BadImageFormatException bifEx)
                            {
                                //get rid of damaged DLL
                                WriteLine("Deleting Damaged DLL " + filepath);
                                File.Delete(filepath);
                            }
                        }
                    }
                }
            }
        }

        public static void AddNuPkgFromUrl(string name, string url)
        {
            AddNuPkgFromUrl(name, url, (string)null);
        }


        /// <summary>
        /// Add a NuPkg to be processed from the web, Note, the optional internalPath expects no preceding or trailing directory seperator.
        /// 
        /// Overload: internalPath is a string[]. Will be assembled into a string with platform specific DirectorySeparatorChar
        /// </summary>
        /// <param name="url">URL to download the package from</param>
        /// <param name="internalPath">the internal path in the nupkg to the dll libraries for .net 3.5</param>
        public static void AddNuPkgFromUrl(string name, string url, string[] internalPath)
        {
            string path = String.Join(Path.DirectorySeparatorChar.ToString(), internalPath);
            AddNuPkgFromUrl(name, url, path);
        }

        /// <summary>
        /// Add a NuPkg to be processed from the web, Note, the optional internalPath expects no preceding or trailing directory seperator.
        /// </summary>
        /// <param name="url">URL to download the package from</param>
        /// <param name="internalPath">the internal path in the nupkg to the dll libraries for .net 3.5</param>
        public static void AddNuPkgFromUrl(string name, string url, string internalPath)
        {
            string[] parts = url.Split('/');
            string filename = name;
            WriteLine(name);
            WriteLine(url);
            WriteLine(internalPath);

            if (!File.Exists(_packageDir + Path.DirectorySeparatorChar + filename))
            {
                byte[] bytes = new byte[1]; //1b, trying to stop any nulls

                WWW www = new WWW(url);
                while (!www.isDone)
                {
                    //Wait
                }

                if (www != null && www.error != null && www.error.Length > 2)
                {
                    WriteLine(www.error);
                }

                bytes = new byte[www.bytes.Length];
                www.bytes.CopyTo(bytes, 0);
                www.Dispose();

                if (bytes == null || bytes.Length == 1)
                {
                    WriteLine("NULL OR 1 BYTE ARRAY");
                }

                File.WriteAllBytes(_packageDir + Path.DirectorySeparatorChar + filename, bytes);
            }
            AddNuPkg(_packageDir + Path.DirectorySeparatorChar + filename, internalPath);
        }


        public static void WriteLine(string s)
        {
            DependencyResolver.WriteLine(s);
        }
    }
}

