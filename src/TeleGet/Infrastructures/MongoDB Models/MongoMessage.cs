using Infrastructures.Telegram;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Infrastructures.MongoDB_Models
{
	[BsonIgnoreExtraElements]
	public class MongoMessage
	{
		[BsonElement("channelId")]
		public int ChannelId { get; set; }
		[BsonElement("messageId")]
		public int MessageId { get; set; }
		[BsonElement("text")]
		public string Text { get; set; }
		[BsonElement("mediaType")]
		public string MediaType { get; set; }
		[BsonElement("mediaSize")]
		public int? MediaSize { get; set; }
		[BsonElement("fileName")]
		public string FileName { get; set; }
		[BsonElement("date")]
		public int Date { get; set; }
		[BsonElement("views")]
		public List<CountTime> Views { get; set; }
	}
}
