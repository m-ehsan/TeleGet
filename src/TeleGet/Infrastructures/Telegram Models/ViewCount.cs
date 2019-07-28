using Infrastructures.ExtensionMethods;
using System;

namespace Infrastructures.Telegram
{
	public class CountTime
	{
		public int Count { get; set; }
		public int Time { get; set; }

		public CountTime(int count, DateTime time)
		{
			Count = count;
			Time = time.ToIntSeconds();
		}
	}
}
