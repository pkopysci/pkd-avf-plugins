namespace CrComLibUi.Components
{
	internal interface ISerialResponseHandler
	{
		/// <summary>
		/// Recieve and processes response data received from a user interface device or connection.
		/// </summary>
		/// <param name="response"></param>
		void HandleSerialResponse(string response);
	}
}
