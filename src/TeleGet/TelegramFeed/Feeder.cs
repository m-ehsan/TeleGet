using Infrastructures;
using Infrastructures.ExtensionMethods;
using Infrastructures.MongoDB_Models;
using Infrastructures.Telegram;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TelegramInterface;

namespace TeleGet_CLI
{
	class Feeder
	{
		private const string _configFilePath = "feeder.cfg";
		private const string _sessionsDirectoryPath = "sessions\\";
		private string _dbAddress = "";
		private int _apiCallDelay;
		private int _maxConcurrentJobs;
		public int MaxConcurrentJobs { get { return _maxConcurrentJobs; } }
		public int ProcessingJobsCount
		{
			get
			{
				lock (_myLock)
				{
					return _jobs.Where(j => j.Status == JobStatus.Processing).Count();
				}
			}
		}
		private List<TLInterfacePackage> _tlInterfacePackages;
		private List<Job> _jobs;
		private bool _stop;
		public string Message { get; private set; }
		private object _myLock;
		private object _logLock;
		private MongoDBInterface db;

		public Feeder()
		{
			_myLock = new object();
			_logLock = new object();
			_tlInterfacePackages = new List<TLInterfacePackage>();
			_jobs = new List<Job>();
			Message = "";
			_maxConcurrentJobs = 1;
			_apiCallDelay = 750;
			LoadConfigs();
			db = new MongoDBInterface(_dbAddress);
			InitializeTLInterfaces();
			ProcessNext();
		}

		#region General
		public void InitializeTLInterfaces()
		{
			string[] sessions = Directory.GetFiles(_sessionsDirectoryPath);
			List<string> unAuthorizedSessions = new List<string>();
			foreach (var sessionFile in sessions)
			{
				string session;
				if (sessionFile.EndsWith(".dat"))
				{
					session = sessionFile.Substring(0, sessionFile.Length - 4);
				}
				else
				{
					session = sessionFile;
				}
				if (!_tlInterfacePackages.Any(p => p.Interface.SessionPath == session))
				{
					TLInterface tLInterface = new TLInterface(session);
					if (tLInterface.IsAuthorized)
					{
						_tlInterfacePackages.Add(new TLInterfacePackage()
						{
							Interface = tLInterface,
							IsAvailable = true,
							LastCallTime = DateTime.Now
						});
					}
					else
					{
						unAuthorizedSessions.Add(session);
					}
				}
			}
			foreach (var session in unAuthorizedSessions)
			{
				File.Delete(session + ".dat");
			}
		}

		private TLInterfacePackage GetAvailableTLInterface()
		{
			TLInterfacePackage tLInterfacePackage = null;
			lock (_myLock)
			{
				while (tLInterfacePackage == null)
				{
					try
					{
						tLInterfacePackage = _tlInterfacePackages.First(i => i.IsAvailable == true && i.LastCallTime.Add(TimeSpan.FromMilliseconds(_apiCallDelay)) < DateTime.Now);
					}
					catch (InvalidOperationException)
					{
						tLInterfacePackage = null;
					}
				}
				tLInterfacePackage.IsAvailable = false;
			}
			return tLInterfacePackage;
		}

		private string BuildCommandString(string cmd, string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				cmd += " " + args[i];
			}
			return cmd;
		}

		private void AddJob(string cmd, bool now = false)
		{
			lock (_myLock)
			{
				_jobs.Add(new Job(cmd));
				UpdateConfigsFile();
				if (now)
				{
					Job todo;
					lock (_myLock)
					{
						todo = _jobs.First(j => j.Status == JobStatus.Waiting && j.Command == cmd);
						todo.Status = JobStatus.Processing;
					}
					Task.Run(() => ProcessCommand(todo.Command));
				}
				ProcessNext();
			}
		}

		private bool HasWaitingJobs()
		{
			lock (_myLock)
			{
				return _jobs.Any(j => j.Status == JobStatus.Waiting);
			}
		}

