using MongoDB.Bson.Serialization.Attributes;

namespace Infrastructures.MongoDB_Models
{
	[BsonIgnoreExtraElements]
	public class MongoUsername
	{
		[BsonElement("username")]
		public string Username { get; set; }
	}
}
