namespace Infrastructures.Telegram
{
	public class Channel
	{
		public int ChannelId { get; set; }
		public long? AccessHash { get; set; }
		public string Title { get; set; }
		public string Username { get; set; }
	}
}