		public Job[] GetJobs(int recentlyFinishedCount)
		{
			lock (_myLock)
			{
				return _jobs.Where(j => j.Status != JobStatus.Finished).Concat(_jobs.Where(j => j.Status == JobStatus.Finished).OrderByDescending(j => j.FinishTime).Take(recentlyFinishedCount)).ToArray();
			}
		}

		public Session[] GetSessions()
		{
			List<Session> sessions = new List<Session>();
			foreach (TLInterfacePackage item in _tlInterfacePackages)
			{
				sessions.Add(new Session()
				{
					FirstName = item.Interface.CurrentUser.FirstName,
					LastName = item.Interface.CurrentUser.LastName,
					Username = item.Interface.CurrentUser.Username,
					PhoneNumber = item.Interface.CurrentUser.Phone,
				});
			}
			return sessions.ToArray();
		}

		private void Log(string s)
		{
			lock (_logLock)
			{
				Message = string.Format("- {0} [{1:H:mm:ss.fff}]", s, DateTime.Now);
				Thread.Sleep(3);
			}
		}

		public void StopProcess()
		{
			_stop = true;
		}
		#endregion

		#region ConfigsFile
		/// <summary>
		/// Loads configurations from file
		/// </summary>
		private void LoadConfigs()
		{
			if (!File.Exists(_configFilePath))
			{
				CreateConfigsFile();
			}
			string[] lines = File.ReadAllLines(_configFilePath, Encoding.UTF8);
			foreach (string line in lines)
			{
				string[] words = line.Split(new char[] { ' ' }, 2);
				switch (words[0].ToLower())
				{
					case "db":
						UpdateDatabaseAddress(words[1]);
						break;
					case "apicalldelay":
						UpdateApiCallDelay(words[1]);
						break;
					case "maxconcurrentjobs":
						UpdateMaxConcurrentJobs(words[1]);
						break;
					case "pendingjobs":
						UpdatePendingJobs(words[1]);
						break;
					default:
						break;
				}
			}
		}

		private void CreateConfigsFile()
		{
			FileStream fs = File.Create(_configFilePath);
			fs.Close();
			UpdateConfigsFile();
		}
		private void UpdateConfigsFile()
		{
			if (!File.Exists(_configFilePath))
			{
				CreateConfigsFile();
			}
			File.WriteAllText(_configFilePath, CreateConfigsFileText());
		}

		private string CreateConfigsFileText()
		{
			string cfg = "";
			cfg += string.Format("db {0}\n", _dbAddress);
			cfg += string.Format("apiCallDelay {0}\n", _apiCallDelay);
			cfg += string.Format("maxConcurrentJobs {0}\n", _maxConcurrentJobs);
			string pendings = "";
			foreach (var item in _jobs.Where(j => j.Status != JobStatus.Finished))
			{
				pendings += item.Command + ";";
			}
			cfg += string.Format("pendingJobs {0}", pendings);
			return cfg;
		}

		private void UpdatePendingJobs(string s)
		{
			_jobs.Clear();
			s = s.Trim();
			var jobs = s.Split(';');
			foreach (var job in jobs)
			{
				if (!string.IsNullOrEmpty(job))
				{
					_jobs.Add(new Job(job));
				}
			}
		}

		private void UpdateDatabaseAddress(string dbAddress)
		{
			dbAddress = dbAddress.Trim();
			if (dbAddress.StartsWith("\"") && dbAddress.EndsWith("\""))
			{
				dbAddress = dbAddress.Substring(1);
				dbAddress = dbAddress.Substring(0, dbAddress.Length - 1);
			}
			_dbAddress = dbAddress;
		}

		private void UpdateApiCallDelay(string s)
		{
			s = s.Trim();
			if (s.StartsWith("\"") && s.EndsWith("\""))
			{
				s = s.Substring(1);
				s = s.Substring(0, s.Length - 1);
			}
			if (int.TryParse(s, out int delay))
			{
				_apiCallDelay = delay;
			}
		}

