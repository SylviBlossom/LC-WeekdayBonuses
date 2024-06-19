using BepInEx.Configuration;
using CSync.Extensions;
using CSync.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WeekdayBonuses;

public class Config : SyncedConfig2<Config>
{
	// General Settings
	public ConfigEntry<bool> AllowDayChange;

	// General Events
	public Dictionary<DayOfWeek, ConfigEntry<EventType>> EventForDay = new();
	public Dictionary<DayOfWeek, ConfigEntry<string>> EventListForDay = new();

	// Tweak Double Monster
	public ConfigEntry<bool> DoubleMonsterAlwaysSpawnOldBird;
	public ConfigEntry<int> DoubleMonsterOldBirdActivationTime;
	public ConfigEntry<float> DoubleMonsterPerEnemyCapMultiplier;
	public ConfigEntry<float> DoubleMonsterIndoorPowerMultiplier;
	public ConfigEntry<float> DoubleMonsterIndoorRateMultiplier;
	public ConfigEntry<float> DoubleMonsterOutdoorPowerMultiplier;
	public ConfigEntry<float> DoubleMonsterOutdoorRateMultiplier;

	// Tweak Double Trap
	public ConfigEntry<float> DoubleTrapMultiplier;
	public ConfigEntry<float> DoubleTrapRollBaseline;
	[SyncedEntryField] public SyncedEntry<float> DoubleTrapBuffLandmineDelay;
	[SyncedEntryField] public SyncedEntry<int> DoubleTrapBuffLandmineNonLethalDamage;
	public ConfigEntry<bool> DoubleTrapEnableTurrets;
	public ConfigEntry<bool> DoubleTrapEnableLandmines;
	public ConfigEntry<bool> DoubleTrapEnableSpikeTraps;
	public ConfigEntry<bool> DoubleTrapEnableOther;

	// Tweak Double Loot
	public ConfigEntry<float> DoubleLootMultiplier;

	// Tweak Small Facility
	[SyncedEntryField] public SyncedEntry<bool> SmallFacilityUseFixedSize;
	[SyncedEntryField] public SyncedEntry<float> SmallFacilityFixedMapSize;
	[SyncedEntryField] public SyncedEntry<float> SmallFacilityMapSizeMultiplier;

	// Tweak Black Friday
	[SyncedEntryField] public SyncedEntry<SaleMode> BlackFridaySaleMode;
	[SyncedEntryField] public SyncedEntry<int> BlackFridayRandomSaleMinimum;
	[SyncedEntryField] public SyncedEntry<int> BlackFridayCustomSale;

	// Tweak Easter
	public ConfigEntry<float> EasterEggSpawnRate;

	// Tweak Nightmare
	[SyncedEntryField] public SyncedEntry<string> NightmareEventList;

