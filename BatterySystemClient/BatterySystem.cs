using System.Linq;
using System.Reflection;
using Aki.Reflection.Patching;
using HarmonyLib;
using Comfort.Common;
using UnityEngine;
using EFT;
using EFT.InventoryLogic;
using BSG.CameraEffects;
using BatterySystem.Configs;
using System.Threading.Tasks;
using BepInEx.Logging;
using System.Collections.Generic;
using EFT.CameraControl;
using EFT.Animations;
using System.Collections;
using EFT.Visual;

namespace BatterySystem
{
	public class BatterySystem
	{
		public static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("BatterySystem");
		public static Item headWearItem = null;
		private static NightVisionComponent _headWearNvg = null;
		private static ThermalVisionComponent _headWearThermal = null;
		private static bool _drainingHeadWearBattery = false;
		public static ResourceComponent headWearBattery = null;

		private static Item _earPieceItem = null;
		private static ResourceComponent _earPieceBattery = null;
		private static bool _drainingEarPieceBattery = false;
		public static float compressorMakeup;
		//compressor is used because the default 
		public static float compressor;

		public static Dictionary<SightModVisualControllers, ResourceComponent> sightMods = new Dictionary<SightModVisualControllers, ResourceComponent>();
		public static Dictionary<TacticalComboVisualController, ResourceComponent> lightMods = new Dictionary<TacticalComboVisualController, ResourceComponent>();
		private static bool _drainingSightBattery;

		public static Item GetheadWearSight() // returns the special device goggles that are equipped
		{
			if (_headWearNvg != null)
				return _headWearNvg.Item;
			else if (_headWearThermal != null)
				return _headWearThermal.Item;
			else
				return null;
		}

		public static bool IsInSlot(Item item, Slot slot)
		{
			if (item != null && slot?.ContainedItem != null && item.IsChildOf(slot.ContainedItem)) return true;
			else return false;
		}

		public static void UpdateBatteryDictionary()
		{
			// Remove unequipped items
			for (int i = BatterySystemPlugin.batteryDictionary.Count - 1; i >= 0; i--)
			{
				Item key = BatterySystemPlugin.batteryDictionary.Keys.ElementAt(i);
				if (!(IsInSlot(key, PlayerInitPatch.GetEquipmentSlot(EquipmentSlot.Earpiece))
					|| IsInSlot(key, PlayerInitPatch.GetEquipmentSlot(EquipmentSlot.Headwear))
					|| IsInSlot(key, Singleton<GameWorld>.Instance.MainPlayer.ActiveSlot)))
					BatterySystemPlugin.batteryDictionary.Remove(key);
			}

			if (BatterySystemConfig.EnableHeadsets.Value && _earPieceItem != null
				&& !BatterySystemPlugin.batteryDictionary.ContainsKey(_earPieceItem)) // earpiece
				BatterySystemPlugin.batteryDictionary.Add(_earPieceItem, _drainingEarPieceBattery);

			if (GetheadWearSight() != null && !BatterySystemPlugin.batteryDictionary.ContainsKey(GetheadWearSight())) // headwear
				BatterySystemPlugin.batteryDictionary.Add(GetheadWearSight(), _drainingHeadWearBattery);

			foreach (SightModVisualControllers sightController in sightMods.Keys) // sights on active weapon
				if (IsInSlot(sightController.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)
					&& !BatterySystemPlugin.batteryDictionary.ContainsKey(sightController.SightMod.Item))
					BatterySystemPlugin.batteryDictionary.Add(sightController.SightMod.Item, sightMods[sightController]?.Value > 0);

			foreach (TacticalComboVisualController deviceController in lightMods.Keys) // tactical devices on active weapon
				if (IsInSlot(deviceController.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)
					&& !BatterySystemPlugin.batteryDictionary.ContainsKey(deviceController.LightMod.Item))
					BatterySystemPlugin.batteryDictionary.Add(deviceController.LightMod.Item, lightMods[deviceController]?.Value > 0);


			if (BatterySystemConfig.EnableLogs.Value)
			{
				Logger.LogInfo("--- BATTERYSYSTEM: Updated battery dictionary: ---");
				foreach (Item i in BatterySystemPlugin.batteryDictionary.Keys)
					Logger.LogInfo(i);
				Logger.LogInfo("---------------------------------------------");
			}
		}