		private void UpdateMaxConcurrentJobs(string s)
		{
			s = s.Trim();
			if (s.StartsWith("\"") && s.EndsWith("\""))
			{
				s = s.Substring(1);
				s = s.Substring(0, s.Length - 1);
			}
			if (int.TryParse(s, out int count))
			{
				_maxConcurrentJobs = count;
			}
		}
		#endregion

		#region Processing
		public void ProcessNext()
		{
			lock (this)
			{
				if (HasWaitingJobs())
				{
					if (ProcessingJobsCount < _maxConcurrentJobs)
					{
						Job todo;
						lock (_myLock)
						{
							todo = _jobs.First(j => j.Status == JobStatus.Waiting);
							todo.Status = JobStatus.Processing;
						}
						Task.Run(() => ProcessCommand(todo.Command));
					}
				}
			}
		}

		private async Task ProcessCommand(string cmd)
		{
			string[] cmdArgs = cmd.Split(' ');
			string[] newArgs = cmdArgs.ToList().Skip(1).Take(cmdArgs.Length - 1).ToArray();
			JobResult result;
			switch (cmdArgs[0].ToLower())
			{
				case "activate":
					result = ProcessActivateCommand(newArgs);
					break;
				case "add":
					result = await ProcessAddCommand(newArgs);
					break;
				case "deactivate":
					result = ProcessDeactivateCommand(newArgs);
					break;
				case "getfeed":
					result = await ProcessGetFeedCommand(newArgs);
					break;
				case "refresh":
					result = await ProcessRefreshCommand(newArgs);
					break;
				default:
					result = new JobResult(JobStatus.Stopped);
					break;
			}
			lock (_myLock)
			{
				Job currentJob = _jobs.First(j => j.Command == cmd && j.Status == JobStatus.Processing);
				currentJob.Message = result.Message;
				if (result.Status == JobStatus.Finished)
				{
					currentJob.Status = JobStatus.Finished;
					currentJob.FinishTime = DateTime.Now;
					UpdateConfigsFile();
				}
				else
				{
					currentJob.Status = JobStatus.Waiting;
				}
			}
			Log(result.Message);
			lock (_myLock)
			{
				ProcessNext();
			}
		}

		private JobResult ProcessActivateCommand(string[] args)
		{
			int count = 0;
			bool stopped = false;
			foreach (string username in args)
			{
				if (_stop)
				{
					stopped = true;
					break;
				}
				if (!username.StartsWith("-"))
				{
					MongoChannel channel = db.GetChannel(username);
					if (channel != null)
					{
						if (!channel.IsActive)
						{
							db.UpdateChannelActivationStatus(channel.ChannelId, true);
							count++;
							Log(string.Format("Channel '{0}' (@{1}) with {2} members has been activated.", channel.Title, channel.Username, channel.ParticipantsCounts.Last().Count));
						}
					}
				}
			}
			if (stopped)
			{
				return new JobResult(JobStatus.Stopped);
			}
			string message = "";
			if (count == 0)
			{
				message = "No channel has been activated.";
			}
			else if (count == 1)
			{
				message = "1 channel has been activated.";
			}
			else
			{
				message = string.Format("{0} channels has been activated.", count);
			}
			return new JobResult(message, JobStatus.Finished);
		}

		private async Task<JobResult> ProcessAddCommand(string[] args)
		{
			int count = 0;
			bool active = false;
			bool stopped = false;
			foreach (string item in args)
			{
				if (item.ToLower() == "-active")
				{
					active = true;
				}
			}
			foreach (string username in args)
			{
				if (_stop)
				{
					stopped = true;
					break;
				}
				if (!username.StartsWith("-"))
				{
					if (await AddChannelHelper(username, active))
					{
						count++;
					}
				}
			}
			if (stopped)
			{
				return new JobResult(JobStatus.Stopped);
			}
			string message = "";
			if (count == 0)
			{
				message = "No new channel has been added.";
			}
			else if (count == 1)
			{
				message = "1 new channel has been added.";
			}
			else
			{
				message = string.Format("{0} new channels has been added.", count);
			}
			return new JobResult(message, JobStatus.Finished);
		}

