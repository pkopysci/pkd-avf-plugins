namespace CrComLibUi.Api;

using Newtonsoft.Json;
using pkd_common_utils.Logging;
using System;

internal static class MessageFactory
{
	/// <summary>
	/// Attempts to deserialize an API response into a ResponseBase data object. If the deserialization.
	/// </summary>
	/// <param name="message">the API JSON message to deserialize.</param>
	/// <returns>the data object containing deserialized message information or NULL if message is null or empty or if the deserialization failed.
	/// </returns>
	public static ResponseBase DeserializeMessage(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			Logger.Error("CrComLibUi.Api.MessageFactory.DeserializeMessage() - argument 'message' cannot be null or empty.");
			return new ResponseBase();
		}

		try
		{
			var eofIdx = message.IndexOf("EOF", StringComparison.Ordinal);
			if (eofIdx != -1)
			{
				message = message[..eofIdx];
			}

			var rx = JsonConvert.DeserializeObject<ResponseBase>(message);
			if (rx != null) return rx;
			
			Logger.Error($"CrComLibUi.Api.MessageFactory.DeserializeMessage() - Could not deserialize {message}. Returning empty response.");
			return new ResponseBase();

		}
		catch (Exception ex)
		{
			Logger.Error(
				"CrComLibUi.Api.MessageFactory.DeserializeMessage() - Failed to deserialize the message returning empty response.\nReason: {0}",
				ex.Message);

			return new ResponseBase();
		}
	}

	/// <summary>
	/// Attempts to serialize and API data object and prepare it for sending. This method will also append the EOF delimiter
	/// to the final string.
	/// </summary>
	/// <param name="messageData">The object to serialize.</param>
	/// <returns>The serialized and formatted response string. The empty string will be returned if messageData is null or if the serialization failed.</returns>
	public static string SerializeMessage(ResponseBase messageData)
	{
		try
		{
			return JsonConvert.SerializeObject(messageData) + "EOF";
		}
		catch (Exception ex)
		{
			Logger.Error(
				"CrComLibUi.Api.MessageFactory.SerializeMessage() - Failed to serialize the message object.. Reason: {0}",
				ex.Message);

			return string.Empty;
		}
	}

	public static ResponseBase CreateGetResponseObject()
	{
		return new ResponseBase() { Method = "GET" };
	}

	public static ResponseBase CreatePostResponseObject()
	{
		return new ResponseBase() { Method = "POST" };
	}

	public static ResponseBase CreateErrorResponse(string errorMessage = "")
	{
		return new ResponseBase()
		{
			Method = "GET",
			Command = "ERROR",
			Data = errorMessage,
		};
	}
}