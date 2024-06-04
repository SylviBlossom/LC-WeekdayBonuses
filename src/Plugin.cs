using BepInEx;
using BepInEx.Logging;
using LethalNetworkAPI;
using System;
using System.Collections.Generic;

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
	public static LethalNetworkVariable<EventType> CurrentEvent = new("currentEvent");

	public static DayOfWeek DayAtStartup;

	private void Awake()
	{
		Instance = this;
		Logger = base.Logger;
		Config = new Config(base.Config);

		DayAtStartup = DateTime.Now.DayOfWeek;
		CurrentDay.Value = DayAtStartup;

		Patches.Initialize();

		// Plugin startup logic
		Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
	}
}