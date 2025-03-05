namespace VirtualLighting;

using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_hardware_service.LightingDevices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class VirtualLightingControl : ILightingDevice
{
	private readonly Dictionary<string, LightingScene> _scenes = new();
	private readonly Dictionary<string, LightingZone> _zones = new();

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? ActiveSceneChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? ZoneLoadChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

	/// <inheritdoc/>
	public string ActiveSceneId
	{
		get;
		private set;
	} = string.Empty;

	/// <inheritdoc/>
	public string Id { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public bool IsInitialized { get; private set; }

	/// <inheritdoc/>
	public bool IsOnline { get; private set; }

	/// <inheritdoc/>
	public string Label { get; private set; } = string.Empty;

	/// <inheritdoc />
	public string Manufacturer { get; set; } = "Emulation, Inc.";

	/// <inheritdoc />
	public string Model { get; set; } = "Virtual Lighting";

	/// <inheritdoc/>
	public ReadOnlyCollection<string> SceneIds
	{
		get
		{
			var keys = new string[_scenes.Keys.Count];
			_scenes.Keys.CopyTo(keys, 0);
			return new ReadOnlyCollection<string>(keys);
		}
	}

	/// <inheritdoc/>
	public ReadOnlyCollection<string> ZoneIds
	{
		get
		{
			var keys = new string[_zones.Keys.Count];
			_zones.Keys.CopyTo(keys, 0);
			return new ReadOnlyCollection<string>(keys);
		}
	}

	/// <inheritdoc/>
	public void AddScene(string id, string label, int index)
	{
		_scenes.Add(id, new LightingScene()
		{
			Id = id,
			Index = index,
			Label = label
		});
	}

	/// <inheritdoc/>
	public void AddZone(string id, string label, int index)
	{
		_zones.Add(id, new LightingZone()
		{
			Id = id,
			Index = index,
			Label = label,
			Level = 0
		});
	}

	/// <inheritdoc/>
	public int GetZoneLoad(string id)
	{
		if (_zones.TryGetValue(id, out var found))
		{
			return found.Level;
		}
		else
		{
			Logger.Error("VirtualLightingControl {0} - no zone found with ID {1}", Id, id);
			return 0;
		}
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
		Id = id;
		Label = label;
		IsInitialized = true;
	}

	/// <inheritdoc/>
	public void RecallScene(string id)
	{
		if (!_scenes.ContainsKey(id)) return;
		ActiveSceneId = id;
		var temp = ActiveSceneChanged;
		temp?.Invoke(this, new GenericSingleEventArgs<string>(ActiveSceneId));
	}

	/// <inheritdoc/>
	public void SetZoneLoad(string id, int loadLevel)
	{
		if (!_zones.TryGetValue(id, out var found)) return;
		found.Level = loadLevel;
		var temp = ZoneLoadChanged;
		temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
	}

	/// <inheritdoc/>
	public void Connect()
	{
		IsOnline = true;
		var temp = ConnectionChanged;
		temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}

	/// <inheritdoc/>
	public void Disconnect()
	{
		IsOnline = false;
		var temp = ConnectionChanged;
		temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}
}