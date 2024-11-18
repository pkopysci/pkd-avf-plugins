namespace VirtualLighting
{
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_hardware_service.LightingDevices;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;

	public class VirtualLightingControl : ILightingDevice
	{
		private readonly Dictionary<string, LightingScene> scenes;
		private readonly Dictionary<string, LightingZone> zones;

		public VirtualLightingControl()
		{
			this.scenes = new Dictionary<string, LightingScene>();
			this.zones = new Dictionary<string, LightingZone>();
			this.ActiveSceneId = string.Empty;
		}

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ActiveSceneChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ZoneLoadChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;

		/// <inheritdoc/>
		public string ActiveSceneId
		{
			get;
			private set;
		}

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
				string[] keys = new string[this.scenes.Keys.Count];
				this.scenes.Keys.CopyTo(keys, 0);
				return new ReadOnlyCollection<string>(keys);
			}
		}

		/// <inheritdoc/>
		public ReadOnlyCollection<string> ZoneIds
		{
			get
			{
				string[] keys = new string[this.zones.Keys.Count];
				this.zones.Keys.CopyTo(keys, 0);
				return new ReadOnlyCollection<string>(keys);
			}
		}

		/// <inheritdoc/>
		public void AddScene(string id, string label, int index)
		{
			this.scenes.Add(id, new LightingScene()
			{
				Id = id,
				Index = index,
				Label = label
			});
		}

		/// <inheritdoc/>
		public void AddZone(string id, string label, int index)
		{
			this.zones.Add(id, new LightingZone()
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
			if (this.zones.TryGetValue(id, out LightingZone found))
			{
				return found.Level;
			}
			else
			{
				Logger.Error("VirtualLightingControl {0} - no zone found with ID {1}", this.Id, id);
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
			this.IsInitialized = false;
			this.Id = id;
			this.Label = label;
			this.IsInitialized = true;
		}

		/// <inheritdoc/>
		public void RecallScene(string id)
		{
			if (this.scenes.TryGetValue(id, out LightingScene found))
			{
				this.ActiveSceneId = id;
				var temp = this.ActiveSceneChanged;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(this.ActiveSceneId));
			}
		}

		/// <inheritdoc/>
		public void SetZoneLoad(string id, int loadLevel)
		{
			if (this.zones.TryGetValue(id, out LightingZone found))
			{
				found.Level = loadLevel;
				var temp = this.ZoneLoadChanged;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
			}
		}

		/// <inheritdoc/>
		public void Connect()
		{
			this.IsOnline = true;
			var temp = this.ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			this.IsOnline = false;
			var temp = this.ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}
	}
}