	public Config(ConfigFile cfg) : base(PluginInfo.PLUGIN_GUID)
	{
		// General Settings
		AllowDayChange = cfg.Bind("GeneralSettings", "AllowDayChange", true, "Whether the day of the week checked for the current bonus event changes in realtime. If disabled, the game will keep the same bonus event from when you launched the game.");

		// General Events
		EventForDay.Clear();
		BindEvent(cfg, DayOfWeek.Monday, EventType.DoubleMonster);
		BindEvent(cfg, DayOfWeek.Tuesday, EventType.DoubleTrap);
		BindEvent(cfg, DayOfWeek.Wednesday, EventType.DoubleLoot);
		BindEvent(cfg, DayOfWeek.Thursday, EventType.SmallFacility);
		BindEvent(cfg, DayOfWeek.Friday, EventType.Black);
		BindEvent(cfg, DayOfWeek.Saturday, EventType.Nightmare);
		BindEvent(cfg, DayOfWeek.Sunday, EventType.Easter);

		// General Event Lists
		EventListForDay.Clear();
		BindEventList(cfg, DayOfWeek.Monday);
		BindEventList(cfg, DayOfWeek.Tuesday);
		BindEventList(cfg, DayOfWeek.Wednesday);
		BindEventList(cfg, DayOfWeek.Thursday);
		BindEventList(cfg, DayOfWeek.Friday);
		BindEventList(cfg, DayOfWeek.Saturday);
		BindEventList(cfg, DayOfWeek.Sunday);

		// Tweak Double Monster
		DoubleMonsterAlwaysSpawnOldBird = cfg.Bind("TweakDoubleMonster", "AlwaysSpawnOldBird", true, "Ensures an Old Bird spawns outside on Double Monster days.");
		DoubleMonsterOldBirdActivationTime = cfg.Bind("TweakDoubleMonster", "OldBirdActivationTime", 18, "If the Old Bird spawn is enabled, this is the time the guaranteed Old Bird awakens (24 hour time). Default is ~6pm.");
		DoubleMonsterPerEnemyCapMultiplier = cfg.Bind("TweakDoubleMonster", "PerEnemyCapMultiplier", 2f, "Multiplies the max spawn count for each individual enemy (unless its max is 1) on Double Monster days.");
		DoubleMonsterIndoorPowerMultiplier = cfg.Bind("TweakDoubleMonster", "IndoorPowerMultiplier", 2f, "Multiplies the max power level indoors on Double Monster days.");
		DoubleMonsterIndoorRateMultiplier = cfg.Bind("TweakDoubleMonster", "IndoorRateMultiplier", 2f, "Multiplies how many indoor enemies spawn per hour on Double Monster days.");
		DoubleMonsterOutdoorPowerMultiplier = cfg.Bind("TweakDoubleMonster", "OutdoorPowerMultiplier", 2f, "Multiplies the max power level outdoors on Double Monster days.");
		DoubleMonsterOutdoorRateMultiplier = cfg.Bind("TweakDoubleMonster", "OutdoorRateMultiplier", 2f, "Multiplies how many outdoor enemies spawn per hour on Double Monster days.");

		DoubleMonsterPerEnemyCapMultiplier.SettingChanged += OnDoubleMonsterVariableSettingChanged;
		DoubleMonsterIndoorPowerMultiplier.SettingChanged += OnDoubleMonsterVariableSettingChanged;
		DoubleMonsterOutdoorPowerMultiplier.SettingChanged += OnDoubleMonsterVariableSettingChanged;

		// Tweak Double Trap
		DoubleTrapMultiplier = cfg.Bind("TweakDoubleTrap", "TrapMultiplier", 1f, "Overall multiplier of the number of traps spawned on Double Trap days.");
		DoubleTrapRollBaseline = cfg.Bind("TweakDoubleTrap", "RollBaseline", 0.5f, "The minimum roll (between 0 and 1) for trap counts during Double Trap days.\nTrap spawn rates are on a curve, and the game rolls a point between 0 - 1 on the curve each day. If this is set to 0.5, it will be between 0.5 - 1, making every day a relatively high roll.");
		DoubleTrapBuffLandmineDelay = cfg.BindSyncedEntry("TweakDoubleTrap", "BuffLandmineDelay", 0.5f, "Additional delay in seconds between stepping on a landmine and it exploding on Double Trap days.");
		DoubleTrapBuffLandmineNonLethalDamage = cfg.BindSyncedEntry("TweakDoubleTrap", "BuffLandmineNonLethalDamage", 30, "Modified damage the non-lethal (outside) radius of landmine explosions deal on Double Trap days.\nVanilla value is 50 damage.");
		DoubleTrapEnableTurrets = cfg.Bind("TweakDoubleTrap", "EnableTurrets", true, "Whether turret amounts will be affected on Double Trap days.");
		DoubleTrapEnableLandmines = cfg.Bind("TweakDoubleTrap", "EnableLandmines", true, "Whether landmine amounts will be affected on Double Trap days.");
		DoubleTrapEnableSpikeTraps = cfg.Bind("TweakDoubleTrap", "EnableSpikeTraps", true, "Whether spike trap amounts will be affected on Double Trap days.");
		DoubleTrapEnableOther = cfg.Bind("TweakDoubleTrap", "EnableOther", true, "Whether the amounts of any hazards not mentioned here (likely modded ones) will be affected on Double Trap days.");

		// Tweak Double Loot
		DoubleLootMultiplier = cfg.Bind("TweakDoubleLoot", "ScrapMultiplier", 2f, "Multiplier for the amount of scrap to be spawned on Double Loot days.");

		// Tweak Small Facility
		SmallFacilityUseFixedSize = cfg.BindSyncedEntry("TweakSmallFacility", "UseFixedSize", true, "Whether Small Facility days should set the facility size the same on every moon using the Fixed Size option, or use the Size Multiplier option.");
		SmallFacilityFixedMapSize = cfg.BindSyncedEntry("TweakSmallFacility", "FixedMapSize", 0.8f, "Static map size multiplier that should be used on every moon on Small Facility days when using fixed size.");
		SmallFacilityMapSizeMultiplier = cfg.BindSyncedEntry("TweakSmallFacility", "MapSizeMultiplier", 0.5f, "Map size multiplier that should be applied on Small Facility days when not using fixed size.");

		// Tweak Black (Friday)
		BlackFridaySaleMode = cfg.BindSyncedEntry("TweakBlackFriday", "SaleMode", SaleMode.Random, "Configures how Black Friday sales should work.\n- Random: Sales are random between the specified minimum, and the maximum possible sale for each item.\n- Highest: All items are on sale for their highest possible amount.\n- Custom: All items are on sale for the specified amount.");
		BlackFridayRandomSaleMinimum = cfg.BindSyncedEntry("TweakBlackFriday", "RandomSaleMinimum", 10, "The minimum sale value of each item using Random sale mode. May be overridden if the item's highest sale percentage is lower.");
		BlackFridayCustomSale = cfg.BindSyncedEntry("TweakBlackFriday", "CustomSale", 50, "The sale percentage of each item using Custom sale mode. Overrides items' individual maximum sale values.");

		BlackFridaySaleMode.Changed += OnBlackFridaySettingChanged;
		BlackFridayRandomSaleMinimum.Changed += OnBlackFridaySettingChanged;
		BlackFridayCustomSale.Changed += OnBlackFridaySettingChanged;

		// Tweak Easter
		EasterEggSpawnRate = cfg.Bind("TweakEaster", "EggSpawnRate", 0.25f, "Easter days spawn an additional number of eggs based on this multiplier applied to the amount of scrap spawned in the facility, e.g. 0.25 means there will be 1.25x the total amount of scrap (and 1/5 will be eggs).");

		// Tweak Nightmare
		NightmareEventList = cfg.BindSyncedEntry("TweakNightmare", "EventList", "", "Comma-separated list of other event names used on Nightmare days. If empty, uses all events.\nEvent names should be formatted like they are in the multiple-choice settings (e.g. \"DoubleLoot,DoubleMonster,Easter\")");

		ConfigManager.Register(this);
	}

