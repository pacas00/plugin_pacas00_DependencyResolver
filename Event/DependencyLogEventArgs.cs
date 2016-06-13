using System;
namespace net.PeterCashel.DependencyResolver.Event
{
    public class DependencyLogEventArgs : EventArgs
    {
        public string LogMessage
        {
            private set; get;
        }

        public DependencyLogEventArgs(string text)
        {
            LogMessage = text;
        }

    }
}

