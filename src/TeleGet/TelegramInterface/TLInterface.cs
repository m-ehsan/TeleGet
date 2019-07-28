using Infrastructures;
using Infrastructures.Telegram;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Account;
using TeleSharp.TL.Channels;
using TeleSharp.TL.Contacts;
using TeleSharp.TL.Messages;
using TeleSharp.TL.Users;
using TLSharp.Core;

namespace TelegramInterface
{
	public class TLInterface
	{
		const int _apiId = 123456; // place your api id here
		const string _apiHash = ""; // place your api hash here
		private string _phoneNumber;
		private string _loginHash;
		private TLPassword _cloudPassword;
		private TelegramClient _client;
		public string SessionPath { get; private set; }
		public TLUser CurrentUser { get; private set; }
		public bool IsAuthorized { get { return CurrentUser != null; } }

		public TLInterface(string session)
		{
			SessionPath = session;
			Init();
		}

		private void Init()
		{
			_client = new TelegramClient(_apiId, _apiHash, new FileSessionStore(), SessionPath);
			Task.Run(async () => await InitializeClientAsync()).Wait();
		}

		private async Task InitializeClientAsync()
		{
			await _client.ConnectAsync();
			var req = new TLRequestGetFullUser() { Id = new TLInputUserSelf() };
			try
			{
				CurrentUser = (TLUser)(await _client.SendRequestAsync<TLUserFull>(req)).User;
			}
			catch (InvalidOperationException)
			{
				CurrentUser = null;
			}
		}

		public async Task SendCodeAsync(string phoneNumber)
		{
			_phoneNumber = phoneNumber;
			_loginHash = await _client.SendCodeRequestAsync(_phoneNumber);
		}

		public async Task EnterCodeAsync(string code)
		{
			try
			{
				CurrentUser = await _client.MakeAuthAsync(_phoneNumber, _loginHash, code);
			}
			catch (CloudPasswordNeededException)
			{
				_cloudPassword = await _client.GetPasswordSetting();
				throw new PasswordRequiredException();
			}
			catch (InvalidPhoneCodeException e)
			{
				throw new Exception(e.Message);
			}
		}

		public async Task EnterCloudPasswordAsync(string password)
		{
			CurrentUser = await _client.MakeAuthWithPasswordAsync(_cloudPassword, password);
		}

