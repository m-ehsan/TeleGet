using System;

namespace TeleGet_CLI
{
	class Job
	{
		public string Command { get; set; }
		public string Message { get; set; }
		public JobStatus Status { get; set; }
		public DateTime? FinishTime { get; set; }

		public Job(string command)
		{
			Command = command;
			Status = JobStatus.Waiting;
		}
	}
}
