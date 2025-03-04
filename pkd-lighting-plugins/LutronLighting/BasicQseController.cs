namespace LutronLighting;

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
	private const string CmdRegex = @"(?<prompt>QSE>)*~(?<command>\w+),(?<id>.+),(?<component>\d+),(?<action>\d+),(?<data>[\w\.]+)\r\n*(?<prompt>QSE>)*";
	private const string LoginRx = "login:";
	private const int RxTimeoutLength = 3000;
	private const string LoginSuccessRx = "connection established\r\n";
	private const string LoginFailRx = @"login incorrect[\r\n]+";
	private readonly Dictionary<int, Action<GroupCollection>> _baseActionHandlers;
	private readonly Dictionary<string, LightingItem> _scenes;
	private readonly Dictionary<string, LightingZoneItem> _zones;
	private BasicTcpClient? _client;
	private CTimer? _rxTimer;
	private bool _disposed;
	private string _userName = string.Empty;
	private int _componentNumber;
	private int _reconnectAttempts;

	public BasicQseController()
	{
		_scenes = new Dictionary<string, LightingItem>();
		_zones = new Dictionary<string, LightingZoneItem>();
		_baseActionHandlers = new Dictionary<int, Action<GroupCollection>>
		{
			{ 7, HandleSceneResponse }
		};
	}

	~BasicQseController()
	{
		Dispose(false);
	}


	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? ActiveSceneChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? ZoneLoadChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

	/// <inheritdoc/>
	public string Id { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public bool IsInitialized { get; private set; }

	/// <inheritdoc/>
	public bool IsOnline { get; private set; }

	/// <inheritdoc/>
	public string Label { get; private set; } = string.Empty;

	/// <inheritdoc />
	public string Manufacturer { get; set; } = "Lutron";

	/// <inheritdoc />
	public string Model { get; set; } = "QSE Controller";

	/// <inheritdoc/>
	public ReadOnlyCollection<string> SceneIds
	{
		get
		{
			List<string> keys = [];
			foreach (var key in _scenes.Keys)
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
			List<string> keys = [];
			foreach (var key in _zones.Keys)
			{
				keys.Add(key);
			}

			return new ReadOnlyCollection<string>(keys);
		}
	}

	/// <inheritdoc/>
	public string ActiveSceneId { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public void AddScene(string id, string label, int index)
	{
		if (
			!CheckString("AddScene", id, "id", false) ||
			!CheckString("AddScene", label, "label", false) ||
			!CheckInt("AddScene", index, 0, 1000, "index"))
		{
			return;
		}

		Logger.Debug("LutronLighting.BasicQseController.AddScene({0}, {1}, {2}", id, label, index);

		if (_scenes.ContainsKey(id))
		{
			Logger.Warn("BasicQseController.AddScene() - scene with ID {0} already exists. Replacing...", id);
			_scenes[id] = new LightingItem() { Id = id, Index = index, Label = label };
		}
		else
		{
			_scenes.Add(id, new LightingItem() { Id = id, Index = index, Label = label });
		}

	}

	/// <inheritdoc/>
	public void AddZone(string id, string label, int index)
	{
		if (
			!CheckString("AddZone", id, "id", false) ||
			!CheckString("AddZone", label, "label", false) ||
			!CheckInt("AddZone", index, 0, 1000, "index"))
		{
			return;
		}

		if (_scenes.ContainsKey(id))
		{
			Logger.Warn("BasicQseController.AddZone() - scene with ID {0} already exists. Replacing...", id);
			_zones[id] = new LightingZoneItem() { Id = id, Index = index, Label = label, Level = 0 };
		}
		else
		{
			_zones.Add(id, new LightingZoneItem() { Id = id, Index = index, Label = label, Level = 0 });
		}
	}

	/// <inheritdoc/>
	public int GetZoneLoad(string id)
	{
		if (!CheckString("GetZoneLoad", id, "id", false))
		{
			return 0;
		}

		if (_zones.TryGetValue(id, out var found))
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
		IsInitialized = false;
		DisposeClient();

		if (!CheckString("BasicQscController.Initialize", hostName, "hostName", false) ||
		    !CheckString("BasicQscController.Initialize", label, "label", false) ||
		    !CheckString("BasicQscController.Initialize", userName, "userName", true) ||
		    !CheckString("BasicQscController.Initialize", password, "password", true) ||
		    !CheckInt("BasicQscController.Initialize", port, 0, 23, "port"))
		{
			Logger.Error("LutronLighting.BasicQseController.Initialize() - Initialization failed.");
			return;
		}

		_reconnectAttempts = 0;
		ParseComponentId(tags);
		Id = id;
		Label = label;
		_userName = userName;
		_client = new BasicTcpClient(hostName, port);
		_client.ConnectionFailed += Client_ConnectionFailed;
		_client.ClientConnected += Client_ClientConnected;
		_client.RxReceived += Client_RxReceived;
		_client.StatusChanged += Client_StatusChanged;

		IsInitialized = true;
	}

	/// <inheritdoc/>
	public void RecallScene(string id)
	{
		if (!CheckInit("RecallScene") || !CheckString("RecallScene", id, "id", false))
		{
			return;
		}

		if (_client is not { Connected: true })
		{
			Logger.Error("LutronLighting.BasicQseController {0} - RecallScene() - client not connected.", Id);
			return;
		}

		if (_scenes.TryGetValue(id, out var found))
		{
			Logger.Debug("LutronLighting.BasicQseController.RecallScene({0})", id);
			Send($"#DEVICE,{Id},{_componentNumber},7,{found.Index}\r\n");
		}
	}

	/// <inheritdoc/>
	public void SetZoneLoad(string id, int loadLevel)
	{
		// TODO: BasicQseController.SetZoneLoad()
		Logger.Warn("LutronLighting.BasicQseController.SetZoneLoad() - Device {0} - output load control not yet supported.", Id);

		//if (!CheckInit("SetZoneLoad") ||
		//    !CheckString("SetZoneLoad", id, "id", false) ||
		//    CheckInt("SetZoneLoad", loadLevel, 0, 100, "loadLevel"))
		//{
		//    return;
		//}
	}

	/// <inheritdoc/>
	public void Connect()
	{
		if (_client == null || _client.Connected)
		{
			return;
		}

		_client.EnableReconnect = true;
		_client.Connect();
	}

	/// <inheritdoc/>
	public void Disconnect()
	{
		DisposeRxTimer();
		if (_client is not { Connected: true }) return;
		_client.EnableReconnect = false;
		_client.Disconnect();
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (_disposed) return;
		if (disposing)
		{
			DisposeClient();
			DisposeRxTimer();
		}

		_disposed = true;
	}

	private void RxTimeoutHandler(object? obj)
	{
		Logger.Error("LutronLighting.BasicQseController - No response to a command");
		_reconnectAttempts++;
		if (_reconnectAttempts <= 5) return;
		Disconnect();
		Connect();
	}

	private void SendSceneQuery()
	{
		Send($"?DEVICE,{Id},{_componentNumber},7\r\n");
	}

	private void Send(string cmd)
	{
		if (_rxTimer is { Disposed: false })
		{
			_rxTimer.Dispose();
			_rxTimer = null;
		}

		_rxTimer = new CTimer(RxTimeoutHandler, RxTimeoutLength);
		Logger.Debug("LutronLighting.BasicQseController {0} - Sending command {1}", Id, cmd);
		_client?.Send(cmd);
	}

	private void Client_ClientConnected(object? sender, EventArgs e)
	{
		_reconnectAttempts = 0;
		IsOnline = _client?.Connected ?? false;
		var temp = ConnectionChanged;
		temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}

	private void Client_ConnectionFailed(object? sender, GenericSingleEventArgs<Crestron.SimplSharp.CrestronSockets.SocketStatus> e)
	{
		if (_reconnectAttempts > 10)
		{
			return;
		}

		Logger.Error("LutronLighting.BasicQseController {0} - Connection failed: {1}", Id, e.Arg);
	}

	private void Client_StatusChanged(object? sender, EventArgs e)
	{
		IsOnline = _client?.Connected ?? false;
		var temp = ConnectionChanged;
		temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));

		if (!IsOnline)
		{
			DisposeRxTimer();
		}
	}

	private void Client_RxReceived(object? sender, GenericSingleEventArgs<string> e)
	{
		DisposeRxTimer();

		Logger.Debug("LutronLighting.BasicQseController - Client RX received: {0}", e.Arg);

		if (e.Arg.Contains(LoginRx))
		{
			_client?.Send($"{_userName}\r\n");
			return;
		}
		
		if (e.Arg.Contains(LoginSuccessRx))
		{
			Logger.Debug("LutronLighting.BasicQseController - Successful login received, Sending scene query.");
			SendSceneQuery();
			return;
		}
		
		if (e.Arg.Contains(LoginFailRx))
		{
			Logger.Error("LutronLighting.BasicWseController {0} - Login attempt failed.", Id);
			return;
		}

		var responses = e.Arg.Split('\n');
		foreach (var rx in responses)
		{
			var cmdMatch = new Regex(CmdRegex).Match(rx);
			if (cmdMatch.Success)
			{
				Logger.Debug("Match Found");
				switch (cmdMatch.Groups["command"].Value)
				{
					case "DEVICE":
						HandleDeviceRx(cmdMatch.Groups);
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
			var component = int.Parse(regexGroups["component"].Value);
			if (component == _componentNumber)
			{
				Logger.Debug("Base component RX received.");
				var actionId = int.Parse(regexGroups["action"].Value);
				if (_baseActionHandlers.TryGetValue(actionId, out var handler))
				{
					handler.Invoke(regexGroups);
				}
			}
			else
			{
				HandleZoneResponse(regexGroups);
			}
		}
		catch (Exception e)
		{
			Logger.Error("LutronLighting.BasicQseController {0} - failed to parse DEVICE response: {1}", Id, e);
		}
	}

	private void HandleSceneResponse(GroupCollection regexGroups)
	{
		try
		{
			int newScene = int.Parse(regexGroups["data"].Value);
			foreach (var kvp in _scenes)
			{
				if (kvp.Value.Index == newScene)
				{
					ActiveSceneId = kvp.Key;
					var temp = ActiveSceneChanged;
					temp?.Invoke(this, new GenericSingleEventArgs<string>(ActiveSceneId));

					break;
				}
			}
		}
		catch (Exception e)
		{
			Logger.Error("LutronLighting.BasicQseController {0} - failed to parse new scene ID: {1}", Id, e);
		}
	}

	private void HandleZoneResponse(GroupCollection regexGroups)
	{
		try
		{
			int index = int.Parse(regexGroups["component"].Value);
			int load = (int)float.Parse(regexGroups["data"].Value);
			foreach (var kvp in _zones)
			{
				if (kvp.Value.Index == index)
				{
					kvp.Value.Level = load;
					var temp = ZoneLoadChanged;
					temp?.Invoke(this, new GenericSingleEventArgs<string>(kvp.Value.Id));

					return;
				}
			}
		}
		catch (Exception e)
		{
			Logger.Error("LutronLighting.BasicQseController {0} - Failed to parse lighting zone response: {1}", Id, e);
		}
	}

	private void DisposeClient()
	{
		if (_client == null) return;
		_client.ConnectionFailed -= Client_ConnectionFailed;
		_client.ClientConnected -= Client_ClientConnected;
		Disconnect();
		_client.Dispose();
	}

	private void DisposeRxTimer()
	{
		_rxTimer?.Dispose();
		_rxTimer = null;
	}

	private bool CheckInit(string methodName)
	{
		if (!IsInitialized)
		{
			Logger.Error("LutronLighting.BasicQseController.{0}() - Device not initialized.", methodName);
		}

		return IsInitialized;
	}

	private static bool CheckString(string methodName, string argument, string argName, bool allowEmpty)
	{
		if (allowEmpty || !string.IsNullOrEmpty(argument)) return true;
		Logger.Error(
			"LutronLighting.BasicQseController.{0}() - argument {1} cannot be {2}}.",
			methodName,
			argName,
			allowEmpty ? "null" : "null or empty");
		
		return false;

	}

	private static bool CheckInt(string methodName, int argument, int min, int max, string argName)
	{
		if (argument >= min && argument <= max) return true;
		Logger.Error("LutronLighting.BasicQseController.{0}() - argument {1} cannot be less than {1} or greater than {2}.", methodName, argName, min, max);
		return false;

	}

	private void ParseComponentId(List<string> tags)
	{
		var componentTag = tags.FirstOrDefault(x => x.Contains("component-"));
		if (componentTag != null)
		{
			try
			{
				var values = componentTag.Split('-');
				if (values.Length > 1)
				{
					_componentNumber = int.Parse(values[1]);
				}
			}
			catch (Exception e)
			{
				Logger.Error("LutronLighting.BasicQseController - failed to parse tags for component ID: {0}", e);
				_componentNumber = 141;
			}
		}
		else
		{
			Logger.Error("LutronLighting.BasicQseController - no 'component-[ID NUMBER] tag in collection. Unable to set component ID.");
		}
	}
}