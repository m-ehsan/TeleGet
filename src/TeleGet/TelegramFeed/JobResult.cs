namespace TeleGet_CLI
{
	class JobResult
	{
		public JobStatus Status { get; set; }
		public string Message { get; set; }

		public JobResult(JobStatus status)
		{
			Message = "";
			Status = status;
		}

		public JobResult(string message, JobStatus status)
		{
			Message = message;
			Status = status;
		}
	}
}
