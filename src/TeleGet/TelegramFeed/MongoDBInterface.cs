using Infrastructures;
using Infrastructures.MongoDB_Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeleGet_CLI
{
	class MongoDBInterface
	{

		private MongoClient _mongoClient;
		private IMongoDatabase _db;

		public MongoDBInterface(string dbAddress)
		{
			_mongoClient = new MongoClient("mongodb://" + dbAddress);
			_db = _mongoClient.GetDatabase("Telegram");
		}

		public bool IsChannelAdded(string username)
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			if (channels.Find(c => c.Username.ToLower() == username.ToLower()).CountDocuments() > 0)
			{
				return true;
			}
			return false;
		}

		public bool IsChannelAdded(int channelId)
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			if (channels.AsQueryable().Any(c => c.ChannelId == channelId))
			{
				return true;
			}
			return false;
		}

		public MongoChannel GetChannel(int channelId)
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			try
			{
				return channels.Find(c => c.ChannelId == channelId).First();
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		public MongoChannel GetChannel(string username)
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			try
			{
				return channels.Find(c => c.Username.ToLower() == username.ToLower()).First();
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		public void AddChannel(MongoChannel channel)
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			channels.InsertOne(channel);
		}

		public void UpdateChannel(int channelId, MongoChannel channel)
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			MongoChannel mongoChannel = channels.Find(c => c.ChannelId == channelId).First();
			mongoChannel.ParticipantsCounts = mongoChannel.ParticipantsCounts.Concat(channel.ParticipantsCounts).ToList();
			var filter = Builders<MongoChannel>.Filter.Eq("channelId", channelId);
			channels.UpdateOne(filter, Builders<MongoChannel>.Update.Set("title", channel.Title));
			channels.UpdateOne(filter, Builders<MongoChannel>.Update.Set("username", channel.Username));
			channels.UpdateOne(filter, Builders<MongoChannel>.Update.Set("about", channel.About));
			channels.UpdateOne(filter, Builders<MongoChannel>.Update.Set("participantsCount", mongoChannel.ParticipantsCounts));
		}

		public void UpdateChannelCoveredTimePeriods(int channelId, TimePeriods timePeriods)
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			var filter = Builders<MongoChannel>.Filter.Eq("channelId", channelId);
			channels.UpdateOne(filter, Builders<MongoChannel>.Update.Set("coveredTimePeriods", timePeriods));
		}

		public void UpdateChannelActivationStatus(int channelId, bool active)
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			var filter = Builders<MongoChannel>.Filter.Eq("channelId", channelId);
			channels.UpdateOne(filter, Builders<MongoChannel>.Update.Set("isActive", active));
		}

		public List<int> GetActiveChannelsIDs()
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			return channels.Find(c => c.IsActive == true).ToList().Select(s => s.ChannelId).ToList();
		}

		public long GetChannelsCount()
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			var filter = Builders<MongoChannel>.Filter.Empty;
			return channels.CountDocuments(filter);
		}

		public long GetActiveChannelsCount()
		{
			var channels = _db.GetCollection<MongoChannel>("channels");
			return channels.CountDocuments(c => c.IsActive == true);
		}

		public void AddMessage(MongoMessage message)
		{
			var messages = _db.GetCollection<MongoMessage>("messages");
			messages.InsertOne(message);
		}

		public bool IsMessageAdded(int channelId, int messageId)
		{
			var messages = _db.GetCollection<MongoMessage>("messages");
			if (messages.AsQueryable().Any(m => m.ChannelId == channelId && m.MessageId == messageId))
			{
				return true;
			}
			return false;
		}

		public long GetMessagesCount()
		{
			var messages = _db.GetCollection<MongoMessage>("messages");
			var filter = Builders<MongoMessage>.Filter.Empty;
			return messages.CountDocuments(filter);
		}

		public long GetMediaMessagesCount()
		{
			var messages = _db.GetCollection<MongoMessage>("messages");
			return messages.CountDocuments(m => !string.IsNullOrEmpty(m.MediaType));
		}

		public void AddUnresolvedUsername(MongoUsername username)
		{
			var unresolvedUsername = _db.GetCollection<MongoUsername>("unresolvedUsernames");
			unresolvedUsername.InsertOne(username);
		}

		//public void RemoveUnresolvedUsername(MongoUsername username)
		//{
		//	var unresolvedUsername = _db.GetCollection<MongoUsername>("unresolvedUsernames");
		//	unresolvedUsername.DeleteOne(username);
		//}

		public bool IsUnresolvedUsernameAdded(MongoUsername username)
		{
			var unresolvedUsername = _db.GetCollection<MongoUsername>("unresolvedUsernames");
			if (unresolvedUsername.AsQueryable().Any(u => u.Username.ToLower() == username.Username.ToLower()))
			{
				return true;
			}
			return false;
		}
	}
}
