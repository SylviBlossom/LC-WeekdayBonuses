using System.Collections.Generic;
using UnityEngine;

namespace WeekdayBonuses;

public static class EventCallbacks
{
	private static Dictionary<SelectableLevel, int> OriginalLevelEnemyPower = new();
	private static Dictionary<SelectableLevel, int> OriginalLevelOutsideEnemyPower = new();
	private static Dictionary<EnemyType, int> OriginalEnemyMaxCounts = new();

	public static void DoubleMonsterEnabled()
	{
		OriginalEnemyMaxCounts.Clear();
		OriginalLevelEnemyPower.Clear();
		OriginalLevelOutsideEnemyPower.Clear();

		foreach (var level in StartOfRound.Instance.levels)
		{
			if (OriginalLevelEnemyPower.ContainsKey(level))
			{
				continue;
			}

			OriginalLevelEnemyPower.Add(level, level.maxEnemyPowerCount);
			OriginalLevelOutsideEnemyPower.Add(level, level.maxOutsideEnemyPowerCount);

			Plugin.Logger.LogInfo($"Max power for {level.PlanetName}: {level.maxEnemyPowerCount}");

			level.maxEnemyPowerCount = (int)(level.maxEnemyPowerCount * Plugin.Config.DoubleMonsterIndoorPowerMultiplier.Value);
			level.maxOutsideEnemyPowerCount = (int)(level.maxOutsideEnemyPowerCount * Plugin.Config.DoubleMonsterOutdoorPowerMultiplier.Value);

			foreach (var enemySpawn in level.Enemies)
			{
				DoubleMonsterApplyEnabledToEnemy(enemySpawn.enemyType);
			}

			foreach (var enemySpawn in level.OutsideEnemies)
			{
				DoubleMonsterApplyEnabledToEnemy(enemySpawn.enemyType);
			}
		}
	}

	public static void DoubleMonsterDisabled()
	{
		foreach (var level in StartOfRound.Instance.levels)
		{
			level.maxEnemyPowerCount = OriginalLevelEnemyPower[level];
			level.maxOutsideEnemyPowerCount = OriginalLevelOutsideEnemyPower[level];

			Plugin.Logger.LogInfo($"Max power for {level.PlanetName}: {level.maxEnemyPowerCount}");

			OriginalLevelEnemyPower.Remove(level);
			OriginalLevelOutsideEnemyPower.Remove(level);

			foreach (var enemySpawn in level.Enemies)
			{
				DoubleMonsterApplyDisabledToEnemy(enemySpawn.enemyType);
			}

			foreach (var enemySpawn in level.OutsideEnemies)
			{
				DoubleMonsterApplyDisabledToEnemy(enemySpawn.enemyType);
			}
		}

		OriginalEnemyMaxCounts.Clear();
		OriginalLevelEnemyPower.Clear();
		OriginalLevelOutsideEnemyPower.Clear();
	}

	private static void DoubleMonsterApplyEnabledToEnemy(EnemyType enemyType)
	{
		if (enemyType.MaxCount > 1 && !OriginalEnemyMaxCounts.ContainsKey(enemyType))
		{
			Plugin.Logger.LogInfo($"Max count for {enemyType.enemyName}: {enemyType.MaxCount}");

			OriginalEnemyMaxCounts.Add(enemyType, enemyType.MaxCount);
			enemyType.MaxCount = (int)(enemyType.MaxCount * Plugin.Config.DoubleMonsterPerEnemyCapMultiplier.Value);
		}
	}

	private static void DoubleMonsterApplyDisabledToEnemy(EnemyType enemyType)
	{
		if (enemyType.MaxCount > 1 && OriginalEnemyMaxCounts.ContainsKey(enemyType))
		{
			enemyType.MaxCount = OriginalEnemyMaxCounts[enemyType];
			OriginalEnemyMaxCounts.Remove(enemyType);

			Plugin.Logger.LogInfo($"Max count for {enemyType.enemyName}: {enemyType.MaxCount}");
		}
	}

	public static void BlackFridaySettingChanged()
	{
		if (GameNetworkManager.Instance.localPlayerController == null)
		{
			return;
		}

		var terminal = Object.FindObjectOfType<Terminal>();

		if (terminal != null)
		{
			Patches.ForceTerminalSalesChange = true;
			terminal?.SetItemSales();
			Patches.ForceTerminalSalesChange = false;
		}
	}
}