		public static void SetEarPieceComponents()
		{
			if (BatterySystemConfig.EnableHeadsets.Value)
			{
				_earPieceItem = PlayerInitPatch.GetEquipmentSlot(EquipmentSlot.Earpiece).Items?.FirstOrDefault();
				_earPieceBattery = _earPieceItem?.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault();
				_drainingEarPieceBattery = false;
				if (BatterySystemConfig.EnableLogs.Value)
				{
					Logger.LogInfo("--- BATTERYSYSTEM: Setting EarPiece components at: " + Time.time + " ---");
					Logger.LogInfo("headWearItem: " + _earPieceItem);
					Logger.LogInfo("Battery in Earpiece: " + _earPieceBattery?.Item);
					Logger.LogInfo("Battery Resource: " + _earPieceBattery);
				}
				CheckEarPieceIfDraining();
				UpdateBatteryDictionary();
			}
		}

		public static void CheckEarPieceIfDraining()
		{
			if (BatterySystemConfig.EnableHeadsets.Value)
			{
				//headset has charged battery installed
				if (_earPieceBattery != null && _earPieceBattery.Value > 0)
				{
					MethodInvoker.GetHandler(AccessTools.Method(typeof(Player), "UpdatePhonesReally"));
					_drainingEarPieceBattery = true;
				}
				//headset has no battery
				else if (_earPieceItem != null)
				{
					Singleton<BetterAudio>.Instance.Master.SetFloat("CompressorMakeup", 0f);
					Singleton<BetterAudio>.Instance.Master.SetFloat("Compressor", compressor - 15f);
					Singleton<BetterAudio>.Instance.Master.SetFloat("MainVolume", -10f);
					_drainingEarPieceBattery = false;
				}
				//no headset equipped
				else
				{
					MethodInvoker.GetHandler(AccessTools.Method(typeof(Player), "UpdatePhonesReally"));
					_drainingEarPieceBattery = false;
				}

				if (_earPieceItem != null && BatterySystemPlugin.batteryDictionary.ContainsKey(_earPieceItem))
					BatterySystemPlugin.batteryDictionary[_earPieceItem] = _drainingEarPieceBattery;

				if (BatterySystemConfig.EnableLogs.Value)
				{
					Logger.LogInfo("--- BATTERYSYSTEM: Checking EarPiece battery: " + Time.time + " ---");
					Logger.LogInfo("EarPiece: " + _earPieceItem);
					Logger.LogInfo("Battery level " + _earPieceBattery?.Value + ", draining " + _drainingEarPieceBattery);
					Logger.LogInfo("---------------------------------------------");
				}
			}
		}

		public static void SetHeadWearComponents()
		{
			headWearItem = PlayerInitPatch.GetEquipmentSlot(EquipmentSlot.Headwear).Items?.FirstOrDefault(); // default null else headwear
			_headWearNvg = headWearItem?.GetItemComponentsInChildren<NightVisionComponent>().FirstOrDefault(); //default null else nvg item
			_headWearThermal = headWearItem?.GetItemComponentsInChildren<ThermalVisionComponent>().FirstOrDefault(); //default null else thermal item
			headWearBattery = GetheadWearSight()?.Parent.Item.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault(); //default null else resource

			if (BatterySystemConfig.EnableLogs.Value)
			{
				Logger.LogInfo("--- BATTERYSYSTEM: Setting HeadWear components at: " + Time.time + " ---");
				Logger.LogInfo("headWearItem: " + headWearItem);
				Logger.LogInfo("headWearNVG: " + _headWearNvg);
				Logger.LogInfo("headWearParent: " + GetheadWearSight()?.Parent.Item);
				Logger.LogInfo("headWearThermal: " + _headWearThermal);
				Logger.LogInfo("Battery in HeadWear: " + headWearBattery?.Item);
				Logger.LogInfo("Battery Resource: " + headWearBattery);
			}
			CheckHeadWearIfDraining();
			UpdateBatteryDictionary();
		}

