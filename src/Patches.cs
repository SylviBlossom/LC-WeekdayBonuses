using DunGen;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WeekdayBonuses;

public static class Patches
{
	public static void Initialize()
	{
		On.StartOfRound.Start += StartOfRound_Start;
		On.StartOfRound.Update += StartOfRound_Update;
		IL.Terminal.TextPostProcess += Terminal_TextPostProcess_IL;

		On.RoundManager.SpawnScrapInLevel += RoundManager_SpawnScrapInLevel;
		IL.RoundManager.SpawnScrapInLevel += RoundManager_SpawnScrapInLevel_IL;
		On.RoundManager.GenerateNewFloor += RoundManager_GenerateNewFloor;
	}

	private static void StartOfRound_Start(On.StartOfRound.orig_Start orig, StartOfRound self)
	{
		if (self.IsServer)
		{
			var dayOfWeek = Plugin.Config.AllowDayChange.Value ? DateTime.Now.DayOfWeek : Plugin.DayAtStartup;

			Plugin.CurrentDay.Value = dayOfWeek;
			Plugin.CurrentEvent.Value = Plugin.Config.EventForDay[dayOfWeek].Value;
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
				Plugin.CurrentEvent.Value = Plugin.Config.EventForDay[dayOfWeek].Value;
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

			switch (Plugin.CurrentEvent.Value)
			{
				case EventType.DoubleLoot:
					dayName = $"Double Loot {dayName}"; break;
				case EventType.SmallFacility:
					dayName = $"Small Facility {dayName}"; break;
				case EventType.Easter:
					dayName = $"Easter {dayName}"; break;
			}

			return dayName;
		});
	}

	private static void RoundManager_SpawnScrapInLevel(On.RoundManager.orig_SpawnScrapInLevel orig, RoundManager self)
	{
		if (StartOfRound.Instance.isChallengeFile || Plugin.CurrentEvent.Value != EventType.DoubleLoot)
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
			if (StartOfRound.Instance.isChallengeFile || Plugin.CurrentEvent.Value != EventType.Easter)
			{
				return;
			}

			Plugin.Logger.LogInfo("Applying Easter");

			var rand = new Random(self.playersManager.randomMapSeed);

			var easterEgg = StartOfRound.Instance.allItemsList.itemsList.First(item => item.itemName == "Easter egg");
			var easterEggCount = (int)(scrapToSpawn.Count * Plugin.Config.EasterEggRate.Value);

			for (var i = 0; i < easterEggCount; i++)
			{
				scrapToSpawn.Insert(rand.Next(0, scrapToSpawn.Count), easterEgg);
			}
		});
	}

	private static void RoundManager_GenerateNewFloor(On.RoundManager.orig_GenerateNewFloor orig, RoundManager self)
	{
		if (StartOfRound.Instance.isChallengeFile || Plugin.CurrentEvent.Value != EventType.SmallFacility)
		{
			orig(self);
			return;
		}

		Plugin.Logger.LogInfo("Applying Small Facility");

		var lastSizeMultiplier = self.currentLevel.factorySizeMultiplier;
		self.currentLevel.factorySizeMultiplier = Plugin.Config.SmallFacilitySize.Value;

		orig(self);

		self.currentLevel.factorySizeMultiplier = lastSizeMultiplier;
	}
}
