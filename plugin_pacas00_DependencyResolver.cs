using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace net.PeterCashel.DependencyResolver
{
	public class plugin_pacas00_DependencyResolver: FortressCraftMod
	{
		public plugin_pacas00_DependencyResolver()
		{
		}


		public void Start()
		{
			string modName = "Pacas00's Dependency Resolver";
			string modId = "pacas00.aaDependencyResolver";

			string workingDir = "";

			foreach(ModConfiguration current in ModManager.mModConfigurations.Mods)
			{
				if(current.Id.Contains(modId) || current.Name == modName)
				{
					workingDir = current.Path;
					break;
				}
			}

			DependencyResolver.logEvent += logHandler;


			Directory.CreateDirectory(workingDir + Path.DirectorySeparatorChar + "lib");


			DependencyResolver.Setup(workingDir + Path.DirectorySeparatorChar + "lib");


			//if(DependencyResolver.isReadyForRegistration)
			//{
			//	//add libs
			//}
			//else {
			//	DependencyResolver.registerEvent += registrationevent;
			//	//add libs when ready.
			//}

		}


		static void logHandler(object source, DependencyLogEventArgs e)
		{
			Debug.LogWarning(e.LogMessage);
		}

		//static void registrationevent(object source, EventArgs e)
		//{
		//	//Will be fired when ready
		//}
	}



}
