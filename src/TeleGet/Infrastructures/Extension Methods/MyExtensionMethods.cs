using System;

namespace Infrastructures.ExtensionMethods
{
	public static class MyExtensionMethods
	{
		public static int ToIntSeconds(this DateTime date)
		{
			return (int)date.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds;
		}

		public static DateTime ToDateTime(this int seconds)
		{
			return new DateTime(1970, 1, 1, 0, 0, 0, 0).Add(TimeSpan.FromSeconds(seconds));
		}
	}
}
