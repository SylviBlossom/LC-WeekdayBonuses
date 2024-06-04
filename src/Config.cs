using BepInEx.Configuration;
using CSync.Extensions;
using CSync.Lib;
using System;
using System.Collections.Generic;

namespace WeekdayBonuses;

public class Config : SyncedConfig2<Config>
{
	public ConfigEntry<bool> AllowDayChange;

	public Dictionary<DayOfWeek, ConfigEntry<EventType>> EventForDay = new();

	public ConfigEntry<float> DoubleLootMultiplier;
	[SyncedEntryField] public SyncedEntry<float> SmallFacilitySize;
	public ConfigEntry<float> EasterEggRate;

	public Config(ConfigFile cfg) : base(PluginInfo.PLUGIN_GUID)
	{
		AllowDayChange = cfg.Bind("GeneralSettings", "AllowDayChange", true, "Whether the day of the week checked for the current bonus event changes in realtime. If disabled, the game will keep the same bonus event from when you launched the game.");

		EventForDay.Clear();
		BindEvent(cfg, DayOfWeek.Monday, EventType.None);
		BindEvent(cfg, DayOfWeek.Tuesday, EventType.None);
		BindEvent(cfg, DayOfWeek.Wednesday, EventType.DoubleLoot);
		BindEvent(cfg, DayOfWeek.Thursday, EventType.None);
		BindEvent(cfg, DayOfWeek.Friday, EventType.SmallFacility);
		BindEvent(cfg, DayOfWeek.Saturday, EventType.None);
		BindEvent(cfg, DayOfWeek.Sunday, EventType.Easter);

		DoubleLootMultiplier = cfg.Bind("TweakDoubleLoot", "ScrapMultiplier", 2f, "Multiplier for the amount of scrap to be spawned on Double Loot days.");
		SmallFacilitySize = cfg.BindSyncedEntry("TweakSmallFacility", "FacilitySize", 0.8f, "Static map size multiplier that should be applied on Small Facility days.");
		EasterEggRate = cfg.Bind("TweakEaster", "EggSpawnRate", 0.25f, "Easter days spawn an additional number of eggs based on this multiplier applied to the amount of scrap spawned in the facility, e.g. 0.25 means there will be 1.25x the total amount of scrap (and 1/5 will be eggs).");

		ConfigManager.Register(this);
	}

	private void BindEvent(ConfigFile cfg, DayOfWeek dayOfWeek, EventType defaultEvent)
	{
		var newEntry = cfg.Bind("GeneralEvents", $"{dayOfWeek}Event", defaultEvent, $"Which bonus event should occur on {dayOfWeek}.");

		newEntry.SettingChanged += OnScheduleSettingChanged;

		EventForDay.Add(dayOfWeek, newEntry);
	}

	private void OnScheduleSettingChanged(object sender, EventArgs e)
	{
		var dayOfWeek = Plugin.CurrentDay.Value;
		Plugin.CurrentEvent.Value = Plugin.Config.EventForDay[dayOfWeek].Value;
	}
}