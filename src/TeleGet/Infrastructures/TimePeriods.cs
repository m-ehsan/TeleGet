using System;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructures
{
	public class TimePeriods
	{
		public List<TimePeriod> _timePeriods;

		public TimePeriods()
		{
			_timePeriods = new List<TimePeriod>();
		}

		public TimePeriods(List<TimePeriod> timePeriods)
		{
			_timePeriods = new List<TimePeriod>();
			foreach (TimePeriod item in timePeriods)
			{
				AddPeriod(item);
			}
		}

		public void AddPeriod(TimePeriod timePeriod)
		{
			if (!_timePeriods.Any(p => p.Start <= timePeriod.Start && p.End >= timePeriod.End))
			{
				while (_timePeriods.Any(p => (p.Start >= timePeriod.Start && p.Start <= timePeriod.End) || (p.End >= timePeriod.Start && p.End <= timePeriod.End)))
				{
					TimePeriod timePeriod2 = _timePeriods.First(p => (p.Start >= timePeriod.Start && p.Start <= timePeriod.End) || (p.End >= timePeriod.Start && p.End <= timePeriod.End));
					_timePeriods.Remove(timePeriod2);
					if (timePeriod2.Start < timePeriod.Start)
					{
						timePeriod.Start = timePeriod2.Start;
					}
					else if (timePeriod2.End > timePeriod.End)
					{
						timePeriod.End = timePeriod2.End;
					}
				}
				_timePeriods.Add(timePeriod);
			}
		}

		public bool Contains(int time)
		{
			foreach (TimePeriod item in _timePeriods)
			{
				if (time >= item.Start && time <= item.End)
				{
					return true;
				}
			}
			return false;
		}

		public bool ContainsLarger(int time)
		{
			foreach (TimePeriod item in _timePeriods)
			{
				if (item.End > time)
				{
					return true;
				}
			}
			return false;
		}

		public int LargestNonIncludedTimeFromPeriod(int start, int end)
		{
			if (start > end)
			{
				int temp = end;
				end = start;
				start = temp;
			}
			if (!Contains(end))
			{
				return end;
			}
			try
			{
				return _timePeriods.Where(p => p.Start <= end).OrderByDescending(p => p.Start).First().Start - 1;
			}
			catch (Exception)
			{
				return -1;
			}
		}
	}
}