		private JobResult ProcessDeactivateCommand(string[] args)
		{
			int count = 0;
			bool stopped = false;
			foreach (string username in args)
			{
				if (_stop)
				{
					stopped = true;
					break;
				}
				if (!username.StartsWith("-"))
				{
					MongoChannel channel = db.GetChannel(username);
					if (channel != null)
					{
						if (channel.IsActive)
						{
							db.UpdateChannelActivationStatus(channel.ChannelId, false);
							count++;
							Log(string.Format("Channel '{0}' (@{1}) with {2} members has been deactivated.", channel.Title, channel.Username, channel.ParticipantsCounts.Last().Count));
						}
					}
				}
			}
			if (stopped)
			{
				return new JobResult(JobStatus.Stopped);
			}
			string message = "";
			if (count == 0)
			{
				message = "No channel has been deactivated.";
			}
			else if (count == 1)
			{
				message = "1 channel has been deactivated.";
			}
			else
			{
				message = string.Format("{0} channels has been deactivated.", count);
			}
			return new JobResult(message, JobStatus.Finished);
		}

		private async Task<JobResult> ProcessGetFeedCommand(string[] args)
		{
			int totalMessagesCount = 0;
			int from = DateTime.Now.ToIntSeconds();
			int to = DateTime.Now.ToIntSeconds();
			bool stopped = false;
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i].ToLower() == "-from")
				{
					try
					{
						from = DateTime.Parse(args[i + 1]).ToIntSeconds();
					}
					catch (Exception)
					{
						return new JobResult("Invalid date after '-from'", JobStatus.Finished);
					}
				}
				if (args[i].ToLower() == "-to")
				{
					try
					{
						to = DateTime.Parse(args[i + 1]).ToIntSeconds();
					}
					catch (Exception)
					{
						return new JobResult("Invalid date after '-to'", JobStatus.Finished);
					}
				}
			}
			List<int> channelsIDs = db.GetActiveChannelsIDs();
			foreach (int channelId in channelsIDs)
			{
				if (_stop)
				{
					stopped = true;
					break;
				}
				MongoChannel channel = db.GetChannel(channelId);
				if (channel.CoveredTimePeriods == null)
				{
					channel.CoveredTimePeriods = new TimePeriods();
				}
				int currentChannelMessagesCount = 0;
				bool skipChannel = false;
				int time = to;
				int lastMessageTime = 0;
				if (channel.CoveredTimePeriods.ContainsLarger(time))
				{
					lastMessageTime = time;
				}
				while (true)
				{
					if (_stop)
					{
						stopped = true;
						break;
					}
					if (time < from)
					{
						break;
					}
					time = channel.CoveredTimePeriods.LargestNonIncludedTimeFromPeriod(from, time);
					if (time < from || skipChannel)
					{
						break;
					}
					int addedMessagesCount = 0;
					List<Message> messages = new List<Message>();
					TLInterfacePackage tl = new TLInterfacePackage();
					tl = GetAvailableTLInterface();
					try
					{
						messages = await tl.Interface.GetChannelMessagesAsync(channel.ChannelId, channel.AccessHash.Value, null, time, null);
						tl.LastCallTime = DateTime.Now;
						tl.IsAvailable = true;
					}
					catch (InvalidOperationException e)
					{
						Log("InvalidOperationException: " + e.Message);
						return new JobResult(JobStatus.Stopped);
					}
					if (messages.Count == 0)
					{
						break;
					}
					foreach (Message item in messages)
					{
						if (!db.IsMessageAdded(item.SourceChannel.ChannelId, item.MessageId))
						{
							// extract usernames from message text
							foreach (string username in ExtractUsernamesHelper(item.Text))
							{
								if (!db.IsChannelAdded(username))
								{
									if (!db.IsUnresolvedUsernameAdded(new MongoUsername { Username = username }))
									{
										db.AddUnresolvedUsername(new MongoUsername { Username = username });
									}
								}
							}
							// add message source channel if it's not added yet.
							if (!db.IsChannelAdded(item.SourceChannel.ChannelId))
							{
								if (item.SourceChannel.AccessHash.HasValue)
								{
									await AddChannelHelper(item.SourceChannel.ChannelId, item.SourceChannel.AccessHash.Value, false);
								}
							}
							// add message to database
							MongoMessage messageToAdd = new MongoMessage
							{
								ChannelId = item.SourceChannel.ChannelId,
								MessageId = item.MessageId,
								Text = item.Text,
								Date = item.OriginalDate ?? item.Date,
								Views = new List<CountTime>() { item.Views }
							};
							if (item.MediaContent != null)
							{
								if (string.IsNullOrEmpty(messageToAdd.Text))
								{
									messageToAdd.Text = item.MediaContent.Caption;
								}
								messageToAdd.MediaType = item.MediaContent.Type;
								messageToAdd.MediaSize = item.MediaContent.Size;
								messageToAdd.FileName = item.MediaContent.FileName;
							}
							db.AddMessage(messageToAdd);
							addedMessagesCount++;
						}
						else if (messages.Count == 1)
						{
							skipChannel = true;
						}
					}
					if (lastMessageTime == 0)
					{
						lastMessageTime = messages.Max(m => m.Date);
					}
					time = messages.Min(m => m.Date);
					channel.CoveredTimePeriods.AddPeriod(new TimePeriod(time, lastMessageTime));
					db.UpdateChannelCoveredTimePeriods(channel.ChannelId, channel.CoveredTimePeriods);
					currentChannelMessagesCount += addedMessagesCount;
					Log(string.Format("{0} new messages has been added from '{1}' (@{2}).", addedMessagesCount, channel.Title, channel.Username));
					lastMessageTime = time;
				}
				totalMessagesCount += currentChannelMessagesCount;
				Log(string.Format("{0} total new messages has been added from '{1}' (@{2}).", currentChannelMessagesCount, channel.Title, channel.Username));
			}
			if (stopped)
			{
				return new JobResult(JobStatus.Stopped);
			}
			string message = "";
			if (totalMessagesCount == 0)
			{
				message = "No new message has been added.";
			}
			else if (totalMessagesCount == 1)
			{
				message = "1 new message has been added.";
			}
			else
			{
				message = string.Format("{0} new messages has been added.", totalMessagesCount);
			}
			return new JobResult(message, JobStatus.Finished);
		}

		private async Task<JobResult> ProcessRefreshCommand(string[] args)
		{
			int count = 0;
			bool stopped = false;
			foreach (string username in args)
			{
				if (_stop)
				{
					stopped = true;
					break;
				}
				if (!username.StartsWith("-"))
				{
					MongoChannel mongoChannel = db.GetChannel(username);
					if (mongoChannel != null)
					{
						if (await RefreshChannelHelper(mongoChannel.ChannelId, mongoChannel.AccessHash.Value))
						{
							count++;
						}
					}
				}
			}
			if (stopped)
			{
				return new JobResult(JobStatus.Stopped);
			}
			string message = "";
			if (count == 0)
			{
				message = "No channel has been refreshed.";
			}
			else if (count == 1)
			{
				message = "1 channel has been refreshed.";
			}
			else
			{
				message = string.Format("{0} channels has been refreshed.", count);
			}
			return new JobResult(message, JobStatus.Finished);
		}
		#endregion

		#region Handle requests
		public string ActivateChannels(string[] args)
		{
			bool now = false;
			foreach (var item in args)
			{
				if (!item.StartsWith("-"))
				{
					// check usernames
					if (!IsUsernameValid(item))
					{
						return string.Format("'{0}' is not a valid username.", item);
					}
				}
				else
				{
					// check parameters
					if (item.ToLower() == "-now")
					{
						now = true;
					}
					else
					{
						return string.Format("'{0}' is not a valid parameter.", item);
					}
				}
			}
			AddJob(BuildCommandString("activate", args), now);
			return "job has been added to queue.";
		}

		public string AddChannels(string[] args)
		{
			bool now = false;
			foreach (var item in args)
			{
				if (!item.StartsWith("-"))
				{
					// check usernames
					if (!IsUsernameValid(item))
					{
						return string.Format("'{0}' is not a valid username.", item);
					}
				}
				else
				{
					// check parameters
					if (item.ToLower() == "-now")
					{
						now = true;
					}
					else if (item.ToLower() == "-active")
					{
					}
					else
					{
						return string.Format("'{0}' is not a valid parameter.", item);
					}
				}
			}
			AddJob(BuildCommandString("add", args), now);
			return "job has been added to queue.";
		}

		public string DeactivateChannels(string[] args)
		{
			bool now = false;
			foreach (var item in args)
			{
				if (!item.StartsWith("-"))
				{
					// check usernames
					if (!IsUsernameValid(item))
					{
						return string.Format("'{0}' is not a valid username.", item);
					}
				}
				else
				{
					// check parameters
					if (item.ToLower() == "-now")
					{
						now = true;
					}
					else
					{
						return string.Format("'{0}' is not a valid parameter.", item);
					}
				}
			}
			AddJob(BuildCommandString("deactivate", args), now);
			return "job has been added to queue.";
		}

		public string GetFeed(string[] args)
		{
			bool now = false;
			bool from = false;
			bool to = false;
			foreach (var item in args)
			{
				if (item.StartsWith("-"))
				{
					// check parameters
					if (item.ToLower() == "-now")
					{
						now = true;
					}
					else if (item.ToLower() == "-from")
					{
						from = true;
					}
					else if (item.ToLower() == "-to")
					{
						to = true;
					}
					else
					{
						return string.Format("'{0}' is not a valid parameter.", item);
					}
				}
			}
			if (!from)
			{
				return "missing '-from' parameter.";
			}
			if (!to)
			{
				return "missing '-to' parameter.";
			}
			AddJob(BuildCommandString("getfeed", args), now);
			return "job has been added to queue.";
		}

		public string RefreshChannels(string[] args)
		{
			bool now = false;
			foreach (var item in args)
			{
				if (!item.StartsWith("-"))
				{
					// check usernames
					if (!IsUsernameValid(item))
					{
						return string.Format("'{0}' is not a valid username.", item);
					}
				}
				else
				{
					// check parameters
					if (item.ToLower() == "-now")
					{
						now = true;
					}
					else
					{
						return string.Format("'{0}' is not a valid parameter.", item);
					}
				}
			}
			AddJob(BuildCommandString("refresh", args), now);
			return "job has been added to queue.";
		}

		public string Stats(string[] args)
		{
			string result = "";
			result += string.Format("Total channels: {0}\nActive channels: {1}", db.GetChannelsCount(), db.GetActiveChannelsCount());
			result += string.Format("\nTotal messages: {0}\nMedia messages: {1}", db.GetMessagesCount(), db.GetMediaMessagesCount());
			return result;
		}
		#endregion

		#region Helpers
		private async Task<bool> AddChannelHelper(int channelId, long accessHash, bool active)
		{
			// get channel info
			TLInterfacePackage tl = GetAvailableTLInterface();
			Channel channel = await tl.Interface.GetChannelAsync(channelId, accessHash);
			tl.LastCallTime = DateTime.Now;
			tl.IsAvailable = true;
			if (channel != null)
			{
				tl = GetAvailableTLInterface();
				ChannelFull channelFull = await tl.Interface.GetFullChannelAsync(channel.ChannelId, channel.AccessHash.Value);
				tl.LastCallTime = DateTime.Now;
				tl.IsAvailable = true;
				if (!db.IsChannelAdded(channel.ChannelId))
				{
					// add channel
					db.AddChannel(new MongoChannel
					{
						ChannelId = channel.ChannelId,
						AccessHash = channel.AccessHash,
						Title = channel.Title,
						Username = channel.Username,
						About = channelFull.About,
						ParticipantsCounts = new List<CountTime>() { new CountTime(channelFull.ParticipantsCount, DateTime.Now) },
						IsActive = active
					});
					Log(string.Format("Channel '{0}' (@{1}) with {2} members has been added.", channel.Title, channel.Username, channelFull.ParticipantsCount));
					return true;
				}
			}
			return false;
		}

		private async Task<bool> AddChannelHelper(string username, bool active)
		{
			// get channel info
			TLInterfacePackage tl = GetAvailableTLInterface();
			Channel channel = await tl.Interface.GetChannelAsync(username);
			tl.LastCallTime = DateTime.Now;
			tl.IsAvailable = true;
			if (channel != null)
			{
				tl = GetAvailableTLInterface();
				ChannelFull channelFull = await tl.Interface.GetFullChannelAsync(channel.ChannelId, channel.AccessHash.Value);
				tl.LastCallTime = DateTime.Now;
				tl.IsAvailable = true;
				if (!db.IsChannelAdded(channel.ChannelId))
				{
					// refresh channel on duplicate username
					if (db.IsChannelAdded(username))
					{
						MongoChannel channelToRefresh = db.GetChannel(username);
						await RefreshChannelHelper(channelToRefresh.ChannelId, channelToRefresh.AccessHash.Value);
					}
					// add channel
					db.AddChannel(new MongoChannel
					{
						ChannelId = channel.ChannelId,
						AccessHash = channel.AccessHash,
						Title = channel.Title,
						Username = channel.Username,
						About = channelFull.About,
						ParticipantsCounts = new List<CountTime>() { new CountTime(channelFull.ParticipantsCount, DateTime.Now) },
						IsActive = active
					});
					Log(string.Format("Channel '{0}' (@{1}) with {2} members has been added.", channel.Title, channel.Username, channelFull.ParticipantsCount));
					return true;
				}
				else
				{
					// refresh channel if it already exists
					await RefreshChannelHelper(channel.ChannelId, channel.AccessHash.Value);
				}
			}
			return false;
		}

		private async Task<bool> RefreshChannelHelper(int channelId, long accessHash)
		{
			// get channel info
			TLInterfacePackage tl = GetAvailableTLInterface();
			Channel channel = await tl.Interface.GetChannelAsync(channelId, accessHash);
			tl.LastCallTime = DateTime.Now;
			tl.IsAvailable = true;
			if (channel != null)
			{
				tl = GetAvailableTLInterface();
				ChannelFull channelFull = await tl.Interface.GetFullChannelAsync(channel.ChannelId, channel.AccessHash.Value);
				tl.LastCallTime = DateTime.Now;
				tl.IsAvailable = true;
				// update channel
				db.UpdateChannel(channelId, new MongoChannel
				{
					Title = channel.Title,
					Username = channel.Username,
					About = channelFull.About,
					ParticipantsCounts = new List<CountTime>() { new CountTime(channelFull.ParticipantsCount, DateTime.Now) }
				});
				Log(string.Format("Channel '{0}' (@{1}) with {2} members has been refreshed.", channel.Title, channel.Username, channelFull.ParticipantsCount));
				return true;
			}
			return false;
		}

		private List<string> ExtractUsernamesHelper(string text)
		{
			text = text.ToLower();
			List<string> usernames = new List<string>();
			string candidate = "";
			bool detected = false;
			for (int i = 0; i < text.Length; i++)
			{
				if (!detected)
				{
					if (text[i] == '@')
					{
						detected = true;
					}
				}
				else
				{
					char ch = text[i];
					if (((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'z') || ch == '_'))
					{
						candidate += ch;
					}
					else
					{
						if (candidate.Length >= 5)
						{
							usernames.Add(candidate);
						}
						candidate = "";
						detected = false;
					}
				}
			}
			if (candidate.Length >= 5)
			{
				usernames.Add(candidate);
			}
			return usernames;
		}

		private bool IsUsernameValid(string username)
		{
			if (username.Length < 5)
			{
				return false;
			}
			username = username.ToLower();
			foreach (char ch in username)
			{
				if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'z') || ch == '_'))
				{
					return false;
				}
			}
			return true;
		}
		#endregion
	}
}
