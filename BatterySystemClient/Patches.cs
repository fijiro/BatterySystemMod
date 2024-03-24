using Aki.Reflection.Patching;
using BatterySystem.Configs;
using BSG.CameraEffects;
using EFT.Animations;
using EFT.InventoryLogic;
using EFT;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Comfort.Common;

namespace BatterySystem
{
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

	//Check weapon sight when aiming down
	public class AimSightPatch : ModulePatch
	{
		private static FieldInfo _playerInterfaceField;
		protected override MethodBase GetTargetMethod()
		{
			//TODO: Fix this, wrong interface
			_playerInterfaceField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_firearmAnimationData");
			return AccessTools.Method(typeof(ProceduralWeaponAnimation), "method_23");
		}

		[PatchPostfix]
		public static void Postfix(ref ProceduralWeaponAnimation __instance)
		{
			if (__instance != null)
			{
				GInterface127 playerField = (GInterface127)_playerInterfaceField.GetValue(__instance);
				if (BatterySystemPlugin.InGame() && playerField?.Weapon != null && Singleton<GameWorld>.Instance.GetAlivePlayerByProfileID(playerField.Weapon.Owner.ID).IsYourPlayer)
				{
					BatterySystem.CheckSightIfDraining();
				}
			}
		}
	}
	//Throws NullRefError?
	//TODO: Use reflection instead of gclass
	public class GetBoneForSlotPatch : ModulePatch
	{
		private static GClass674.GClass675 _gClass = new GClass674.GClass675();
		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(GClass674), "GetBoneForSlot");
		}

		[PatchPrefix]
		public static void Prefix(ref GClass674 __instance, IContainer container)
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
			return AccessTools.Method(nameof(Player.UpdatePhones));
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
					StaticManager.BeginCoroutine(IsThermalSwitching(__instance));
				else BatterySystem.SetHeadWearComponents();
			}
		}
		private static IEnumerator IsThermalSwitching(ThermalVision tv)
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
