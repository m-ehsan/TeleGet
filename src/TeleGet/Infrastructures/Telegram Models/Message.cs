namespace Infrastructures.Telegram
{
	public class Message
	{
		public Channel SourceChannel { get; set; }
		public int MessageId { get; set; }
		public string Text { get; set; }
		public Media MediaContent { get; set; }
		public int Date { get; set; }
		public int? OriginalDate { get; set; }
		public CountTime Views { get; set; }
	}
}