	private void BindEvent(ConfigFile cfg, DayOfWeek dayOfWeek, EventType defaultEvent)
	{
		var newEntry = cfg.Bind("GeneralEvents", $"{dayOfWeek}Event", defaultEvent, $"Which bonus event should occur on {dayOfWeek}.");

		newEntry.SettingChanged += OnScheduleSettingChanged;

		EventForDay.Add(dayOfWeek, newEntry);
	}

	private void BindEventList(ConfigFile cfg, DayOfWeek dayOfWeek)
	{
		var newEntry = cfg.Bind("GeneralEventLists", $"{dayOfWeek}EventList", "", $"Comma-separated list of names of bonus events that should occur on {dayOfWeek}. Overrides the basic settings.\nEvent names should be formatted like they are in the multiple-choice settings (e.g. \"DoubleLoot,DoubleMonster,Easter\")");

		newEntry.SettingChanged += OnScheduleSettingChanged;

		EventListForDay.Add(dayOfWeek, newEntry);
	}

	private void OnScheduleSettingChanged(object sender, EventArgs e)
	{
		var dayOfWeek = Plugin.CurrentDay.Value;
		Plugin.CurrentEvents.Value = WeekdayUtils.GetEventsForDay(dayOfWeek);
	}

	private void OnDoubleMonsterVariableSettingChanged(object sender, EventArgs e)
	{
		if (StartOfRound.Instance != null && StartOfRound.Instance.enabled && WeekdayUtils.HasEvent(EventType.DoubleMonster))
		{
			EventCallbacks.DoubleMonsterDisabled();
			EventCallbacks.DoubleMonsterEnabled();
		}
	}

	private void OnBlackFridaySettingChanged<T>(object sender, SyncedSettingChangedEventArgs<T> e)
	{
		var terminal = Object.FindObjectOfType<Terminal>();
		terminal?.SetItemSales();
	}
}