		public static void CheckHeadWearIfDraining()
		{
			_drainingHeadWearBattery = headWearBattery != null && headWearBattery.Value > 0
				&& (_headWearNvg == null && _headWearThermal != null
				? (_headWearThermal.Togglable.On && !CameraClass.Instance.ThermalVision.InProcessSwitching)
				: (_headWearNvg != null && _headWearThermal == null && _headWearNvg.Togglable.On && !CameraClass.Instance.NightVision.InProcessSwitching));
			// headWear has battery with resource installed and headwear (nvg/thermal) isn't switching and is on

			if (BatterySystemConfig.EnableLogs.Value)
			{
				Logger.LogInfo("--- BATTERYSYSTEM: Checking HeadWear battery: " + Time.time + " ---");
				Logger.LogInfo("hwItem: " + GetheadWearSight());
				Logger.LogInfo("Battery level " + headWearBattery?.Value + ", HeadWear_on: " + _drainingHeadWearBattery);
				Logger.LogInfo("---------------------------------------------");
			}
			if (headWearBattery != null && BatterySystemPlugin.batteryDictionary.ContainsKey(GetheadWearSight()))
				BatterySystemPlugin.batteryDictionary[GetheadWearSight()] = _drainingHeadWearBattery;

			if (_headWearNvg != null)
				PlayerInitPatch.nvgOnField.SetValue(CameraClass.Instance.NightVision, _drainingHeadWearBattery);

			else if (_headWearThermal != null)
				PlayerInitPatch.thermalOnField.SetValue(CameraClass.Instance.ThermalVision, _drainingHeadWearBattery);
		}

