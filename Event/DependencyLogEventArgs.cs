using System;
namespace net.PeterCashel.DependencyResolver
{
	public class DependencyLogEventArgs: EventArgs
	{
		public string LogMessage
		{
			private set; get;
		}

		public DependencyLogEventArgs(string Text)
		{
			LogMessage = Text;
		}

	}
}

