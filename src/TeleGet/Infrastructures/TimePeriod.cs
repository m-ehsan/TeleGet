namespace Infrastructures
{
	public class TimePeriod
	{
		public int Start { get; set; }
		public int End { get; set; }

		public TimePeriod(int start, int end)
		{
			Start = start;
			End = end;
		}
	}
}