		public static void SetSightComponents(SightModVisualControllers sightInstance)
		{
			LootItemClass lootItem = sightInstance.SightMod.Item as LootItemClass;

			bool _hasBatterySlot(LootItemClass loot, string[] filters = null)
			{
				//use default parameter if nothing specified (any drainable battery)
				filters = filters ?? new string[] { "5672cb124bdc2d1a0f8b4568", "5672cb304bdc2dc2088b456a", "590a358486f77429692b2790" };
				foreach (Slot slot in loot.Slots)
				{
					if (slot.Filters.FirstOrDefault()?.Filter.Any(sfilter => filters.Contains(sfilter)) == true)
						return true;
				}
				return false;
			}

			if (BatterySystemConfig.EnableLogs.Value)
			{
				Logger.LogInfo("--- BATTERYSYSTEM: Setting sight components at " + Time.time + " ---");
				Logger.LogInfo("For: " + sightInstance.SightMod.Item);
			}
			//before applying new sights, remove sights that are not on equipped weapon
			for (int i = sightMods.Keys.Count - 1; i >= 0; i--)
			{
				SightModVisualControllers key = sightMods.Keys.ElementAt(i);
				if (!IsInSlot(key.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
				{
					sightMods.Remove(key);
				}
			}

			if (IsInSlot(sightInstance.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot) && _hasBatterySlot(lootItem))
			{
				if (BatterySystemConfig.EnableLogs.Value)
					Logger.LogInfo("Sight Found: " + sightInstance.SightMod.Item);
				// if sight is already in dictionary, dont add it
				if (!sightMods.Keys.Any(key => key.SightMod.Item == sightInstance.SightMod.Item)
					&& (sightInstance.SightMod.Item.Template.Parent._id == "55818acf4bdc2dde698b456b" //compact collimator
					|| sightInstance.SightMod.Item.Template.Parent._id == "55818ad54bdc2ddc698b4569" //collimator
					|| sightInstance.SightMod.Item.Template.Parent._id == "55818aeb4bdc2ddc698b456a")) //Special Scope
				{
					sightMods.Add(sightInstance, sightInstance.SightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault());
				}
			}
			CheckSightIfDraining();
			UpdateBatteryDictionary();
		}
		public static void CheckSightIfDraining()
		{
			//ERROR:  If reap-ir is on and using canted collimator, enabled optic sight removes collimator effect. find a way to only drain active sight! /////////////////////////////////////////////////////////
			if (BatterySystemConfig.EnableLogs.Value)
				Logger.LogInfo("--- BATTERYSYSTEM: Checking Sight battery at " + Time.time + " ---");

			//for because modifying sightMods[key]
			for (int i = 0; i < sightMods.Keys.Count; i++)
			{
				SightModVisualControllers key = sightMods.Keys.ElementAt(i);
				if (key?.SightMod?.Item != null)
				{
					sightMods[key] = key.SightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault();
					_drainingSightBattery = (sightMods[key] != null && sightMods[key].Value > 0
						&& IsInSlot(key.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot));

					if (BatterySystemPlugin.batteryDictionary.ContainsKey(key.SightMod.Item))
						BatterySystemPlugin.batteryDictionary[key.SightMod.Item] = _drainingSightBattery;

					if (BatterySystemConfig.EnableLogs.Value)
						Logger.LogInfo("Sight on: " + _drainingSightBattery + " for " + key.name);

					// true for finding inactive gameobject reticles
					foreach (CollimatorSight col in key.gameObject.GetComponentsInChildren<CollimatorSight>(true))
					{
						col.gameObject.SetActive(_drainingSightBattery);
					}
					foreach (OpticSight optic in key.gameObject.GetComponentsInChildren<OpticSight>(true))
					{
						/*
						//for nv sights
						if (optic.NightVision != null)
						{
							Logger.LogWarning("OPTIC ENABLED: " + optic.NightVision?.enabled);
							//PlayerInitPatch.nvgOnField.SetValue(optic.NightVision, _drainingSightBattery);
							optic.NightVision.enabled = _drainingSightBattery;
							Logger.LogWarning("OPTIC ON: " + optic.NightVision.On);
							continue;
						}*/

						if (key.SightMod.Item.Template.Parent._id != "55818ad54bdc2ddc698b4569" && 
							key.SightMod.Item.Template.Parent._id != "5c0a2cec0db834001b7ce47d") //Exceptions for hhs-1 (tan)
							optic.enabled = _drainingSightBattery;
					}
				}
			}
			if (BatterySystemConfig.EnableLogs.Value)
				Logger.LogInfo("---------------------------------------------");
		}

		public static void SetDeviceComponents(TacticalComboVisualController deviceInstance)
		{
			if (BatterySystemConfig.EnableLogs.Value)
			{
				Logger.LogInfo("--- BATTERYSYSTEM: Setting Tactical Device Components at " + Time.time + " ---");
				Logger.LogInfo("For: " + deviceInstance.LightMod.Item);
			}
			//before applying new sights, remove sights that are not on equipped weapon
			for (int i = lightMods.Keys.Count - 1; i >= 0; i--)
			{
				TacticalComboVisualController key = lightMods.Keys.ElementAt(i);
				if (!IsInSlot(key.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
				{
					lightMods.Remove(key);
				}
			}

			if (IsInSlot(deviceInstance.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
			{
				if (BatterySystemConfig.EnableLogs.Value)
					Logger.LogInfo("Device Found: " + deviceInstance.LightMod.Item);
				// if sight is already in dictionary, dont add it
				if (!lightMods.Keys.Any(key => key.LightMod.Item == deviceInstance.LightMod.Item)
					&& (deviceInstance.LightMod.Item.Template.Parent._id == "55818b084bdc2d5b648b4571" //flashlight
					|| deviceInstance.LightMod.Item.Template.Parent._id == "55818b0e4bdc2dde698b456e" //laser
					|| deviceInstance.LightMod.Item.Template.Parent._id == "55818b164bdc2ddc698b456c")) //combo
				{
					lightMods.Add(deviceInstance, deviceInstance.LightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault());
				}
			}
			CheckDeviceIfDraining();
			UpdateBatteryDictionary();
		}
		public static void CheckDeviceIfDraining()
		{
			if (BatterySystemConfig.EnableLogs.Value)
				Logger.LogInfo("--- BATTERYSYSTEM: Checking Tactical Device battery at " + Time.time + " ---");

			for (int i = 0; i < lightMods.Keys.Count; i++)
			{
				TacticalComboVisualController key = lightMods.Keys.ElementAt(i);
				if (key?.LightMod?.Item != null)
				{
					lightMods[key] = key.LightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault();
					_drainingSightBattery = (lightMods[key] != null && key.LightMod.IsActive && lightMods[key].Value > 0
						&& IsInSlot(key.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot));

					if (BatterySystemPlugin.batteryDictionary.ContainsKey(key.LightMod.Item))
						BatterySystemPlugin.batteryDictionary[key.LightMod.Item] = _drainingSightBattery;

					if (BatterySystemConfig.EnableLogs.Value)
						Logger.LogInfo("Light on: " + _drainingSightBattery + " for " + key.name);

					// true for finding inactive gameobject reticles
					foreach (LaserBeam laser in key.gameObject.GetComponentsInChildren<LaserBeam>(true))
					{
						laser.gameObject.gameObject.SetActive(_drainingSightBattery);
					}
					foreach (Light light in key.gameObject.GetComponentsInChildren<Light>(true))
					{
						light.gameObject.gameObject.SetActive(_drainingSightBattery);
					}
				}
			}

			if (BatterySystemConfig.EnableLogs.Value)
				Logger.LogInfo("---------------------------------------------");
		}
	}

	public class PlayerInitPatch : ModulePatch
	{
		private static FieldInfo _inventoryField = null;
		private static InventoryControllerClass _inventoryController = null;
		private static InventoryControllerClass _botInventory = null;
		public static FieldInfo nvgOnField = null;
		public static FieldInfo thermalOnField = null;
		private static readonly System.Random _random = new System.Random();

		protected override MethodBase GetTargetMethod()
		{
			_inventoryField = AccessTools.Field(typeof(Player), "_inventoryController");
			nvgOnField = AccessTools.Field(typeof(NightVision), "_on");
			thermalOnField = AccessTools.Field(typeof(ThermalVision), "On");

			return AccessTools.Method(typeof(Player), "Init");
		}

		[PatchPostfix]
		public static async void Postfix(Player __instance, Task __result)
		{
			await __result;
			if (BatterySystemConfig.EnableLogs.Value)
				Logger.LogInfo("PlayerInitPatch AT " + Time.time + ", IsYourPlayer: " + __instance.IsYourPlayer + ", ID: " + __instance.FullIdInfo);

			if (__instance.IsYourPlayer)
			{
				_inventoryController = (InventoryControllerClass)_inventoryField.GetValue(__instance); //Player Inventory
				BatterySystem.sightMods.Clear(); // remove old sight entries that were saved from previous raid
				BatterySystem.lightMods.Clear(); // same for tactical devices
				BatterySystem.SetEarPieceComponents();
				//__instance.OnSightChangedEvent -= sight => BatterySystem.CheckSightIfDraining();
			}
			else //Spawned bots have their batteries drained
			{
				DrainSpawnedBattery(__instance);
			}
		}

		private static void DrainSpawnedBattery(Player botPlayer)
		{
			_botInventory = (InventoryControllerClass)_inventoryField.GetValue(botPlayer);
			foreach (Item item in _botInventory.EquipmentItems)
			{
				//batteries charge depends on their max charge and bot level
				for (int i = 0; i < item.GetItemComponentsInChildren<ResourceComponent>().Count(); i++)
				{
					int resourceAvg = _random.Next(0, 5);
					ResourceComponent resource = item.GetItemComponentsInChildren<ResourceComponent>().ElementAt(i);
					if (resource.MaxResource > 0)
					{
						if (botPlayer.Side != EPlayerSide.Savage)
						{
							resourceAvg = (int)(botPlayer.Profile.Info.Level / 150f * resource.MaxResource);
						}
						resource.Value = _random.Next(Mathf.Max(resourceAvg - 10, 0), (int)Mathf.Min(resourceAvg + 5, resource.MaxResource));
					}
					if (BatterySystemConfig.EnableLogs.Value)
					{
						Logger.LogInfo("DrainSpawnedBattery on Bot at " + Time.time);
						Logger.LogInfo("LVL: " + botPlayer.Profile.Info.Level + " AVG: " + resourceAvg);
						Logger.LogInfo("Checking item from slot: " + item);
						Logger.LogInfo("Res value: " + resource.Value);
					}
				}
			}
		}
		public static Slot GetEquipmentSlot(EquipmentSlot slot)

		{
			return _inventoryController.Inventory.Equipment.GetSlot(slot);
		}
	}

	//When Aiming down, check sight
	public class AimSightPatch : ModulePatch
	{
		private static FieldInfo playerInterfaceField;
		protected override MethodBase GetTargetMethod()
		{
			playerInterfaceField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "ginterface114_0");
			return AccessTools.Method(typeof(ProceduralWeaponAnimation), "method_21");
		}

		[PatchPostfix]
		public static void Postfix(ref ProceduralWeaponAnimation __instance)
		{
			if (__instance != null)
			{
				GInterface114 playerField = (GInterface114)playerInterfaceField.GetValue(__instance);
				if (BatterySystemPlugin.InGame() && playerField?.Weapon != null && Singleton<GameWorld>.Instance.GetAlivePlayerByProfileID(playerField.Weapon.Owner.ID).IsYourPlayer)
				{
					BatterySystem.CheckSightIfDraining();
				}
			}
		}
	}
	//Throws NullRefError.
	public class GetBoneForSlotPatch : ModulePatch
	{
		private static GClass707.GClass708 _gClass = new GClass707.GClass708();
		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(GClass707), "GetBoneForSlot");
		}

		[PatchPrefix]
		public static void Prefix(ref GClass707 __instance, IContainer container)
		{
			if (!__instance.ContainerBones.ContainsKey(container) && container.ID == "mod_equipment")
			{
				_gClass.Bone = null;
				_gClass.Item = null;
				_gClass.ItemView = null;
				if (BatterySystemConfig.EnableLogs.Value)
				{
					Logger.LogInfo("--- BatterySystem: GetBoneForSlot at " + Time.time + " ---");
					Logger.LogInfo("GameObject: " + __instance.GameObject);
					Logger.LogInfo(" Container: " + container);
					Logger.LogInfo("Items: " + container.Items.FirstOrDefault());
					Logger.LogWarning("Trying to get bone for battery slot!");
					Logger.LogInfo("---------------------------------------------");
				}
				__instance.ContainerBones.Add(container, _gClass);
			}
		}
	}

	public class UpdatePhonesPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(Player), "UpdatePhones");
		}
		[PatchPostfix]
		public static void PatchPostfix(ref Player __instance) //BetterAudio __instance
		{
			if (BatterySystemPlugin.InGame() && __instance.IsYourPlayer)
			{
				if (BatterySystemConfig.EnableLogs.Value)
					Logger.LogInfo("UpdatePhonesPatch at " + Time.time);
				Singleton<BetterAudio>.Instance.Master.GetFloat("Compressor", out BatterySystem.compressor);
				Singleton<BetterAudio>.Instance.Master.GetFloat("CompressorMakeup", out BatterySystem.compressorMakeup);
				BatterySystem.SetEarPieceComponents();
			}
		}
	}

