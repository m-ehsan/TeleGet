using System;
using TelegramInterface;

namespace TeleGet_CLI
{
	class TLInterfacePackage
	{
		public TLInterface Interface { get; set; }
		public DateTime LastCallTime { get; set; }
		public bool IsAvailable { get; set; }
	}
}
