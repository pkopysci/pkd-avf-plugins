namespace LutronLighting
{
	using Crestron.SimplSharp;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.NetComs;
	using pkd_hardware_service.LightingDevices;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Text.RegularExpressions;

	public class BasicQseController : ILightingDevice, IDisposable
	{
		private static readonly string CMD_REGEX = @"(?<prompt>QSE>)*~(?<command>\w+),(?<id>.+),(?<component>\d+),(?<action>\d+),(?<data>[\w\.]+)\r\n*(?<prompt>QSE>)*";
		private static readonly string LOGIN_RX = "login:";
		private const int RX_TIMEOUT_LENGTH = 3000;
		private static readonly string LOGIN_SUCCESS_RX = "connection established\r\n";
		private static readonly string LOGIN_FAIL_RX = @"login incorrect[\r\n]+";
		private readonly Dictionary<int, Action<GroupCollection>> baseActionHandlers;
		private readonly Dictionary<string, LightingItem> scenes;
		private readonly Dictionary<string, LightingZoneItem> zones;
		private BasicTcpClient client;
		private CTimer rxTimer;
		private bool disposed;
		private string userName;
#pragma warning disable IDE0052 // Remove unread private members
		private string password;
#pragma warning restore IDE0052 // Remove unread private members
		private int componentNumber;
		private int reconnectAttempts;

		public BasicQseController()
		{
			this.scenes = new Dictionary<string, LightingItem>();
			this.zones = new Dictionary<string, LightingZoneItem>();
			this.baseActionHandlers = new Dictionary<int, Action<GroupCollection>>
			{
				{ 7, this.HandleSceneResponse }
			};
		}

		~BasicQseController()
		{
			this.Dispose(false);
		}


		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ActiveSceneChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ZoneLoadChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;

		/// <inheritdoc/>
		public string Id { get; private set; }

		/// <inheritdoc/>
		public bool IsInitialized { get; private set; }

		/// <inheritdoc/>
		public bool IsOnline { get; private set; }

		/// <inheritdoc/>
		public string Label { get; private set; }

		/// <inheritdoc/>
		public ReadOnlyCollection<string> SceneIds
		{
			get
			{
				List<string> keys = new List<string>();
				foreach (var key in this.scenes.Keys)
				{
					keys.Add(key);
				}

				return new ReadOnlyCollection<string>(keys);
			}
		}

		/// <inheritdoc/>
		public ReadOnlyCollection<string> ZoneIds
		{
			get
			{
				List<string> keys = new List<string>();
				foreach (var key in this.zones.Keys)
				{
					keys.Add(key);
				}

				return new ReadOnlyCollection<string>(keys);
			}
		}

		/// <inheritdoc/>
		public string ActiveSceneId { get; private set; }

		/// <inheritdoc/>
		public void AddScene(string id, string label, int index)
		{
			if (
				!this.CheckString("AddScene", id, "id", false) ||
				!this.CheckString("AddScene", label, "label", false) ||
				!this.CheckInt("AddScene", index, 0, 1000, "index"))
			{
				return;
			}

			Logger.Debug("LutronLighting.BasicQseController.AddScene({0}, {1}, {2}", id, label, index);

			if (this.scenes.ContainsKey(id))
			{
				Logger.Warn("BasicQseController.AddScene() - scene with ID {0} already exists. Replacing...", id);
				this.scenes[id] = new LightingItem() { Id = id, Index = index, Label = label };
			}
			else
			{
				this.scenes.Add(id, new LightingItem() { Id = id, Index = index, Label = label });
			}

		}

		/// <inheritdoc/>
		public void AddZone(string id, string label, int index)
		{
			if (
				!this.CheckString("AddZone", id, "id", false) ||
				!this.CheckString("AddZone", label, "label", false) ||
				!this.CheckInt("AddZone", index, 0, 1000, "index"))
			{
				return;
			}

			if (this.scenes.ContainsKey(id))
			{
				Logger.Warn("BasicQseController.AddZone() - scene with ID {0} already exists. Replacing...", id);
				this.zones[id] = new LightingZoneItem() { Id = id, Index = index, Label = label, Level = 0 };
			}
			else
			{
				this.zones.Add(id, new LightingZoneItem() { Id = id, Index = index, Label = label, Level = 0 });
			}
		}

		/// <inheritdoc/>
		public int GetZoneLoad(string id)
		{
			if (!this.CheckString("GetZoneLoad", id, "id", false))
			{
				return 0;
			}

			if (this.zones.TryGetValue(id, out LightingZoneItem found))
			{
				return found.Level;
			}

			return 0;
		}

		/// <inheritdoc/>
		public void Initialize(
			string hostName,
			int port,
			string id,
			string label,
			string userName,
			string password,
			List<string> tags)
		{
			this.IsInitialized = false;
			this.DisposeClient();

			if (!this.CheckString("BasicQscController.Initialize", hostName, "hostName", false) ||
				!this.CheckString("BasicQscController.Initialize", label, "label", false) ||
				!this.CheckString("BasicQscController.Initialize", userName, "userName", true) ||
				!this.CheckString("BasicQscController.Initialize", password, "password", true) ||
				!this.CheckInt("BasicQscController.Initialize", port, 0, 23, "port"))
			{
				Logger.Error("LutronLighting.BasicQseController.Initialize() - Initialization failed.");
				return;
			}

			this.reconnectAttempts = 0;
			this.ParseComponentId(tags);
			this.Id = id;
			this.Label = label;
			this.userName = userName;
			this.password = password;
			this.client = new BasicTcpClient(hostName, port);
			this.client.ConnectionFailed += this.Client_ConnectionFailed;
			this.client.ClientConnected += this.Client_ClientConnected;
			this.client.RxRecieved += Client_RxRecieved;
			this.client.StatusChanged += this.Client_StatusChanged;

			this.IsInitialized = true;
		}

		/// <inheritdoc/>
		public void RecallScene(string id)
		{
			if (!this.CheckInit("RecallScene") || !this.CheckString("RecallScene", id, "id", false))
			{
				return;
			}

			if (!this.client.Connected)
			{
				Logger.Error("LutronLighting.BasicQseController {0} - RecallScene() - client not connected.", this.Id);
				return;
			}

			if (this.scenes.TryGetValue(id, out LightingItem found))
			{
				Logger.Debug("LutronLighting.BasicQseController.RecallScene({0})", id);
				this.Send(string.Format("#DEVICE,{0},{1},7,{2}\r\n", this.Id, this.componentNumber, found.Index));
			}
		}

		/// <inheritdoc/>
		public void SetZoneLoad(string id, int loadLevel)
		{
			// TODO: BasicQseController.SetZoneLoad()
			Logger.Warn("LutronLighting.BasicQseController.SetZoneLoad() - Device {0} - output load control not yet supported.", this.Id);

			//if (!this.CheckInit("SetZoneLoad") ||
			//    !this.CheckString("SetZoneLoad", id, "id", false) ||
			//    this.CheckInt("SetZoneLoad", loadLevel, 0, 100, "loadLevel"))
			//{
			//    return;
			//}
		}

		/// <inheritdoc/>
		public void Connect()
		{
			if (this.client == null || this.client.Connected)
			{
				return;
			}

			this.client.EnableReconnect = true;
			this.client.Connect();
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			this.DisposeRxTimer();
			this.client.EnableReconnect = false;
			this.client.Disconnect();
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					this.DisposeClient();
					this.DisposeRxTimer();
				}

				this.disposed = true;
			}
		}

		private void RxTimeoutHandler(object obj)
		{
			Logger.Error("LutronLighting.BasicQseController - No response to a command");
			this.reconnectAttempts++;
			if (this.reconnectAttempts > 5)
			{
				this.Disconnect();
				this.Connect();
			}
		}

		private void SendSceneQuery()
		{
			this.Send(string.Format("?DEVICE,{0},{1},7\r\n", this.Id, this.componentNumber));
		}

		private void Send(string cmd)
		{
			if (this.rxTimer != null && !rxTimer.Disposed)
			{
				this.rxTimer.Dispose();
				this.rxTimer = null;
			}

			this.rxTimer = new CTimer(this.RxTimeoutHandler, RX_TIMEOUT_LENGTH);
			Logger.Debug("LutronLighting.BasicQseController {0} - Sending command {1}", this.Id, cmd);
			this.client.Send(cmd);
		}

		private void Client_ClientConnected(object sender, EventArgs e)
		{
			this.reconnectAttempts = 0;
			this.IsOnline = this.client.Connected;
			var temp = this.ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		private void Client_ConnectionFailed(object sender, GenericSingleEventArgs<Crestron.SimplSharp.CrestronSockets.SocketStatus> e)
		{
			if (this.reconnectAttempts > 10)
			{
				return;
			}

			Logger.Error("LutronLighting.BasicQseController {0} - Connection failed: {1}", this.Id, e.Arg);
		}

		private void Client_StatusChanged(object sender, EventArgs e)
		{
			this.IsOnline = client.Connected;
			var temp = this.ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));

			if (!this.IsOnline)
			{
				this.DisposeRxTimer();
			}
		}

		private void Client_RxRecieved(object sender, GenericSingleEventArgs<string> e)
		{
			this.DisposeRxTimer();

			Logger.Debug("LutronLighting.BasicQseController - Client RX received: {0}", e.Arg);

			if (e.Arg.Contains(LOGIN_RX))
			{
				client.Send(string.Format("{0}\r\n", this.userName));
				return;
			}
			else if (e.Arg.Contains(LOGIN_SUCCESS_RX))
			{
				Logger.Debug("LutronLighting.BasicQseController - Successfull login received! Sending scene query.");
				this.SendSceneQuery();
				return;
			}
			else if (e.Arg.Contains(LOGIN_FAIL_RX))
			{
				Logger.Error("LutronLighting.BasicWseController {0} - Login attempt failed.", this.Id);
				return;
			}

			string[] responses = e.Arg.Split('\n');
			foreach (string rx in responses)
			{
				Match cmdMatch = new Regex(CMD_REGEX).Match(rx);
				if (cmdMatch.Success)
				{
					Logger.Debug("Match Found");
					switch (cmdMatch.Groups["command"].Value)
					{
						case "DEVICE":
							this.HandleDeviceRx(cmdMatch.Groups);
							break;
						case "OUTPUT":
							Logger.Debug("OUTPUT RX received.");
							break;
						case "Error":
							Logger.Debug("Error RX Received");
							break;
					}
				}
			}
		}

		private void HandleDeviceRx(GroupCollection regexGroups)
		{
			Logger.Debug("DEVICE RX received.");
			try
			{
				int component = int.Parse(regexGroups["component"].Value);
				if (component == this.componentNumber)
				{
					Logger.Debug("Base component RX received.");
					int actionId = int.Parse(regexGroups["action"].Value);
					if (this.baseActionHandlers.TryGetValue(actionId, out Action<GroupCollection> handler))
					{
						handler.Invoke(regexGroups);
					}
				}
				else
				{
					this.HandleZoneReponse(regexGroups);
				}
			}
			catch (Exception e)
			{
				Logger.Error("LutronLighting.BasicQseController {0} - failed to parse DEVICE response: {1}", this.Id, e);
			}
		}

		private void HandleSceneResponse(GroupCollection regexGroups)
		{
			try
			{
				int newScene = int.Parse(regexGroups["data"].Value);
				foreach (var kvp in this.scenes)
				{
					if (kvp.Value.Index == newScene)
					{
						this.ActiveSceneId = kvp.Key;
						var temp = this.ActiveSceneChanged;
						temp?.Invoke(this, new GenericSingleEventArgs<string>(this.ActiveSceneId));

						break;
					}
				}
			}
			catch (Exception e)
			{
				Logger.Error("LutronLighting.BasicQseController {0} - failed to parse new scene ID: {1}", this.Id, e);
			}
		}

		private void HandleZoneReponse(GroupCollection regexGroups)
		{
			try
			{
				int index = int.Parse(regexGroups["component"].Value);
				int load = (int)float.Parse(regexGroups["data"].Value);
				foreach (var kvp in this.zones)
				{
					if (kvp.Value.Index == index)
					{
						kvp.Value.Level = load;
						var temp = this.ZoneLoadChanged;
						temp?.Invoke(this, new GenericSingleEventArgs<string>(kvp.Value.Id));

						return;
					}
				}
			}
			catch (Exception e)
			{
				Logger.Error("LutronLighting.BasicQseController {0} - Failed to parse lighting zone response: {1}", this.Id, e);
			}
		}

		private void DisposeClient()
		{
			if (this.client != null)
			{
				this.client.ConnectionFailed -= this.Client_ConnectionFailed;
				this.client.ClientConnected -= this.Client_ClientConnected;
				this.Disconnect();
				this.client.Dispose();
			}
		}

		private void DisposeRxTimer()
		{
			if (this.rxTimer != null && !this.rxTimer.Disposed)
			{
				this.rxTimer.Dispose();
				this.rxTimer = null;
			}
		}

		private bool CheckInit(string methodName)
		{
			if (!this.IsInitialized)
			{
				Logger.Error("LutronLighting.BasicQseController.{0}() - Device not initialized.", methodName);
			}

			return this.IsInitialized;
		}

		private bool CheckString(string methodName, string argument, string argName, bool allowEmpty)
		{
			if ((!allowEmpty && string.IsNullOrEmpty(argument)) || argument == null)
			{
				Logger.Error("LutronLighting.BasicQseController.{0}() - argument {1} cannot be {2}}.", methodName, argName, allowEmpty ? "null" : "null or empty");
				return false;
			}

			return true;
		}

		private bool CheckInt(string methodName, int argument, int min, int max, string argName)
		{
			if (argument < min || argument > max)
			{
				Logger.Error("LutronLighting.BasicQseController.{0}() - argument {1} cannot be less than {1} or greater than {2}.", methodName, argName, min, max);
				return false;
			}

			return true;
		}

		private void ParseComponentId(List<string> tags)
		{
			var componentTag = tags.FirstOrDefault(x => x.Contains("component-"));
			if (componentTag != null)
			{
				try
				{
					string[] values = componentTag.Split('-');
					if (values.Length > 1)
					{
						this.componentNumber = int.Parse(values[1]);
					}
				}
				catch (Exception e)
				{
					Logger.Error("LutronLighting.BasicQseController - failed to parse tags for component ID: {0}", e);
					this.componentNumber = 141;
				}
			}
			else
			{
				Logger.Error("LutronLighting.BasicQseController - no 'compoent-[ID NUMBER] tag in collection. Unable to set component ID.");
			}
		}
	}
}
