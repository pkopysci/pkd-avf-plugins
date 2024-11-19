namespace CrComLibUi.Components
{
	using Crestron.SimplSharpPro.DeviceSupport;
	using pkd_application_service.UserInterface;
	using pkd_common_utils.Logging;
	using pkd_ui_service.Interfaces;
	using CrComLibUi.Api;
	using System;
	using System.Collections.Generic;

	internal abstract class BaseComponent : IUiComponent, ISerialResponseHandler
	{
		protected readonly Dictionary<string, Action<ResponseBase>> GetHandlers = new Dictionary<string, Action<ResponseBase>>();
		protected readonly Dictionary<string, Action<ResponseBase>> PostHandlers = new Dictionary<string, Action<ResponseBase>>();
		protected readonly UserInterfaceDataContainer uiData;
		protected BasicTriListWithSmartObject ui;

		protected BaseComponent(
			BasicTriListWithSmartObject ui,
			UserInterfaceDataContainer uiData)
		{
			this.uiData = uiData;
			this.ui = ui;
		}

		public bool Initialized { get; protected set; }

		/// <inheritdoc/>
		public abstract void HandleSerialResponse(string response);

		/// <inheritdoc/>
		public abstract void Initialize();

		/// <inheritdoc/>
		public virtual void SetActiveDefaults() { }

		/// <inheritdoc/>
		public virtual void SetStandbyDefaults() { }

		protected void Send(ResponseBase data, ApiHooks hook)
		{
			// serialize and send if successful
			string rxMessage = MessageFactory.SerializeMessage(data);
			if (!string.IsNullOrEmpty(rxMessage))
			{
				ui.StringInput[(uint)hook].StringValue = rxMessage;
			}
		}

		protected void FlushJoinData(ApiHooks hook)
		{
			ui.StringInput[(uint)hook].StringValue = string.Empty;
		}

		protected virtual bool CheckInitialized(string className, string methodName)
		{
			if (!this.Initialized)
			{
				Logger.Error($"GcuVueUi.{className}.{methodName}() - system not yet initialized.");
				return false;
			}

			return true;
		}
	}
}
