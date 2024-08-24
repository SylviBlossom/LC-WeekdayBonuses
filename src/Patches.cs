using DunGen;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine.AI;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace WeekdayBonuses;

public static class Patches
{
	public static bool ForceTerminalSalesChange;

	private static bool DoubleMonsterSpawnRobot;
	private static float DoubleMonsterRobotSpawnTime;

	public static void Initialize()
	{
		On.StartOfRound.OnEnable += StartOfRound_OnEnable;
		On.StartOfRound.OnDisable += StartOfRound_OnDisable;
		On.StartOfRound.Start += StartOfRound_Start;
		On.StartOfRound.Update += StartOfRound_Update;
		IL.Terminal.TextPostProcess += Terminal_TextPostProcess_IL;

		IL.Terminal.SetItemSales += Terminal_SetItemSales_IL;
		On.Terminal.SetItemSales += Terminal_SetItemSales;
		On.Landmine.TriggerMineOnLocalClientByExiting += Landmine_TriggerMineOnLocalClientByExiting;
		IL.Landmine.ExplodeMineClientRpc += Landmine_ExplodeMineClientRpc_IL;
		On.Landmine.Detonate += Landmine_Detonate;
		IL.Landmine.Detonate += Landmine_Detonate_IL;
		On.RoundManager.SpawnScrapInLevel += RoundManager_SpawnScrapInLevel;
		On.RoundManager.GenerateNewFloor += RoundManager_GenerateNewFloor;
		IL.RoundManager.SpawnScrapInLevel += RoundManager_SpawnScrapInLevel_IL;
		IL.RoundManager.SpawnMapObjects += RoundManager_SpawnMapObjects_IL;
		IL.RoundManager.SpawnEnemiesOutside += RoundManager_SpawnEnemiesOutside_IL;
		IL.RoundManager.PredictAllOutsideEnemies += RoundManager_PredictAllOutsideEnemies_IL;
		IL.RoundManager.PlotOutEnemiesForNextHour += RoundManager_PlotOutEnemiesForNextHour_IL;
	}

