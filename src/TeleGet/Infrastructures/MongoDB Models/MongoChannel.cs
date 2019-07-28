using Infrastructures.Telegram;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Infrastructures.MongoDB_Models
{
	[BsonIgnoreExtraElements]
	public class MongoChannel
	{
		[BsonElement("channelId")]
		public int ChannelId { get; set; }
		[BsonElement("accessHash")]
		public long? AccessHash { get; set; }
		[BsonElement("title")]
		public string Title { get; set; }
		[BsonElement("username")]
		public string Username { get; set; }
		[BsonElement("about")]
		public string About { get; set; }
		[BsonElement("participantsCounts")]
		public List<CountTime> ParticipantsCounts { get; set; }
		[BsonElement("coveredTimePeriods")]
		public TimePeriods CoveredTimePeriods { get; set; }
		[BsonElement("isActive")]
		public bool IsActive { get; set; }
	}
}
