using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.Models.ChatModels
{
	[Table("FileMessages")] // ← Та же таблица, что и FileMessage
	public class VoiceMessage : FileMessage
	{
		public VoiceMessage()
		{
			MessageType = "voice";
			ContentType = "audio/webm";
		}
	}
}