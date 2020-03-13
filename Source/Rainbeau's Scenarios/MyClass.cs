using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace RFScenarios_Code {

	[StaticConstructorOnStartup]
	internal static class RFScenarios_Initializer {
		public static List<int> skyFalls = new List<int>(new int[]{ 
		  5, 8, 10, 13, 15, 23, 30, 38, 45, 53, 65, 78, 90, 103, 115,
		  133, 150, 168, 185, 203, 225, 248, 270, 293, 315, 343, 370, 398, 425, 453, 
		  485, 518, 550, 583, 615, 653, 690, 728, 765, 803, 845, 888, 930, 973, 1015,
		  1063, 1110, 1158, 1205, 1253, 1305, 1358, 1410, 1463, 1515, 1573, 1630, 1688, 1745, 1803, 
		  1865, 1928, 1990, 2053, 2115, 2183, 2250, 2318, 2385, 2453, 2525, 2598, 2670, 2743, 2815
		  });
		public static bool usingSkyFall = false;
		public static bool usingMoonFall = false;
		static RFScenarios_Initializer() {
			LongEventHandler.QueueLongEvent(new Action(RFScenarios_Initializer.Setup), "LibraryStartup", false, null);
		}
		public static void Setup() {
			SetIncidents.SetIncidentClasses();
		}
	}

	public static class SetIncidents {
		public static void SetIncidentClasses() {
			foreach (IncidentDef def in DefDatabase<IncidentDef>.AllDefsListForReading) {
				if (def.defName == "MeteoriteImpact") {
					if (Controller.Settings.crashBurn.Equals(true)) {
						def.workerClass = typeof(IncidentWorker_MeteoriteImpactFlame);
					}
					else {
						def.workerClass = typeof(IncidentWorker_MeteoriteImpact);
					}
				}
			}
		}
	}

	public static class ShipChunkOptions {
		public static ThingDef SelectChunkFromAvailableOptions() {
			return GenCollection.RandomElement<ThingDef>(DefDatabase<ThingDef>.AllDefs.Where<ThingDef>((ThingDef defs) => {
				if (!defs.defName.StartsWith("ShipChunk")) {
					return false;
				}
				return !defs.defName.Contains("Incoming");
			}));
		}
	}

	public class Controller : Mod {
		public static Settings Settings;
		public override string SettingsCategory() { return "RFScenarios".Translate(); }
		public override void DoSettingsWindowContents(Rect canvas) { Settings.DoWindowContents(canvas); }
		public Controller(ModContentPack content) : base(content) {
			Harmony harmony = new Harmony("net.rainbeau.rimworld.mod.scenarios");
			harmony.PatchAll( Assembly.GetExecutingAssembly() );
			Settings = GetSettings<Settings>();
		}
	}

	public class Settings : ModSettings {
		public bool crashBurn = true;
		public bool podTrees = true;
		public void DoWindowContents(Rect canvas) {
			Listing_Standard list = new Listing_Standard();
			list.ColumnWidth = canvas.width;
			list.Begin(canvas);
			list.Gap();
			list.CheckboxLabeled( "RFScenarios.CrashBurn".Translate(), ref crashBurn, "RFScenarios.CrashBurnTip".Translate() );
			list.Gap(24);
			list.CheckboxLabeled( "RFScenarios.PodTrees".Translate(), ref podTrees, "RFScenarios.PodTreesTip".Translate() );
			list.Gap(24);
			list.Label("RFScenarios.MustRestartGame".Translate());
			list.End();
		}
		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref crashBurn, "crashBurn", true);
			Scribe_Values.Look(ref podTrees, "podTrees", true);
		}
	}

	public class IncidentWorker_CCC_SkyFall : IncidentWorker {
		private readonly static Pair<int, float>[] CountChance;
		static IncidentWorker_CCC_SkyFall() {
			IncidentWorker_CCC_SkyFall.CountChance = new Pair<int, float>[] { new Pair<int, float>(1, 1f), new Pair<int, float>(2, 0.95f), new Pair<int, float>(3, 0.7f), new Pair<int, float>(4, 0.4f) };
		}
		public IncidentWorker_CCC_SkyFall() { }
		protected override bool TryExecuteWorker(IncidentParms parms) {
			RFScenarios_Initializer.usingSkyFall = true;
			float SkyFallType = Rand.Value;
			if (SkyFallType < 0.1f) {
				// Meteor Swarm
				Messages.Message("RFScenarios.MessageMeteorSwarm".Translate(), MessageTypeDefOf.NeutralEvent);
				return true;
			}
			else if (SkyFallType < 0.5f) {
				// Ship Chunk
				IntVec3 intVec3;
				Map map = (Map)parms.target;
				if (!this.TryFindShipChunkDropCell(map.Center, map, 999999, out intVec3)) {
					return false;
				}
				this.SpawnShipChunks(intVec3, map, this.RandomCountToDrop);
				Messages.Message("MessageShipChunkDrop".Translate(), new TargetInfo(intVec3, map, false), MessageTypeDefOf.NeutralEvent);
				return true;
			}
			else if (SkyFallType < 0.6f) {
				// Cargo Pod
				Map map = (Map)parms.target;
				List<Thing> things = ThingSetMakerDefOf.ResourcePod.root.Generate();
				IntVec3 intVec3 = DropCellFinder.RandomDropSpot(map);
				DropPodUtility.DropThingsNear(intVec3, map, things, 110, false, true, true);
				Find.LetterStack.ReceiveLetter("LetterLabelCargoPodCrash".Translate(), "CargoPodCrash".Translate(), LetterDefOf.PositiveEvent, new TargetInfo(intVec3, map, false), null);
				return true;
			}
			else if (SkyFallType < 0.65f) {
				// Flashstorm
				Map map = (Map)parms.target;
				if ((map).gameConditionManager.ConditionIsActive(GameConditionDefOf.Flashstorm)) {
					return false;
				}
				FloatRange durationDays = new FloatRange(0.075f, 0.1f);
				int num = Mathf.RoundToInt(durationDays.RandomInRange * 60000f);
				GameCondition_Flashstorm gameConditionFlashstorm = (GameCondition_Flashstorm)GameConditionMaker.MakeCondition(GameConditionDefOf.Flashstorm, num);
				map.gameConditionManager.RegisterCondition(gameConditionFlashstorm);
				Find.LetterStack.ReceiveLetter("RFScenarios.LetterLabelFlashstorm".Translate(), "RFScenarios.Flashstorm".Translate(), LetterDefOf.ThreatSmall, new TargetInfo(gameConditionFlashstorm.centerLocation.ToIntVec3, map, false), null);
				if (map.weatherManager.curWeather.rainRate > 0.1f) {
					map.weatherDecider.StartNextWeather();
				}
				return true;
			}
			else if (SkyFallType < 0.95f) {
				// Meteorite
				IntVec3 intVec3;
				Map map = (Map)parms.target;
				if (!this.TryFindCell(out intVec3, map)) {
					return false;
				}
				List<Thing> things = new List<Thing>();
				Thing test;
				IntRange count;
				int num;
				float type = Rand.Value;
				if (type < 0.75f) {
					test = ThingMaker.MakeThing(ThingDefOf.MineableSteel, null);
					count = new IntRange(10, 20);
					num = Mathf.RoundToInt(count.RandomInRange);
				}
				else if (type < 0.875f) {
					test = ThingMaker.MakeThing(ThingDefOf.MineableComponentsIndustrial, null);
					count = new IntRange(9, 18);
					num = Mathf.RoundToInt(count.RandomInRange);
				}
				else if (type < 0.9375f) {
					test = ThingMaker.MakeThing(ThingDefOf.MineablePlasteel, null);
					count = new IntRange(3, 6);
					num = Mathf.RoundToInt(count.RandomInRange);
				}
				else {
					test = ThingMaker.MakeThing(ThingDefOf.MineableUranium, null);
					count = new IntRange(1, 3);
					num = Mathf.RoundToInt(count.RandomInRange);
				}
				test.stackCount = num;
				things.Add(test);
				if (Controller.Settings.crashBurn.Equals(true) && Rand.Value < 0.67f) {
					SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncomingFlame, things, intVec3, map);
				}
				else {
					SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncoming, things, intVec3, map);
				}
				LetterDef letterDef = (!things[0].def.building.isResourceRock ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent);
                String text = TranslatorFormattedStringExtensions.Translate("RFScenarios.Meteorite", things[0].def.label);
				Find.LetterStack.ReceiveLetter("RFScenarios.LetterLabelMeteorite".Translate(), text, letterDef, new TargetInfo(intVec3, map, false), null);
				return true;
			}
			else {
				// Escape Pod
				Map map = (Map)parms.target;
				List<Thing> things = ThingSetMakerDefOf.RefugeePod.root.Generate();
				IntVec3 intVec3 = DropCellFinder.RandomDropSpot(map);
				Pawn pawn = this.FindPawn(things);
				TaggedString str = new TaggedString("LetterLabelRefugeePodCrash".Translate());
                TaggedString str1 = new TaggedString("RefugeePodCrash".Translate());
				if (pawn.ageTracker.AgeBiologicalYears > 19 && pawn.ageTracker.AgeBiologicalYears < 36) {
					if (Rand.Value < 0.33) {
						pawn.SetFaction(Faction.OfPlayer);
						str1 = "RFScenarios.RefugeePodCrash".Translate();
					}
				}
				PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref str1, ref str, pawn);
				Find.LetterStack.ReceiveLetter(str, str1, LetterDefOf.NeutralEvent, new TargetInfo(intVec3, map, false), null);
				ActiveDropPodInfo activeDropPodInfo = new ActiveDropPodInfo();
				activeDropPodInfo.innerContainer.TryAddRangeOrTransfer(things, true, false);
				activeDropPodInfo.openDelay = 180;
				activeDropPodInfo.leaveSlag = true;
				DropPodUtility.MakeDropPodAt(intVec3, map, activeDropPodInfo);
				return true;
			}
		}
		private Pawn FindPawn(List<Thing> things) {
			for (int i = 0; i < things.Count; i++) {
				Pawn item = things[i] as Pawn;
				if (item != null) {
					return item;
				}
				Corpse corpse = things[i] as Corpse;
				if (corpse != null) {
					return corpse.InnerPawn;
				}
			}
			return null;
		}
		private int RandomCountToDrop {
			get {
				float ticksGame = (float)Find.TickManager.TicksGame / 3600000f;
				float single = Mathf.Clamp(GenMath.LerpDouble(0f, 1.2f, 1f, 0.1f, ticksGame), 0.1f, 1f);
				Pair<int, float> pair = IncidentWorker_CCC_SkyFall.CountChance.RandomElementByWeight<Pair<int, float>>((Pair<int, float> x) => {
					if (x.First == 1) {
						return x.Second;
					}
					return x.Second * single;
				});
				return pair.First;
			}
		}
		private void SpawnChunk(IntVec3 pos, Map map) {
			ThingDef chunkType = ShipChunkOptions.SelectChunkFromAvailableOptions();
			if (Controller.Settings.crashBurn.Equals(true) && Rand.Value < 0.67f) {
				SkyfallerMaker.SpawnSkyfaller(ThingDefOf.ShipChunkIncomingFlame, chunkType, pos, map);
			}
			else {
				SkyfallerMaker.SpawnSkyfaller(ThingDefOf.ShipChunkIncoming, chunkType, pos, map);
			}
		}
		private void SpawnShipChunks(IntVec3 firstChunkPos, Map map, int count) {
			IntVec3 intVec3;
			this.SpawnChunk(firstChunkPos, map);
			for (int i = 0; i < count - 1; i++) {
				if (this.TryFindShipChunkDropCell(firstChunkPos, map, 5, out intVec3)) {
					this.SpawnChunk(intVec3, map);
				}
			}
		}
		private bool TryFindCell(out IntVec3 cell, Map map) {
			int mineablesCountRange = ThingSetMaker_Meteorite.MineablesCountRange.max;
			ThingDef meteoriteIncoming = ThingDefOf.MeteoriteIncoming;
			Map map1 = map;
			IntVec3 intVec3 = new IntVec3();
			return CellFinderLoose.TryFindSkyfallerCell(meteoriteIncoming, map1, out cell, 10, intVec3, -1, true, false, false, false, true, true, (IntVec3 x) => {
				int num = Mathf.CeilToInt(Mathf.Sqrt((float)mineablesCountRange)) + 2;
				CellRect cellRect = CellRect.CenteredOn(x, num, num);
				int num1 = 0;

                foreach (IntVec3 current in cellRect)
                {
                    if (current.InBounds(map) && current.Standable(map))
                    {
                        num1++;
                    }
                }

				return num1 >= mineablesCountRange;
			});
		}
		private bool TryFindShipChunkDropCell(IntVec3 nearLoc, Map map, int maxDist, out IntVec3 pos) {
			ThingDef shipChunkIncoming = ThingDefOf.ShipChunkIncoming;
			Map map1 = map;
			IntVec3 intVec3 = nearLoc;
			return CellFinderLoose.TryFindSkyfallerCell(shipChunkIncoming, map1, out pos, 10, intVec3, maxDist, true, false, false, false, true, false, null);
		}
	}

	public class IncidentWorker_CCC_MoonFall : IncidentWorker {
		static IncidentWorker_CCC_MoonFall() { }
		public IncidentWorker_CCC_MoonFall() { }
		protected override bool TryExecuteWorker(IncidentParms parms) {
			RFScenarios_Initializer.usingMoonFall = true;
			float SkyFallType = Rand.Value;
			if (SkyFallType < 0.2f) {
				// Meteor Swarm
				Messages.Message("RFScenarios.MessageMeteorSwarmTribal".Translate(), MessageTypeDefOf.NeutralEvent);
				return true;
			}
			else if (SkyFallType < 0.25f) {
				// Flashstorm
				Map map = (Map)parms.target;
				if ((map).gameConditionManager.ConditionIsActive(GameConditionDefOf.Flashstorm)) {
					return false;
				}
				FloatRange durationDays = new FloatRange(0.075f, 0.1f);
				int num = Mathf.RoundToInt(durationDays.RandomInRange * 60000f);
				GameCondition_Flashstorm gameConditionFlashstorm = (GameCondition_Flashstorm)GameConditionMaker.MakeCondition(GameConditionDefOf.Flashstorm, num);
				map.gameConditionManager.RegisterCondition(gameConditionFlashstorm);
				Find.LetterStack.ReceiveLetter("RFScenarios.LetterLabelFlashstorm".Translate(), "RFScenarios.FlashstormTribal".Translate(), LetterDefOf.ThreatSmall, new TargetInfo(gameConditionFlashstorm.centerLocation.ToIntVec3, map, false), null);
				if (map.weatherManager.curWeather.rainRate > 0.1f) {
					map.weatherDecider.StartNextWeather();
				}
				return true;
			}
			else {
				// Meteorite
				IntVec3 intVec3;
				Map map = (Map)parms.target;
				if (!this.TryFindCell(out intVec3, map)) {
					return false;
				}
				List<Thing> list = ThingSetMakerDefOf.Meteorite.root.Generate();
				if (Controller.Settings.crashBurn.Equals(true) && Rand.Value < 0.67f) {
					SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncomingFlame, list, intVec3, map);
				}
				else {
					SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncoming, list, intVec3, map);
				}
				LetterDef letterDef = (!list[0].def.building.isResourceRock ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent);
                String text = TranslatorFormattedStringExtensions.Translate("RFScenarios.MeteoriteTribal", list[0].def.label);
                Find.LetterStack.ReceiveLetter("RFScenarios.LetterLabelMeteoriteTribal".Translate(), text, letterDef, new TargetInfo(intVec3, map, false), null);
				return true;
			}
		}
		private bool TryFindCell(out IntVec3 cell, Map map) {
			int mineablesCountRange = ThingSetMaker_Meteorite.MineablesCountRange.max;
			ThingDef meteoriteIncoming = ThingDefOf.MeteoriteIncoming;
			Map map1 = map;
			IntVec3 intVec3 = new IntVec3();
			return CellFinderLoose.TryFindSkyfallerCell(meteoriteIncoming, map1, out cell, 10, intVec3, -1, true, false, false, false, true, true, (IntVec3 x) => {
				int num = Mathf.CeilToInt(Mathf.Sqrt((float)mineablesCountRange)) + 2;
				CellRect cellRect = CellRect.CenteredOn(x, num, num);
				int num1 = 0;

                foreach (IntVec3 current in cellRect)
                {
                    if (current.InBounds(map) && current.Standable(map))
                    {
                        num1++;
                    }
                }
				return num1 >= mineablesCountRange;
			});
		}
	}
	
	[DefOf]
	public static class IncidentDefOf {
		public static IncidentDef CCC_SkyFall;
		public static IncidentDef CCC_MoonFall;
	}
	
	[DefOf]
	public static class ThingDefOf {
		public static ThingDef MeteoriteIncoming;
		public static ThingDef MeteoriteIncomingFlame;
		public static ThingDef MineableComponentsIndustrial;
		public static ThingDef MineablePlasteel;
		public static ThingDef MineableSteel;
		public static ThingDef MineableUranium;
		public static ThingDef ShipChunkIncoming;
		public static ThingDef ShipChunkIncomingFlame;
		public static ThingDef WoodLog;
	}
	        
	[HarmonyPatch(typeof(IncidentWorker_ShipChunkDrop), "SpawnChunk")]
	public static class IncidentWorker_ShipChunkDrop_SpawnChunk {
		static bool Prefix(IntVec3 pos, Map map) {
			ThingDef chunkType = ShipChunkOptions.SelectChunkFromAvailableOptions();
			if (Controller.Settings.crashBurn.Equals(true) && Rand.Value < 0.67f) {
				SkyfallerMaker.SpawnSkyfaller(ThingDefOf.ShipChunkIncomingFlame, chunkType, pos, map);
			}
			else {
				SkyfallerMaker.SpawnSkyfaller(ThingDefOf.ShipChunkIncoming, chunkType, pos, map);
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(DropPodUtility), "DropThingGroupsNear")]
	public static class ScenPart_PlayerPawnsArriveMethod_DropThingGroupsNear {
		static bool Prefix(IntVec3 dropCenter, Map map, List<List<Thing>> thingsGroups, int openDelay = 110, bool instaDrop = false, bool leaveSlag = false, bool canRoofPunch = true) {
			IntVec3 intVec3;
			foreach (List<Thing> thingsGroup in thingsGroups) {
				if (!DropCellFinder.TryFindDropSpotNear(dropCenter, map, out intVec3, true, canRoofPunch)) {
					Log.Warning(string.Concat(new object[] { "DropThingsNear failed to find a place to drop ", thingsGroup.FirstOrDefault<Thing>(), " near ", dropCenter, ". Dropping on random square instead." }));
					intVec3 = CellFinderLoose.RandomCellWith((IntVec3 c) => c.Walkable(map), map, 1000);
				}
				for (int i = 0; i < thingsGroup.Count; i++) {
					thingsGroup[i].SetForbidden(true, false);
				}
				if (!instaDrop) {
					ActiveDropPodInfo activeDropPodInfo = new ActiveDropPodInfo();
					foreach (Thing thing in thingsGroup) {
						activeDropPodInfo.innerContainer.TryAdd(thing, true);
					}
					if (Controller.Settings.podTrees.Equals(true)) {
						float woodCheck = map.Biome.plantDensity;
						if (woodCheck > 1) { woodCheck = 1; }
						if (Rand.Value < (woodCheck * 4)) {
							Thing wood = ThingMaker.MakeThing(ThingDefOf.WoodLog, null);
							float count = Rand.Gaussian(80,10);
							count = count * woodCheck;
							wood.stackCount = (int)count;
							activeDropPodInfo.innerContainer.TryAdd(wood, true);
						}
					}
					activeDropPodInfo.openDelay = openDelay;
					activeDropPodInfo.leaveSlag = leaveSlag;
					DropPodUtility.MakeDropPodAt(intVec3, map, activeDropPodInfo);
				}
				else {
					foreach (Thing thing1 in thingsGroup) {
						GenPlace.TryPlaceThing(thing1, intVec3, map, ThingPlaceMode.Near, null);
					}
				}
			}
			return false;
		}
	}

	public class IncidentWorker_MeteoriteImpactFlame : IncidentWorker {
		public IncidentWorker_MeteoriteImpactFlame() { }
		protected override bool CanFireNowSub(IncidentParms parms) {
			IntVec3 intVec3;
			return this.TryFindCell(out intVec3, (Map)parms.target);
		}
		protected override bool TryExecuteWorker(IncidentParms parms) {
			IntVec3 intVec3;
			Map map = (Map)parms.target;
			if (!this.TryFindCell(out intVec3, map)) {
				return false;
			}
			List<Thing> things = ThingSetMakerDefOf.Meteorite.root.Generate();
			if (Controller.Settings.crashBurn.Equals(true) && Rand.Value < 0.67f) {
				SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncomingFlame, things, intVec3, map);
			}
			else {
				SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncoming, things, intVec3, map);
			}
			LetterDef letterDef = (!things[0].def.building.isResourceRock ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent);
			string str = string.Format(this.def.letterText, things[0].def.label).CapitalizeFirst();
			Find.LetterStack.ReceiveLetter(this.def.letterLabel, str, letterDef, new TargetInfo(intVec3, map, false), null);
			return true;
		}
		private bool TryFindCell(out IntVec3 cell, Map map) {
			int mineablesCountRange = ThingSetMaker_Meteorite.MineablesCountRange.max;
			ThingDef meteoriteIncoming = ThingDefOf.MeteoriteIncoming;
			Map map1 = map;
			IntVec3 intVec3 = new IntVec3();
			return CellFinderLoose.TryFindSkyfallerCell(meteoriteIncoming, map1, out cell, 10, intVec3, -1, true, false, false, false, true, true, (IntVec3 x) => {
				int num = Mathf.CeilToInt(Mathf.Sqrt((float)mineablesCountRange)) + 2;
				CellRect cellRect = CellRect.CenteredOn(x, num, num);
				int num1 = 0;

                foreach (IntVec3 current in cellRect)
                {
                    if (current.InBounds(map) && current.Standable(map))
                    {
                        num1++;
                    }
                }
				return num1 >= mineablesCountRange;
			});
		}
	}

	public class StorytellerCompProperties_SkyFall : StorytellerCompProperties {
		public StorytellerCompProperties_SkyFall() {
			this.compClass = typeof(StorytellerComp_SkyFall);
		}
	}
	
	public class StorytellerComp_SkyFall : StorytellerComp {
		protected int IntervalsPassed {
			get {
				return Find.TickManager.TicksGame / 1000;
			}
		}
		[DebuggerHidden]
		public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target) {
			if (target == Find.Maps.Find((Map x) => x.IsPlayerHome)) {
				if (RFScenarios_Initializer.usingSkyFall.Equals(true)) {
					bool skyFall = false;
					for (int i=0; i<RFScenarios_Initializer.skyFalls.Count; i++) {
						if (this.IntervalsPassed == RFScenarios_Initializer.skyFalls[i]) {
							skyFall = true;
							break;
						}
					}
					if (skyFall.Equals(true)) {
						if (Rand.Value < 0.33f) {
							IncidentDef inc = IncidentDefOf.CCC_SkyFall;
							yield return new FiringIncident(inc, this, null) {
								parms = { target = target }
							};
						}
					}
				}
			}
		}
	}

	public class StorytellerCompProperties_MoonFall : StorytellerCompProperties {
		public StorytellerCompProperties_MoonFall() {
			this.compClass = typeof(StorytellerComp_MoonFall);
		}
	}
	
	public class StorytellerComp_MoonFall : StorytellerComp {
		protected int IntervalsPassed {
			get {
				return Find.TickManager.TicksGame / 1000;
			}
		}
		[DebuggerHidden]
		public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target) {
			if (target == Find.Maps.Find((Map x) => x.IsPlayerHome)) {
				if (RFScenarios_Initializer.usingMoonFall.Equals(true)) {
					bool moonFall = false;
					for (int i=0; i<RFScenarios_Initializer.skyFalls.Count; i++) {
						if (this.IntervalsPassed == RFScenarios_Initializer.skyFalls[i]) {
							moonFall = true;
							break;
						}
					}
					if (moonFall.Equals(true)) {
						if (Rand.Value < 0.5f) {
							IncidentDef inc = IncidentDefOf.CCC_MoonFall;
							yield return new FiringIncident(inc, this, null) {
								parms = { target = target }
							};
						}
					}
				}
			}
		}
	}

}
