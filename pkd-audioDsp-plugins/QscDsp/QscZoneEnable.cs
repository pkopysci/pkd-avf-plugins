﻿namespace QscDsp
{
	using Crestron.SimplSharp;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Class for storing and controlling audio signal enable mute toggles.
	/// </summary>
	internal class QscZoneEnable
	{
		private readonly Dictionary<string, QscBoolNamedControl> zoneToggles;

		/// <summary>
		/// Creates an instance of <see cref="QscZoneEnable"/> class.
		/// </summary>
		public QscZoneEnable()
		{
			this.zoneToggles = new Dictionary<string, QscBoolNamedControl>();
		}

		/// <summary>
		/// Triggered whenever a monitored zone enable toggle control state is updated from the core.
		/// </summary>
		public event EventHandler<GenericSingleEventArgs<string>> ZoneControlChanged;

		/// <summary>
		/// Add a new zone control object to the internal collection. If an existing object is detected then the new
		/// add request is ignored.
		/// </summary>
		/// <param name="zoneId">The unique ID of the zone object. used for internal referencing.</param>
		/// <param name="controlTag">The DSP desgin named control or instance tag used for commands.</param>
		public bool TryAddZone(string zoneId, string controlTag)
		{
			if (this.zoneToggles.ContainsKey(zoneId))
			{
				return false;
			}

			QscBoolNamedControl newZoneToggle = new QscBoolNamedControl
			{
				Id = zoneId,
				ControlTag = controlTag,
				newNamedControlIntChange = (str, val) => { this.ZoneControlChangeHandler(zoneId, val, str); }
			};
			this.zoneToggles.Add(zoneId, newZoneToggle);
			return true;
		}

		/// <summary>
		/// Remove the target control object from the internal collection and unregister it with the DSP. Does nothing if no control
		/// with a matching ID is found.
		/// </summary>
		/// <param name="zoneId">The uinique ID of the the zone control object to remove.</param>
		public bool TryRemoveZone(string zoneId)
		{
			if (!this.zoneToggles.ContainsKey(zoneId))
			{
				return false;
			}

			this.zoneToggles.Remove(zoneId);
			return true;
		}

		/// <summary>
		/// Send a request to the target control object to flip the current state.
		/// Does nothing if no control with a matching ID is found.
		/// </summary>
		/// <param name="zoneId">The unique ID of the zone control object to change.</param>
		public void ToggleZone(string zoneId)
		{
			if (this.zoneToggles.TryGetValue(zoneId, out QscBoolNamedControl target))
			{

				int newState = target.CurrentState ? 1 : 0;
				target.SetBoolean(newState);
			}
		}

		/// <summary>
		/// Gets the current state of the zone enable control object.
		/// </summary>
		/// <param name="zoneId">The unique ID of the control object to query.</param>
		/// <returns>The current state of the control or false if no object with a matching ID is found.</returns>
		public bool QueryZone(string zoneId)
		{
			if (this.zoneToggles.TryGetValue(zoneId, out QscBoolNamedControl target))
			{
				return target.CurrentState;
			}

			return false;
		}

		/// <summary>
		/// Register all zone control objects that have been added.
		/// </summary>
		/// <param name="coreId">The unique ID of the core that the objects will be registered to.</param>
		public void Register(string coreId)
		{
			foreach (var kvp in this.zoneToggles)
			{
				kvp.Value.Initialize(coreId, kvp.Value.ControlTag, 1);
			}
		}

		private void ZoneControlChangeHandler(string zoneId, short shortData, SimplSharpString stringData)
		{
			Logger.Debug("QscZoneEnable.ZoneControlChangeHandler({0}, {1}. {2})", zoneId, shortData, stringData);

			if (this.zoneToggles.TryGetValue(zoneId, out QscBoolNamedControl target))
			{
				target.CurrentState = shortData == 0;
			}

			var temp = this.ZoneControlChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(zoneId));
		}
	}
}