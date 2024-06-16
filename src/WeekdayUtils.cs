using System;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

namespace WeekdayBonuses;

public static class WeekdayUtils
{
	private static EventType[] nightmareEventList = [];
	private static string lastNightmareEventListValue = null;

	public static EnemyType GetEnemyType(string enemyName)
	{
		var quickMenuManager = Object.FindObjectOfType<QuickMenuManager>();

		if (quickMenuManager == null)
		{
			return null;
		}

		foreach (var enemySpawn in quickMenuManager.testAllEnemiesLevel.Enemies)
		{
			if (enemySpawn.enemyType.enemyName == enemyName)
			{
				return enemySpawn.enemyType;
			}
		}

		foreach (var enemySpawn in quickMenuManager.testAllEnemiesLevel.OutsideEnemies)
		{
			if (enemySpawn.enemyType.enemyName == enemyName)
			{
				return enemySpawn.enemyType;
			}
		}

		foreach (var enemySpawn in quickMenuManager.testAllEnemiesLevel.DaytimeEnemies)
		{
			if (enemySpawn.enemyType.enemyName == enemyName)
			{
				return enemySpawn.enemyType;
			}
		}

		return null;
	}

	public static EventType[] GetEventsForDay(DayOfWeek day)
	{
		var eventListString = Plugin.Config.EventListForDay[day].Value;

		if (!string.IsNullOrEmpty(eventListString.Trim()))
		{
			return ParseEventList(eventListString);
		}

		var eventType = Plugin.Config.EventForDay[day].Value;

		return (eventType != EventType.None) ? [eventType] : [];
	}

	public static EventType[] ParseEventList(string eventListString)
	{
		var splitEventList = eventListString.Split(',', StringSplitOptions.RemoveEmptyEntries);

		var parsedEvents = new List<EventType>();

		foreach (var eventName in splitEventList)
		{
			var eventNameNoSpaces = new string(eventName.Where(c => !char.IsWhiteSpace(c)).ToArray());

			if (Enum.TryParse<EventType>(eventNameNoSpaces, true, out var eventType))
			{
				parsedEvents.Add(eventType);
			}
			else
			{
				Plugin.Logger.LogError($"Unknown event in config: {eventName}");
			}
		}

		return parsedEvents.ToArray();
	}

	public static bool HasEvent(EventType eventType)
	{
		if (Plugin.CurrentEvents.Value.Contains(eventType))
		{
			return true;
		}

		if (Plugin.CurrentEvents.Value.Contains(EventType.Nightmare))
		{
			UpdateNightmareEventList();

			if (nightmareEventList.Contains(eventType))
			{
				return true;
			}
		}

		return false;
	}

	private static void UpdateNightmareEventList()
	{
		if (lastNightmareEventListValue != Plugin.Config.NightmareEventList.Value)
		{
			if (string.IsNullOrEmpty(Plugin.Config.NightmareEventList.Value.Trim()))
			{
				nightmareEventList = (EventType[])Enum.GetValues(typeof(EventType));
			}
			else
			{
				nightmareEventList = ParseEventList(Plugin.Config.NightmareEventList.Value);
			}

			lastNightmareEventListValue = Plugin.Config.NightmareEventList.Value;
		}
	}
}
