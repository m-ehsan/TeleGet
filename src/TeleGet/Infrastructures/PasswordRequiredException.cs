using System;
using System.Runtime.Serialization;

namespace Infrastructures
{
	[Serializable]
	public class PasswordRequiredException : Exception
	{
		public PasswordRequiredException()
		{
		}

		public PasswordRequiredException(string message) : base(message)
		{
		}

		public PasswordRequiredException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected PasswordRequiredException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}