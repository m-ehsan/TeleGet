using Infrastructures;
using Infrastructures.ExtensionMethods;
using Infrastructures.Telegram;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TelegramInterface;

namespace TeleGet_CLI
{
	class Program
	{
		private static bool _loop = true;
		private static bool _showLog = false;
		private static Feeder feeder;
		private static Thread mainThread;
		private static Thread logThread;

		private static void MainThreadAction()
		{
			while (_loop)
			{
				Thread.Sleep(1);
				if (feeder.ProcessingJobsCount < feeder.MaxConcurrentJobs)
				{
					feeder.ProcessNext();
				}
			}
		}

		private static void LogThreadAction()
		{
			string message = feeder.Message;
			while (_showLog && _loop)
			{
				Thread.Sleep(TimeSpan.FromMilliseconds(0.5));
				if (feeder.Message != message)
				{
					message = feeder.Message;
					Console.WriteLine(message);
				}
			}
		}

		private static void StartMainThread()
		{
			if (mainThread != null)
			{
				if (!mainThread.IsAlive)
				{
					mainThread = new Thread(new ThreadStart(MainThreadAction));
					mainThread.Start();
				}
			}
			else
			{
				mainThread = new Thread(new ThreadStart(MainThreadAction));
				mainThread.Start();
			}
		}

		static void Main(string[] args)
		{
			Console.WriteLine("initializing ...");
			Console.CancelKeyPress += Console_CancelKeyPress;
			feeder = new Feeder();
			//StartMainThread();
			while (_loop)
			{
				if (args.Length == 0)
				{
					Console.Write("> ");
					try
					{
						args = Console.ReadLine().Trim().Split(' ');
					}
					catch (Exception)
					{
						break;
					}
				}
				string[] newArgs = args.ToList().Skip(1).Take(args.Length - 1).ToArray();
				switch (args[0].ToLower())
				{
					case "activate":
						ActivateChannels(newArgs);
						break;
					case "add":
						AddChannels(newArgs);
						break;
					case "addsession":
						AddSession(newArgs).Wait();
						break;
					case "channels":
						break;
					case "cls":
						Console.Clear();
						break;
					case "deactivate":
						DeactivateChannels(newArgs);
						break;
					case "getfeed":
						GetFeed(newArgs);
						break;
					case "help":
						DisplayHelp();
						break;
					case "jobs":
						DisplayJobs();
						break;
					case "log":
						DisplayLog();
						break;
					case "newusernames":
						break;
					case "refresh":
						RefreshChannels(newArgs);
						break;
					case "search":
						break;
					case "sessions":
						DisplaySessions();
						break;
					case "stats":
						Stats(newArgs);
						break;
					case "exit":
						Exit();
						break;
					case "":
						break;
					default:
						Console.WriteLine("'{0}' is not recognized as a command.", args[0]);
						break;
				}
				args = new string[] { };
			}
		}

		private static void Exit()
		{
			_loop = false;
			Console.WriteLine("Preparing to exit.");
			if (feeder != null)
			{
				feeder.StopProcess();
			}
			if (mainThread != null)
			{
				while (mainThread.IsAlive) ;
			}
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			Exit();
		}

