using System;
using System.IO;
using System.Reflection;
using net.PeterCashel.DependencyResolver.Event;
using net.PeterCashel.DependencyResolver.Plugins;
using UnityEngine;

namespace net.PeterCashel.DependencyResolver
{
    public delegate void StartupEventHandler(object source, ModStartupEventArgs e);

    public class PluginPacas00DependencyResolver : FortressCraftMod
    {
        public PluginPacas00DependencyResolver()
        {
            //Does get called, register static events here.

        }

        /// <summary>
        /// Register against this event to receive logging information from the resolver. Once someone registers, it stops logging to Debug.Log
        /// </summary>
        public static event StartupEventHandler StartupEvent;

        public void Start()
        {
            const string modName = "Pacas00's Dependency Resolver";
            const string modId = "pacas00.aaDependencyResolver";
            var workingDir = "";

            DependencyResolver.LogEvent += logHandler;
            StartupEvent += DependencyResolver.StartupEvent;
            StartupEvent += NUPKGPackageHandler.StartupEvent;

            foreach (ModConfiguration current in ModManager.mModConfigurations.Mods)
            {
                if (current.Id.Contains(modId) || current.Name == modName)
                {
                    workingDir = current.Path;
                    break;
                }
            }
            if (StartupEvent != null)
                StartupEvent.Invoke(new object(), new ModStartupEventArgs(workingDir));
        }

        static void logHandler(object source, DependencyLogEventArgs e)
        {
            Debug.LogWarning(e.LogMessage);
        }
    }
}