	private static void Terminal_SetItemSales_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		ILLabel gotoLabel = null;

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchBgt(out gotoLabel)))
		{
			Plugin.Logger.LogError("Failed IL hook for Terminal.SetItemSales @ Randomly don't change sales");
			return;
		}

		cursor.EmitDelegate(() => ForceTerminalSalesChange);
		cursor.Emit(OpCodes.Brtrue, gotoLabel);
	}

	private static void Landmine_Detonate_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcI4(50)))
		{
			Plugin.Logger.LogError("Failed IL hook for Landmine.Detonate @ Set non-lethal damage to 50");
			return;
		}

		cursor.EmitDelegate<Func<int, int>>(damage =>
		{
			if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleTrap))
			{
				return damage;
			}

			var customDamage = Plugin.Config.DoubleTrapBuffLandmineNonLethalDamage.Value;

			return customDamage >= 0 ? customDamage : damage;
		});
	}

	private static void Landmine_Detonate(On.Landmine.orig_Detonate orig, Landmine self)
	{
		if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleTrap))
		{
			orig(self);
			return;
		}

		var modLandmine = self.GetComponent<ModLandmine>();

		if (modLandmine == null || !modLandmine.DelayExplosion)
		{
			orig(self);
			return;
		}

		modLandmine.TryDetonate();
	}

	private static void Landmine_TriggerMineOnLocalClientByExiting(On.Landmine.orig_TriggerMineOnLocalClientByExiting orig, Landmine self)
	{
		if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleTrap))
		{
			orig(self);
			return;
		}

		DelayLandmineDetonation(self);

		orig(self);
	}

	private static void Landmine_ExplodeMineClientRpc_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		if (!cursor.TryGotoNext(MoveType.AfterLabel,
				instr => instr.MatchLdarg(0),
				instr => instr.MatchCallOrCallvirt<Landmine>("SetOffMineAnimation")))
		{
			Plugin.Logger.LogError("Failed IL hook for Landmine.ExplodeMineClientRpc @ SetOffMineAnimation");
			return;
		}

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.EmitDelegate<Action<Landmine>>(self =>
		{
			if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleTrap))
			{
				return;
			}

			DelayLandmineDetonation(self);
		});
	}

	private static void DelayLandmineDetonation(Landmine landmine)
	{
		var modLandmine =
			landmine.GetComponent<ModLandmine>() ??
			landmine.gameObject.AddComponent<ModLandmine>();

		modLandmine.DelayExplosion = true;
	}

	private static void Terminal_SetItemSales(On.Terminal.orig_SetItemSales orig, Terminal self)
	{
		orig(self);

		if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.Black))
		{
			return;
		}

		var random = new Random(StartOfRound.Instance.randomMapSeed + 90);

		for (var i = 0; i < self.itemSalesPercentages.Length; i++)
		{
			var highestSale = 80;

			if (i < self.buyableItemsList.Length)
			{
				highestSale = self.buyableItemsList[i].highestSalePercentage;
			}

			switch (Plugin.Config.BlackFridaySaleMode.Value)
			{
				case SaleMode.Random:
				{
					var minValue = Mathf.Clamp(Plugin.Config.BlackFridayRandomSaleMinimum.Value, 0, highestSale);
					var maxValue = Mathf.Clamp(highestSale, 0, 90);
					var sale = self.RoundToNearestTen(100 - random.Next(minValue, maxValue + 1));

					self.itemSalesPercentages[i] = sale;
					break;
				}
				case SaleMode.Highest:
				{
					var maxValue = Mathf.Clamp(highestSale, 0, 90);
					var sale = self.RoundToNearestTen(100 - maxValue);

					self.itemSalesPercentages[i] = sale;
					break;
				}
				case SaleMode.Custom:
				{
					var customValue = Mathf.Clamp(Plugin.Config.BlackFridayCustomSale.Value, 0, 100);
					var sale = 100 - customValue;

					self.itemSalesPercentages[i] = sale;
					break;
				}
			}
		}
	}

	private static void RoundManager_SpawnMapObjects_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		var randomLoc = 0;
		var mapObjectIndexLoc = 6;
		var spawnedObjectCountLoc = 4;

		if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchStloc(spawnedObjectCountLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for RoundManager.SpawnMapObjects @ Calculate number of hazards");
			return;
		}

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc, mapObjectIndexLoc);
		cursor.Emit(OpCodes.Ldloc, randomLoc);
		cursor.EmitDelegate<Func<int, RoundManager, int, Random, int>>((amount, self, i, random) =>
		{
			if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleTrap))
			{
				return amount;
			}

			var hazard = self.currentLevel.spawnableMapObjects[i];

			var isEnabled = false;
			switch (hazard.prefabToSpawn.name)
			{
				case "TurretContainer":
					isEnabled = Plugin.Config.DoubleTrapEnableTurrets.Value; break;
				case "Landmine":
					isEnabled = Plugin.Config.DoubleTrapEnableLandmines.Value; break;
				case "SpikeRoofTrapHazard":
					isEnabled = Plugin.Config.DoubleTrapEnableSpikeTraps.Value; break;
				default:
					isEnabled = Plugin.Config.DoubleTrapEnableOther.Value; break;
			}

			if (!isEnabled)
			{
				return amount;
			}

			if (Plugin.Config.DoubleTrapRollBaseline.Value > 0f)
			{
				var range = 1f - Plugin.Config.DoubleTrapRollBaseline.Value;
				var roll = ((float)random.NextDouble() * range) + Plugin.Config.DoubleTrapRollBaseline.Value;

				amount = (int)hazard.numberToSpawn.Evaluate(roll);
			}

			return (int)(amount * Plugin.Config.DoubleTrapMultiplier.Value);
		});
	}

	private static void RoundManager_PlotOutEnemiesForNextHour_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		var spawnedEnemyCountLoc = 1;

		if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchStloc(spawnedEnemyCountLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for RoundManager.PlotOutEnemiesForNextHour @ Evaluate enemy curve");
			return;
		}

		cursor.EmitDelegate<Func<float, float>>(orig =>
		{
			if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleMonster))
			{
				return orig;
			}

			if (orig < 0f)
			{
				return orig;
			}

			return orig * Plugin.Config.DoubleMonsterIndoorRateMultiplier.Value;
		});
	}

	private static void StartOfRound_OnEnable(On.StartOfRound.orig_OnEnable orig, StartOfRound self)
	{
		orig(self);
		Plugin.CallEventsEnabled(Plugin.CurrentEvents.Value);
	}

	private static void StartOfRound_OnDisable(On.StartOfRound.orig_OnDisable orig, StartOfRound self)
	{
		Plugin.CallEventsDisabled(Plugin.CurrentEvents.Value);
		orig(self);
	}

	private static void RoundManager_PredictAllOutsideEnemies_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		var randomSeedLoc = 9;
		var spawnedEnemyCountLoc = 4;

		if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchStloc(spawnedEnemyCountLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for RoundManager.PredictAllOutsideEnemies @ Evaluate enemy curve");
			return;
		}

		cursor.EmitDelegate<Func<float, float>>(orig =>
		{
			if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleMonster))
			{
				return orig;
			}

			if (orig < 0f)
			{
				return orig;
			}

			return orig * Plugin.Config.DoubleMonsterOutdoorRateMultiplier.Value;
		});

		if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchLdfld<RoundManager>("enemyNestSpawnObjects")))
		{
			Plugin.Logger.LogError("Failed IL hook for RoundManager.PredictAllOutsideEnemies @ After spawn prediction loop");
			return;
		}

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc, randomSeedLoc);
		cursor.EmitDelegate<Action<RoundManager, Random>>((self, anomalyRandom) =>
		{
			DoubleMonsterSpawnRobot = false;
			DoubleMonsterRobotSpawnTime = 0f;

			if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleMonster))
			{
				return;
			}

			if (!Plugin.Config.DoubleMonsterAlwaysSpawnOldBird.Value)
			{
				return;
			}

			DoubleMonsterSpawnRobot = true;
			DoubleMonsterRobotSpawnTime = self.timeScript.lengthOfHours * (Plugin.Config.DoubleMonsterOldBirdActivationTime.Value - 6);

			var robotEnemy = WeekdayUtils.GetEnemyType("RadMech");

			self.SpawnNestObjectForOutsideEnemy(robotEnemy, anomalyRandom);
		});
	}

	private static void RoundManager_SpawnEnemiesOutside_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		var spawnedEnemyCountLoc = 1;

		if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchStloc(spawnedEnemyCountLoc)))
		{
			Plugin.Logger.LogError("Failed IL hook for RoundManager.SpawnEnemiesOutside @ Evaluate enemy curve");
			return;
		}

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.EmitDelegate<Func<float, RoundManager, float>>((orig, self) =>
		{
			if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleMonster))
			{
				return orig;
			}

			var currentTime = self.timeScript.lengthOfHours * self.currentHour;

			if (DoubleMonsterSpawnRobot && currentTime >= DoubleMonsterRobotSpawnTime)
			{
				// Also spawn robot here since we might as well
				SpawnRobot();

				DoubleMonsterSpawnRobot = false;
				DoubleMonsterRobotSpawnTime = 0f;
			}

			if (orig < 0f)
			{
				return orig;
			}

			return orig * Plugin.Config.DoubleMonsterOutdoorRateMultiplier.Value;
		});
	}

	private static void SpawnRobot()
	{
		var roundManager = RoundManager.Instance;
		var spawnPoints = GameObject.FindGameObjectsWithTag("OutsideAINode");
		var robotEnemy = WeekdayUtils.GetEnemyType("RadMech");

		if (robotEnemy.requireNestObjectsToSpawn)
		{
			bool foundNest = false;
			EnemyAINestSpawnObject[] array = Object.FindObjectsByType<EnemyAINestSpawnObject>(FindObjectsSortMode.None);
			for (int j = 0; j < array.Length; j++)
			{
				if (array[j].enemyType == robotEnemy)
				{
					foundNest = true;
					break;
				}
			}
			if (!foundNest)
			{
				return;
			}
		}
		var spawnGroupCount = Mathf.Max(robotEnemy.spawnInGroupsOf, 1);
		var i = 0;
		while (i < spawnGroupCount)
		{
			Vector3 spawnPoint = spawnPoints[roundManager.AnomalyRandom.Next(0, spawnPoints.Length)].transform.position;
			spawnPoint = roundManager.GetRandomNavMeshPositionInBoxPredictable(spawnPoint, 10f, default, roundManager.AnomalyRandom, roundManager.GetLayermaskForEnemySizeLimit(robotEnemy));
			spawnPoint = roundManager.PositionWithDenialPointsChecked(spawnPoint, spawnPoints, robotEnemy);
			var enemyPrefab = Object.Instantiate(robotEnemy.enemyPrefab, spawnPoint, Quaternion.Euler(Vector3.zero));
			enemyPrefab.gameObject.GetComponentInChildren<NetworkObject>().Spawn(true);
			roundManager.SpawnedEnemies.Add(enemyPrefab.GetComponent<EnemyAI>());
			i++;
		}
	}

	private static void StartOfRound_Start(On.StartOfRound.orig_Start orig, StartOfRound self)
	{
		if (self.IsServer)
		{
			var dayOfWeek = Plugin.Config.AllowDayChange.Value ? DateTime.Now.DayOfWeek : Plugin.DayAtStartup;

			Plugin.CurrentDay.Value = dayOfWeek;
			Plugin.CurrentEvents.Value = WeekdayUtils.GetEventsForDay(dayOfWeek);
		}

		orig(self);
	}

	private static void StartOfRound_Update(On.StartOfRound.orig_Update orig, StartOfRound self)
	{
		orig(self);

		if (self.IsServer && Plugin.Config.AllowDayChange.Value)
		{
			var dayOfWeek = DateTime.Now.DayOfWeek;

			if (Plugin.CurrentDay.Value != DateTime.Now.DayOfWeek)
			{
				Plugin.CurrentDay.Value = dayOfWeek;
				Plugin.CurrentEvents.Value = WeekdayUtils.GetEventsForDay(dayOfWeek);
			}
		}
	}

	private static void Terminal_TextPostProcess_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		if (!cursor.TryGotoNext(instr => instr.MatchLdstr("[currentDay]")))
		{
			Plugin.Logger.LogError("Failed IL hook for Terminal.TextPostProcess @ [currentDay]");
			return;
		}

		if (!cursor.TryGotoNext(MoveType.AfterLabel, instr => instr.MatchCallOrCallvirt<string>("Replace")))
		{
			Plugin.Logger.LogError("Failed IL hook for Terminal.TextPostProcess @ String replace");
			return;
		}

		cursor.EmitDelegate<Func<string, string>>(orig =>
		{
			if (StartOfRound.Instance.isChallengeFile)
			{
				return orig;
			}

			var dayName = Plugin.CurrentDay.Value.ToString();

			if (Plugin.CurrentEvents.Value.Length > 0)
			{
				var arbitraryFirstEvent = Plugin.CurrentEvents.Value[0];

				switch (arbitraryFirstEvent)
				{
					case EventType.DoubleMonster:
						dayName = $"Double Monster {dayName}"; break;
					case EventType.DoubleTrap:
						dayName = $"Double Trap {dayName}"; break;
					case EventType.DoubleLoot:
						dayName = $"Double Loot {dayName}"; break;
					case EventType.SmallFacility:
						dayName = $"Small Facility {dayName}"; break;
					case EventType.Black:
						dayName = $"Black {dayName}"; break;
					case EventType.Nightmare:
						dayName = $"Nightmare {dayName}"; break;
					case EventType.Easter:
						dayName = $"Easter {dayName}"; break;
				}
			}

			return dayName;
		});
	}

	private static void RoundManager_SpawnScrapInLevel(On.RoundManager.orig_SpawnScrapInLevel orig, RoundManager self)
	{
		if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.DoubleLoot))
		{
			orig(self);
			return;
		}

		Plugin.Logger.LogInfo("Applying Double Loot");

		var lastMultiplier = self.scrapAmountMultiplier;
		self.scrapAmountMultiplier *= Plugin.Config.DoubleLootMultiplier.Value;

		orig(self);

		self.scrapAmountMultiplier = lastMultiplier;
	}

	private static void RoundManager_SpawnScrapInLevel_IL(ILContext il)
	{
		var cursor = new ILCursor(il);

		var containerLoc = -1;
		var scrapToSpawnField = default(FieldReference);

		if (!cursor.TryGotoNext(
				instr => instr.MatchLdloc(out containerLoc),
				instr => instr.MatchNewobj<List<Item>>(),
				instr => instr.MatchStfld(out scrapToSpawnField)))
		{
			Plugin.Logger.LogError("Failed IL hook for RoundManager.SpawnScrapInLevel @ Init ScrapToSpawn list");
			return;
		}

		if (!cursor.TryGotoNext(MoveType.AfterLabel,
				instr => instr.MatchLdnull(),
				instr => instr.MatchStloc(out _)))
		{
			Plugin.Logger.LogError("Failed IL hook for RoundManager.SpawnScrapInLevel @ After populate ScrapToSpawn");
			return;
		}

		cursor.Emit(OpCodes.Ldarg_0);
		cursor.Emit(OpCodes.Ldloc, containerLoc);
		cursor.Emit(OpCodes.Ldfld, scrapToSpawnField);
		cursor.EmitDelegate<Action<RoundManager, List<Item>>>((self, scrapToSpawn) =>
		{
			if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.Easter))
			{
				return;
			}

			Plugin.Logger.LogInfo("Applying Easter");

			var rand = new Random(self.playersManager.randomMapSeed);

			var easterEgg = StartOfRound.Instance.allItemsList.itemsList.First(item => item.itemName == "Easter egg");
			var easterEggCount = (int)(scrapToSpawn.Count * Plugin.Config.EasterEggSpawnRate.Value);

			for (var i = 0; i < easterEggCount; i++)
			{
				scrapToSpawn.Insert(rand.Next(0, scrapToSpawn.Count), easterEgg);
			}
		});
	}

	private static void RoundManager_GenerateNewFloor(On.RoundManager.orig_GenerateNewFloor orig, RoundManager self)
	{
		if (StartOfRound.Instance.isChallengeFile || !WeekdayUtils.HasEvent(EventType.SmallFacility))
		{
			orig(self);
			return;
		}

		Plugin.Logger.LogInfo("Applying Small Facility");

		if (Plugin.Config.SmallFacilityUseFixedSize)
		{
			var lastSizeMultiplier = self.currentLevel.factorySizeMultiplier;
			self.currentLevel.factorySizeMultiplier = Plugin.Config.SmallFacilityFixedMapSize.Value;

			orig(self);

			self.currentLevel.factorySizeMultiplier = lastSizeMultiplier;
		}
		else
		{
			var lastSizeMultiplier = self.mapSizeMultiplier;
			self.mapSizeMultiplier *= Plugin.Config.SmallFacilityMapSizeMultiplier.Value;

			orig(self);

			self.mapSizeMultiplier = lastSizeMultiplier;
		}
	}
}