		public async Task<Channel> GetChannelAsync(string username)
		{
			var req = new TLRequestResolveUsername() { Username = username };
			TLResolvedPeer peer;
			try
			{
				peer = await _client.SendRequestAsync<TLResolvedPeer>(req);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
			if (peer.Chats.Count == 0)
			{
				return null;
			}
			TLChannel channel = (TLChannel)peer.Chats[0];
			if (!channel.AccessHash.HasValue)
			{
				return null;
			}
			return new Channel()
			{
				ChannelId = channel.Id,
				AccessHash = channel.AccessHash ?? 0,
				Title = channel.Title,
				Username = channel.Username
			};
		}

		public async Task<Channel> GetChannelAsync(int channelId, long accessHash)
		{
			var req = new TLRequestGetFullChannel()
			{
				Channel = new TLInputChannel()
				{
					ChannelId = channelId,
					AccessHash = accessHash
				}
			};
			TeleSharp.TL.Messages.TLChatFull fullChat;
			try
			{
				fullChat = await _client.SendRequestAsync<TeleSharp.TL.Messages.TLChatFull>(req);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
			TLChannel channel = (TLChannel)fullChat.Chats[0];
			return new Channel()
			{
				ChannelId = channel.Id,
				AccessHash = channel.AccessHash ?? 0,
				Title = channel.Title,
				Username = channel.Username
			};
		}

		public async Task<ChannelFull> GetFullChannelAsync(int channelId, long accessHash)
		{
			var req = new TLRequestGetFullChannel()
			{
				Channel = new TLInputChannel()
				{
					ChannelId = channelId,
					AccessHash = accessHash
				}
			};
			TeleSharp.TL.Messages.TLChatFull fullChat;
			try
			{
				fullChat = await _client.SendRequestAsync<TeleSharp.TL.Messages.TLChatFull>(req);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
			var fullChannel = (TLChannelFull)fullChat.FullChat;
			return new ChannelFull()
			{
				ChannelId = fullChannel.Id,
				About = fullChannel.About,
				ParticipantsCount = fullChannel.ParticipantsCount ?? 0
			};
		}

		public async Task<List<Message>> GetChannelMessagesAsync(int channelId, long accessHash, List<int> messageIDs)
		{
			var IDs = new TLVector<int>();
			foreach (var id in messageIDs)
			{
				IDs.Add(id);
			}
			var req = new TeleSharp.TL.Channels.TLRequestGetMessages() { Channel = new TLInputChannel() { ChannelId = channelId, AccessHash = accessHash }, Id = IDs };
			TLChannelMessages messages;
			messages = await _client.SendRequestAsync<TLChannelMessages>(req);
			return RefineMessages(messages);
		}

		public async Task<List<Message>> GetChannelMessagesAsync(int channelId, long accessHash, int? offsetId, int? offsetDate, int? limit)
		{
			var req = new TLRequestGetHistory()
			{
				Peer = new TLInputPeerChannel() { ChannelId = channelId, AccessHash = accessHash },
				OffsetId = offsetId ?? 0,
				OffsetDate = offsetDate ?? 0,
				Limit = limit ?? 0
			};
			TLChannelMessages messages = new TLChannelMessages();
			messages = await _client.SendRequestAsync<TLChannelMessages>(req);
			return RefineMessages(messages);
		}

		private List<Message> RefineMessages(TLChannelMessages messages)
		{
			List<Message> result = new List<Message>();
			for (int i = 0; i < messages.Messages.Count; i++)
			{
				if (!(messages.Messages[i] is TLMessage))
				{
					continue;
				}
				TLMessage message = messages.Messages[i] as TLMessage;
				Message tempMessage = new Message
				{
					Text = message.Message,
					SourceChannel = new Channel() { ChannelId = (message.ToId as TLPeerChannel).ChannelId },
					MessageId = message.Id,
					Date = message.Date,
					Views = new CountTime(message.Views.HasValue ? message.Views.Value : 0, DateTime.Now)
				};
				if (message.FwdFrom != null)
				{
					if (message.FwdFrom.ChannelId != null)
					{
						tempMessage.SourceChannel.ChannelId = message.FwdFrom.ChannelId ?? 0;
						tempMessage.MessageId = message.FwdFrom.ChannelPost ?? 0;
						tempMessage.OriginalDate = message.FwdFrom.Date;
					}

				}
				if (message.Media != null)
				{
					if (message.Media is TLMessageMediaPhoto)
					{
						tempMessage.MediaContent = new Media()
						{
							Caption = (message.Media as TLMessageMediaPhoto).Caption,
							Type = "photo",
						};
					}
					else if (message.Media is TLMessageMediaDocument)
					{
						tempMessage.MediaContent = new Media()
						{
							Caption = (message.Media as TLMessageMediaDocument).Caption,
							Type = ((message.Media as TLMessageMediaDocument).Document as TLDocument).MimeType,
							Size = ((message.Media as TLMessageMediaDocument).Document as TLDocument).Size
						};
						try
						{
							tempMessage.MediaContent.FileName = ((TLDocumentAttributeFilename)(((message.Media as TLMessageMediaDocument).Document as TLDocument).Attributes.First(a => a.GetType() == typeof(TLDocumentAttributeFilename)))).FileName;
						}
						catch (Exception)
						{
							tempMessage.MediaContent.FileName = null;
						}
					}
				}
				// append source channel info
				foreach (var chat in messages.Chats)
				{
					if (chat is TLChannel)
					{
						if ((chat as TLChannel).Id == tempMessage.SourceChannel.ChannelId)
						{
							tempMessage.SourceChannel.AccessHash = (chat as TLChannel).AccessHash;
							tempMessage.SourceChannel.Title = (chat as TLChannel).Title;
							tempMessage.SourceChannel.Username = (chat as TLChannel).Username;
							break;
						}
					}
				}
				result.Add(tempMessage);
			}
			return result;
		}
	}
}