		private static async Task AddSession(string[] args)
		{
			string phoneNumber;
			if (args.Count() > 0)
			{
				phoneNumber = args[0].Trim();
			}
			else
			{
				Console.Write("Enter phone number: ");
				phoneNumber = Console.ReadLine().Trim();
				if (string.IsNullOrEmpty(phoneNumber))
				{
					Console.WriteLine("Session creation has been cancelled.");
					return;
				}
			}
			string sessionName = string.Format(@"sessions\{0}", DateTime.Now.ToIntSeconds());
			TLInterface tl = new TLInterface(sessionName);
			try
			{
				await tl.SendCodeAsync(phoneNumber);
			}
			catch (InvalidOperationException e)
			{
				Console.WriteLine("Error: {0}", e.Message);
				File.Delete(sessionName + ".dat");
				return;
			}
			Console.Write("Enter code: ");
			try
			{
				await tl.EnterCodeAsync(Console.ReadLine().Trim());
			}
			catch (PasswordRequiredException)
			{
				Console.Write("Enter your cloud password: ");
				try
				{
					await tl.EnterCloudPasswordAsync(Console.ReadLine().Trim());
				}
				catch (InvalidOperationException e)
				{
					Console.WriteLine("Error: {0}", e.Message);
					File.Delete(sessionName + ".dat");
					return;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Error: {0}", e.Message);
				File.Delete(sessionName + ".dat");
				return;
			}
			if (tl.IsAuthorized)
			{
				Console.WriteLine("Session added successfully.");
				feeder.InitializeTLInterfaces();
			}
			else
			{
				Console.WriteLine("Error.");
				File.Delete(sessionName + ".dat");
			}
		}

		private static void DisplayJobs()
		{
			Console.WriteLine("Processing jobs:");
			Job[] jobs = feeder.GetJobs(5);
			string[] processingJobs = jobs.Where(j => j.Status == JobStatus.Processing).Select(j => j.Command).ToArray();
			if (processingJobs.Length == 0)
			{
				Console.WriteLine("\t- no job in process -");
			}
			else
			{
				foreach (string job in processingJobs)
				{
					Console.WriteLine("\t- {0}", job);
				}
			}
			Console.WriteLine("\nWaiting jobs:");
			string[] waitingJobs = jobs.Where(j => j.Status == JobStatus.Waiting).Select(j => j.Command).ToArray();
			foreach (string job in waitingJobs)
			{
				Console.WriteLine("\t- {0}", job);
			}
			Console.WriteLine("\nRecently finished jobs:");
			Job[] finishedJobs = jobs.Where(j => j.Status == JobStatus.Finished).ToArray();
			foreach (Job job in finishedJobs)
			{
				Console.WriteLine("\t- '{0}'\n\t\tresult: {1}\n\t\ttime: {2}", job.Command, job.Message, job.FinishTime);
			}
			Console.WriteLine();
		}

		private static void DisplaySessions()
		{
			Session[] sessions = feeder.GetSessions();
			Console.WriteLine("Active sessions:");
			foreach (Session item in sessions)
			{
				Console.WriteLine("\t- phone:+{0} username:@{1}", item.PhoneNumber, item.Username);
			}
			Console.WriteLine();
		}

		private static void ActivateChannels(string[] args)
		{
			string message = feeder.ActivateChannels(args);
			Console.WriteLine(message);
		}

		private static void AddChannels(string[] args)
		{
			string message = feeder.AddChannels(args);
			Console.WriteLine(message);
		}

		private static void DeactivateChannels(string[] args)
		{
			string message = feeder.DeactivateChannels(args);
			Console.WriteLine(message);
		}

		private static void GetFeed(string[] args)
		{
			string message = feeder.GetFeed(args);
			Console.WriteLine(message);
		}

		private static void DisplayHelp()
		{
			Console.WriteLine("\tadd\t\t\tadd channels by username.");
			Console.WriteLine("\tactivate\t\tmark channels as active.");
			Console.WriteLine("\tdeactivate\t\tmark channels as deactive.");
			// to do ...
			Console.WriteLine();
		}

		private static void DisplayLog()
		{
			_showLog = true;
			if (logThread != null)
			{
				if (!logThread.IsAlive)
				{
					logThread = new Thread(new ThreadStart(LogThreadAction));
					logThread.Start();
				}
			}
			else
			{
				logThread = new Thread(new ThreadStart(LogThreadAction));
				logThread.Start();
			}
			while (true)
			{
				ConsoleKeyInfo key = Console.ReadKey(true);
				if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
				{
					_showLog = false;
					break;
				}
			}
		}

		private static void RefreshChannels(string[] args)
		{
			string message = feeder.RefreshChannels(args);
			Console.WriteLine(message);
		}

		private static void Stats(string[] args)
		{
			string message = feeder.Stats(args);
			Console.WriteLine(message);
		}
	}
}