	public class ApplyItemPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(Slot).GetMethod(nameof(Slot.ApplyContainedItem));
		}

		[PatchPostfix]
		static void Postfix(ref Slot __instance) // limit to only player asap
		{
			if (BatterySystemPlugin.InGame() && __instance.ContainedItem.ParentRecursiveCheck(PlayerInitPatch.GetEquipmentSlot(EquipmentSlot.Headwear).ParentItem))
			{
				if (BatterySystemConfig.EnableLogs.Value)
				{
					Logger.LogInfo("BATTERYSYSTEM: ApplyItemPatch at: " + Time.time);
					Logger.LogInfo("Slot parent: " + __instance.ParentItem);
				}
				if (BatterySystem.IsInSlot(__instance.ContainedItem, PlayerInitPatch.GetEquipmentSlot(EquipmentSlot.Earpiece)))
				{
					if (BatterySystemConfig.EnableLogs.Value)
						Logger.LogInfo("Slot is child of EarPiece!");

					BatterySystem.SetEarPieceComponents();
					return;
				}
				else if (BatterySystem.IsInSlot(__instance.ParentItem, PlayerInitPatch.GetEquipmentSlot(EquipmentSlot.Headwear)))
				{ //if item in headwear slot applied
					if (BatterySystemConfig.EnableLogs.Value)
						Logger.LogInfo("Slot is child of HeadWear!");
					BatterySystem.SetHeadWearComponents();
					return;
				}
				else if (BatterySystem.IsInSlot(__instance.ContainedItem, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
				{ // if sight is removed and empty slot is applied, then remove the sight from sightdb
					if (BatterySystemConfig.EnableLogs.Value)
						Logger.LogInfo("Slot is child of ActiveSlot!");
					BatterySystem.CheckDeviceIfDraining();
					BatterySystem.CheckSightIfDraining();
					return;
				}
				BatterySystem.SetEarPieceComponents();
				BatterySystem.SetHeadWearComponents();
			}
		}
	}

	public class SightDevicePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(SightModVisualControllers).GetMethod(nameof(SightModVisualControllers.UpdateSightMode));
		}

		[PatchPostfix]
		static void Postfix(ref SightModVisualControllers __instance)
		{
			//only sights on equipped weapon are added
			if (BatterySystemPlugin.InGame() && BatterySystem.IsInSlot(__instance.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
			{
				BatterySystem.SetSightComponents(__instance);
			}
		}
	}
	
	/*
	public class FoldableSightPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(ProceduralWeaponAnimation).GetMethod("FindAimTransforms");
		}
		[PatchPostfix]
		static void Postfix(ref ProceduralWeaponAnimation __instance)
		{
			if (BatterySystemConfig.AutoUnfold.Value)
				if (BatterySystemConfig.EnableLogs.Value)
					Logger.LogInfo("FindAimTransforms at " + Time.time);
			//AutofoldableSight.On == On when folds, unfold false
		}
	}*/

	public class TacticalDevicePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(TacticalComboVisualController).GetMethod(nameof(TacticalComboVisualController.UpdateBeams));
		}

		[PatchPostfix]
		static void Postfix(ref TacticalComboVisualController __instance)
		{
			//only sights on equipped weapon are added
			if (BatterySystemPlugin.InGame()
				&& BatterySystem.IsInSlot(__instance?.LightMod?.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
			{
				BatterySystem.SetDeviceComponents(__instance);
			}
		}
	}

	public class NvgHeadWearPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(NightVision).GetMethod(nameof(NightVision.StartSwitch));
		}

		[PatchPostfix]
		static void Postfix(ref NightVision __instance)
		{
			if (__instance.name == "FPS Camera" && BatterySystemPlugin.InGame())
			{
				if (__instance.InProcessSwitching)
					StaticManager.BeginCoroutine(IsNVSwitching(__instance));
				else BatterySystem.SetHeadWearComponents();
			}
		}
		//waits until InProcessSwitching is false and then 
		private static IEnumerator IsNVSwitching(NightVision nv)
		{
			while (nv.InProcessSwitching)
			{
				yield return new WaitForSeconds(1f / 100f);
			}
			BatterySystem.SetHeadWearComponents();
			yield break;
		}
	}

	public class ThermalHeadWearPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(ThermalVision).GetMethod(nameof(ThermalVision.StartSwitch));
		}

		[PatchPostfix]
		static void Postfix(ref ThermalVision __instance)
		{
			if (__instance.name == "FPS Camera" && BatterySystemPlugin.InGame())
			{
				if (__instance.InProcessSwitching)
					StaticManager.BeginCoroutine(IsSwitching(__instance));
				else BatterySystem.SetHeadWearComponents();
			}
		}
		private static IEnumerator IsSwitching(ThermalVision tv)
		{
			while (tv.InProcessSwitching)
			{
				yield return new WaitForSeconds(1f / 100f);
			}
			BatterySystem.SetHeadWearComponents();
			yield break;
		}
	}

}