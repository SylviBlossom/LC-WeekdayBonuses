using BepInEx;
using BepInEx.Logging;
using LethalNetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WeekdayBonuses;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("LethalNetworkAPI")]
[BepInDependency("com.sigurd.csync", "5.0.0")]
public class Plugin : BaseUnityPlugin
{
	public static Plugin Instance { get; private set; }
	public static new ManualLogSource Logger { get; private set; }
	public static new Config Config { get; private set; }

	public static LethalNetworkVariable<DayOfWeek> CurrentDay = new("currentDay");
	public static LethalNetworkVariable<EventType[]> CurrentEvents = new("currentEvents");

	public static DayOfWeek DayAtStartup;
	public static EventType[] LastEvents = [];

	private void Awake()
	{
		Instance = this;
		Logger = base.Logger;
		Config = new Config(base.Config);

		CurrentEvents.OnValueChanged += CurrentEvents_OnValueChanged;

		DayAtStartup = DateTime.Now.DayOfWeek;
		CurrentDay.Value = DayAtStartup;
		CurrentEvents.Value = WeekdayUtils.GetEventsForDay(DayAtStartup);

		Patches.Initialize();

		// Plugin startup logic
		Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
	}

	private void CurrentEvents_OnValueChanged(EventType[] eventTypes)
	{
		var enabledEvents = new List<EventType>();
		var disabledEvents = new List<EventType>();

		foreach (EventType eventType in Enum.GetValues(typeof(EventType)))
		{
			var newContains = eventTypes.Contains(eventType);
			var oldContains = LastEvents.Contains(eventType);

			if (newContains && !oldContains)
			{
				enabledEvents.Add(eventType);
			}
			else if (oldContains && !newContains)
			{
				disabledEvents.Add(eventType);
			}
		}

		LastEvents = eventTypes;

		if (StartOfRound.Instance == null || !StartOfRound.Instance.enabled)
		{
			return;
		}

		CallEventsDisabled(disabledEvents.ToArray());
		CallEventsEnabled(enabledEvents.ToArray());
	}

	public static void CallEventsEnabled(EventType[] events)
	{
		if (events.Contains(EventType.DoubleMonster))
		{
			EventCallbacks.DoubleMonsterEnabled();
		}
		if (events.Contains(EventType.Black))
		{
			EventCallbacks.BlackFridaySettingChanged();
		}
	}

	public static void CallEventsDisabled(EventType[] events)
	{
		if (events.Contains(EventType.DoubleMonster))
		{
			EventCallbacks.DoubleMonsterDisabled();
		}
		if (events.Contains(EventType.Black))
		{
			EventCallbacks.BlackFridaySettingChanged();
		}
	}
}