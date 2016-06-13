using System;
namespace net.PeterCashel.DependencyResolver.Event
{
    public class ModStartupEventArgs : EventArgs
    {
        public string WorkingDir
        {
            private set; get;
        }

        public ModStartupEventArgs(string workingDir)
        {
            WorkingDir = workingDir;
        }

    }
}

