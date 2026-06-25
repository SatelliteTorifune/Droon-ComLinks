using HarmonyLib;
using Assets.Scripts.Flight;
using Assets.Scripts.Flight.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Flight.MapView.Orbits.Chain.ManeuverNodes;
using Assets.Scripts.Craft.Parts;
using ModApi.Craft.Parts;
using Assets.Scripts.Craft.Parts.Modifiers;
using ModApi.Ui.Inspector;
using Assets.Scripts.Flight.MapView.UI.Inspector;
using ModApi.Craft;
using Assets.Scripts.Flight.Sim;
using UnityEngine;
using ModApi.Input;
using ModApi.Flight.UI;
using Assets.Scripts.DroonComLinks.Network;

namespace Assets.Scripts.DroonComLinks.Controls
{
    //This class uses Harmony https://github.com/pardeike/Harmony Copyright (c) 2017 Andreas Pardeike
    public class ControlsPatches
    {
        public void PatchControls()
        {
            Harmony harmony = new("com.aram.dcl");
            if (ModSettings.Instance.DebugMode) Harmony.DEBUG = true;
            harmony.PatchAll();
        }

        public static bool RegisterExternalCommand(string id, bool needsPower = true) => ComLinksManager.Instance.ManageComRequest(id, needsPower);

        public static bool ShouldBlockPlayerInput()
        {
            return ModSettings.Instance.BlockControls && !ComLinksManager.Instance.PlayerHasControl;
        }

        // 用于在 Prefix 和 Postfix 之间传递状态的静态变量
        internal static CraftControlsState? _savedControlsState = null;
    }

    [HarmonyPatch(typeof(InputSliderScript), "UpdateHandlePosition")]
    class InputSliderScriptPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("UpdateHandlePosition", needsPower: false);
    }

    [HarmonyPatch(typeof(NavSphereScript), nameof(NavSphereScript.UnlockHeading))]
    class NavSphereScriptUnlockHeadingfPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("UnlockHeading");
    }

    [HarmonyPatch(typeof(ActivationPanelController), "OnActivationButtonClicked")]
    class ActivationPanelControllerfPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("OnActivationButtonClicked");
    }

    [HarmonyPatch(typeof(StagingPanelController), "OnStagingButtonClicked")]
    class StagingPanelControllerPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("OnStagingButtonClicked");
    }

    [HarmonyPatch(typeof(ManeuverNodeScript), "OnAdjustorChangeBegin")]
    class ManeuverNodeScriptOnAdjustorChangeBeginPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("OnAdjustorChangeBegin", needsPower: false);
    }

    [HarmonyPatch(typeof(ManeuverNodeScript), "OnAdjustorChangeEnd")]
    class ManeuverNodeScriptOnAdjustorChangeEndPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("OnAdjustorChangeEnd");
    }

    [HarmonyPatch(typeof(ManeuverNodeScript), "OnAdjustorChanging")]
    class ManeuverNodeScriptOnAdjustorChangingPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("OnAdjustorChanging", needsPower: false);
    }

    [HarmonyPatch(typeof(ManeuverNodeScript), nameof(ManeuverNodeScript.OnDrag))]
    class ManeuverNodeScriptOnDragPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("ManeuverNodeDrag", needsPower: false);
    }

    [HarmonyPatch(typeof(ManeuverNodeScript), nameof(ManeuverNodeScript.OnEndDrag))]
    class ManeuverNodeScriptOnEndDragPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("ManeuverNodeEndDrag");
    }

    [HarmonyPatch(typeof(PartScript), "ToggleActivationStateFromInspector")]
    class PartScriptToggleActivationStateFromInspectorPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("ToggleActivationStateFromInspector");
    }

    [HarmonyPatch(typeof(PartScript), "OnExplodePartClicked")]
    class PartScriptOnExplodePartClickedPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("OnExplodePartClicked");
    }

    [HarmonyPatch(typeof(SelectedModel), "OnAutoBurnClicked")]
    class SelectedModelOnAutoBurnClickedPatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("OnAutoBurnClicked");
    }

    [HarmonyPatch(typeof(CraftControls), "ToggleTranslationMode")]
    class ToggleTranslationModePatch
    {
        static bool Prefix() => ControlsPatches.RegisterExternalCommand("ToggleTranslationMode");
    }

    [HarmonyPatch(typeof(FuelTankScript), "GenerateInspectorModel")]
    class FuelTankScriptInspectorModelPatch
    {
        static void Postfix(IGroupModel group, bool flightScene, FuelTankScript __instance)
        {
            ManageGroup(group, __instance);
        }

        private static void ManageGroup(IGroupModel group, FuelTankScript instance)
        {
            foreach (ItemModel item in group.Items)
            {
                if (item is IGroupModel) ManageGroup((IGroupModel)item, instance);
                if (item is IconButtonRowModel)
                {
                    IconButtonRowModel buttonRow = (IconButtonRowModel)item;

                    foreach (IconButtonModel button in buttonRow.Buttons)
                    {
                        bool flag = false;
                        FuelTransferMode fuelTransferMode = FuelTransferMode.None;
                        if (button.Sprite == "Ui/Sprites/Flight/IconFuelTransferDrain")
                        {
                            flag = true;
                            fuelTransferMode = FuelTransferMode.Drain;
                        }
                        if (button.Sprite == "Ui/Sprites/Flight/IconFuelTransferNone")
                        {
                            flag = true;
                            fuelTransferMode = FuelTransferMode.None;
                        }
                        if (button.Sprite == "Ui/Sprites/Flight/IconFuelTransferFill")
                        {
                            flag = true;
                            fuelTransferMode = FuelTransferMode.Fill;
                        }
                        if (flag)
                        {
                            Traverse.Create(button).Field("_action").SetValue((Action<IconButtonModel>)delegate
                            {
                                if (ComLinksManager.Instance.ManageComRequest("FuelTransferMode" + fuelTransferMode.ToString()))
                                    if ((bool)Traverse.Create(instance).Field("_viewTankSet").GetValue())
                                    {
                                        instance.CraftFuelSource.FuelTransferMode = fuelTransferMode;
                                    }
                                    else instance.FuelTransferMode = fuelTransferMode;
                            });
                        }
                    }
                }
            }
        }
    }

    // ============================================================================
    // 飞行控制 Patch
    // ============================================================================

    // 完全阻止控制 (原有行为) - 当 BlockPlayerInputOnly = false 时使用
    [HarmonyPatch(typeof(FlightControls), nameof(FlightControls.Update))]
    class FlightControlsPatch
    {
        static bool Prefix(float timeStep, FlightControls __instance, ref CraftNode ____craftNode, ref INavSphere ____navSphere, FlightSceneScript ____flightScene, ref float ____throttleIncrement)
        {
            // 如果启用了 "仅阻止玩家输入" 模式，则跳过此 Patch
            if (ModSettings.Instance.BlockPlayerInputOnly)
            {
                return true;
            }

            if (!ControlsPatches.RegisterExternalCommand("FlightControlsUpdate", needsPower: false)) return false;

            if (____craftNode == null || Game.Instance.UserInterface.AnyDialogsOpen || Game.Instance.UserInterface.IsTextInputFocused)
            {
                return false;
            }
            IGameInputs inputs = Game.Instance.Inputs;
            if (inputs.SwapRollYaw.GetButtonDownIfEnabled())
            {
                Traverse.Create(__instance).Property("SwapRollYaw").SetValue(!__instance.SwapRollYaw);
                if (Game.InFlightScene)
                {
                    Game.Instance.FlightScene.FlightSceneUI.ShowMessage("Roll and Yaw inputs swapped.");
                }
            }
            if (inputs.SwapEvaStrafeTurn.GetButtonDownIfEnabled())
            {
                Traverse.Create(__instance).Property("SwapEvaStrafeTurn").SetValue(!__instance.SwapEvaStrafeTurn);
                if (Game.InFlightScene)
                {
                    Game.Instance.FlightScene.FlightSceneUI.ShowMessage("EVA Strafe and EVA Turn inputs swapped.");
                }
            }
            Traverse.Create(typeof(FlightControls).Assembly.GetType("InputWrapper")).Method("UpdateLastInput", inputs.Throttle).GetValue();
            bool flag = Traverse.Create(typeof(FlightControls).Assembly.GetType("InputWrapper")).Method("LastInputWasNormalAxis", inputs.Throttle).GetValue<bool>();
            if (inputs.Throttle.Enabled && !flag)
            {
                float throttleIncrement = Mathf.Clamp(inputs.Throttle.GetAxis(), -1f, 1f);
                if (____throttleIncrement != throttleIncrement)
                {
                    ____throttleIncrement = throttleIncrement;
                    ControlsPatches.RegisterExternalCommand("Throttle Increment");
                }
            }
            float? controlInput = Traverse.Create(__instance).Method("GetControlInput", inputs.Pitch).GetValue<float?>();
            float? controlInput2 = Traverse.Create(__instance).Method("GetControlInput", __instance.SwapRollYaw ? inputs.Yaw : inputs.Roll).GetValue<float?>();
            float? controlInput3 = Traverse.Create(__instance).Method("GetControlInput", __instance.SwapRollYaw ? inputs.Roll : inputs.Yaw).GetValue<float?>();
            float? controlInput4 = Traverse.Create(__instance).Method("GetControlInput", inputs.Brake).GetValue<float?>();
            float num = Traverse.Create(__instance).Method("GetControlInput", inputs.EvaMoveUpDownNoModifier).GetValue<float?>().GetValueOrDefault();
            float num2 = Traverse.Create(__instance).Method("GetControlInput", inputs.EvaPitchNoModifier).GetValue<float?>().GetValueOrDefault();
            float num3 = Traverse.Create(__instance).Method("GetControlInput", inputs.EvaRollNoModifier).GetValue<float?>().GetValueOrDefault();
            float num4 = Traverse.Create(__instance).Method("GetControlInput", inputs.EvaMoveFwdAft).GetValue<float?>().GetValueOrDefault();
            float num5 = Traverse.Create(__instance).Method("GetControlInput", __instance.SwapEvaStrafeTurn ? inputs.EvaTurn : inputs.EvaStrafe).GetValue<float?>().GetValueOrDefault();
            float num6 = Traverse.Create(__instance).Method("GetControlInput", __instance.SwapEvaStrafeTurn ? inputs.EvaStrafe : inputs.EvaTurn).GetValue<float?>().GetValueOrDefault();
            IGameInput evaEnableJetpackMovement = inputs.EvaEnableJetpackMovement;
            if (!evaEnableJetpackMovement.IsBound || evaEnableJetpackMovement.GetButton())
            {
                if (evaEnableJetpackMovement.IsBound)
                {
                    num4 = 0f;
                    num5 = 0f;
                    num6 = 0f;
                }
                num += Traverse.Create(__instance).Method("GetControlInput", inputs.EvaMoveUpDown).GetValue<float?>().GetValueOrDefault();
                num2 += Traverse.Create(__instance).Method("GetControlInput", inputs.EvaPitch).GetValue<float?>().GetValueOrDefault();
                num3 += Traverse.Create(__instance).Method("GetControlInput", inputs.EvaRoll).GetValue<float?>().GetValueOrDefault();
            }
            float oldTranslateUp = __instance.Controls.TranslateUp;
            float oldTranslateRight = __instance.Controls.TranslateRight;
            float oldTranslateForward = __instance.Controls.TranslateForward;

            __instance.Controls.PitchInputReceived = false;
            __instance.Controls.RollInputReceived = false;
            __instance.Controls.YawInputReceived = false;
            __instance.Controls.TranslateUp = 0f;
            __instance.Controls.TranslateRight = 0f;
            __instance.Controls.TranslateForward = 0f;

            if (!____craftNode.Controls.TranslationModeEnabled)
            {
                if (controlInput2.HasValue && (!____navSphere.HeadingLocked || controlInput2.Value + __instance.AnalogRoll != 0f))
                {
                    __instance.Controls.Roll = Mathf.Clamp(controlInput2.Value + __instance.AnalogRoll + __instance.Controls.OffsetRoll, -1f, 1f);
                    __instance.Controls.RollInputReceived = controlInput2.Value + __instance.AnalogRoll != 0f;
                    if (__instance.Controls.RollInputReceived) ControlsPatches.RegisterExternalCommand("RollInputReceived");
                }
                if (controlInput.HasValue && (!____navSphere.HeadingLocked || controlInput.Value + __instance.AnalogPitch != 0f))
                {
                    __instance.Controls.Pitch = Mathf.Clamp(controlInput.Value + __instance.AnalogPitch + __instance.Controls.OffsetPitch, -1f, 1f);
                    __instance.Controls.PitchInputReceived = controlInput.Value + __instance.AnalogPitch != 0f;
                    if (__instance.Controls.PitchInputReceived) ControlsPatches.RegisterExternalCommand("PitchInputReceived");
                }
                if (controlInput3.HasValue && (!____navSphere.HeadingLocked || controlInput3.Value + __instance.AnalogYaw != 0f))
                {
                    __instance.Controls.Yaw = Mathf.Clamp(controlInput3.Value + __instance.AnalogYaw + __instance.Controls.OffsetYaw, -1f, 1f);
                    __instance.Controls.YawInputReceived = controlInput3.Value + __instance.AnalogYaw != 0f;
                    if (__instance.Controls.YawInputReceived) ControlsPatches.RegisterExternalCommand("YawInputReceived");
                }
                if (____navSphere.HeadingLocked)
                {
                    ____navSphere.LockHeading(____navSphere.Pitch, ____navSphere.Heading);
                }
                __instance.Controls.TranslateUp = Mathf.Clamp(__instance.Controls.OffsetTranslateUp, -1f, 1f);
                __instance.Controls.TranslateForward = Mathf.Clamp(__instance.Controls.OffsetTranslateForward, -1f, 1f);
                __instance.Controls.TranslateRight = Mathf.Clamp(__instance.Controls.OffsetTranslateRight, -1f, 1f);
            }
            else
            {
                if (controlInput2.HasValue)
                {
                    __instance.Controls.TranslateUp = Mathf.Clamp(controlInput.Value + __instance.AnalogPitch + __instance.Controls.OffsetTranslateUp, -1f, 1f);
                }
                if (controlInput.HasValue)
                {
                    __instance.Controls.TranslateForward = Mathf.Clamp(controlInput2.Value + __instance.AnalogThrottle + __instance.Controls.OffsetTranslateForward, -1f, 1f);
                }
                if (controlInput3.HasValue)
                {
                    __instance.Controls.TranslateRight = Mathf.Clamp(controlInput3.Value + __instance.AnalogRoll + __instance.Controls.OffsetTranslateRight, -1f, 1f);
                }
            }
            float translateUp = Mathf.Clamp((__instance.Controls.TranslateUp + Traverse.Create(__instance).Method("GetControlInput", inputs.TranslateUpDown).GetValue<float?>()).GetValueOrDefault(), -1f, 1f);
            if (translateUp != oldTranslateUp)
            {
                __instance.Controls.TranslateUp = translateUp;
                ControlsPatches.RegisterExternalCommand("TranslateUp");
            }
            float translateRight = Mathf.Clamp((__instance.Controls.TranslateRight + Traverse.Create(__instance).Method("GetControlInput", inputs.TranslateLeftRight).GetValue<float?>()).GetValueOrDefault(), -1f, 1f);
            if (translateRight != oldTranslateUp)
            {
                __instance.Controls.TranslateRight = translateRight;
                ControlsPatches.RegisterExternalCommand("TranslateRight");
            }
            float translateForward = Mathf.Clamp((__instance.Controls.TranslateForward + Traverse.Create(__instance).Method("GetControlInput", inputs.TranslateForwardBackward).GetValue<float?>()).GetValueOrDefault(), -1f, 1f);
            if (translateForward != oldTranslateForward)
            {
                __instance.Controls.TranslateForward = translateForward;
                ControlsPatches.RegisterExternalCommand("TranslateForward");
            }
            __instance.Controls.EvaAnalogJump = __instance.EvaJumpUI;
            __instance.Controls.EvaMoveFwdAft = Mathf.Clamp(num4 + __instance.AnalogEvaMoveFwdAft, -1f, 1f);
            __instance.Controls.EvaStrafe = Mathf.Clamp(num5 + __instance.AnalogEvaStrafe, -1f, 1f);
            __instance.Controls.EvaTurn = Mathf.Clamp(num6 + __instance.AnalogYaw, -1f, 1f);
            __instance.Controls.EvaMoveUpDown = Mathf.Clamp(num + __instance.AnalogEvaUpDown, -1f, 1f);
            __instance.Controls.EvaPitch = Mathf.Clamp(num2 + __instance.AnalogPitch, -1f, 1f);
            __instance.Controls.EvaRoll = Mathf.Clamp(num3 + __instance.AnalogRoll, -1f, 1f);
            __instance.Controls.EvaShootTether = inputs.EvaShootTether.GetButtonDownIfEnabled() || __instance.EvaShootTetherUI;
            __instance.Controls.EvaTetherLength = Mathf.Clamp(inputs.EvaTetherLength.GetAxis() + __instance.Controls.EvaTetherLengthOffset, -1f, 1f);
            if (controlInput4.HasValue)
            {
                float brake = Mathf.Clamp(controlInput4.Value + __instance.AnalogBrake + __instance.Controls.OffsetBrake, -1f, 1f);
                if (__instance.Controls.Brake != brake)
                {
                    __instance.Controls.Brake = brake;
                    ControlsPatches.RegisterExternalCommand("Brake");
                }
            }
            if (inputs.Throttle.Enabled)
            {
                float oldThrottle = __instance.Controls.Throttle;
                if (flag)
                {
                    __instance.Controls.Throttle = inputs.Throttle.GetAxis();
                }
                else
                {
                    __instance.Controls.Throttle += timeStep * (____throttleIncrement + (__instance.Controls.TranslationModeEnabled ? 0f : __instance.AnalogThrottle));
                }
                float throttle = Mathf.Clamp01(__instance.Controls.Throttle);
                if (throttle != oldThrottle)
                {
                    __instance.Controls.Throttle = throttle;
                    ControlsPatches.RegisterExternalCommand("Throttle");
                }
            }
            if (inputs.KillThrottle.GetButtonDownIfEnabled())
            {
                __instance.Controls.Throttle = 0f;
                ControlsPatches.RegisterExternalCommand("KillThrottle");
            }
            else if (inputs.FullThrottle.GetButtonDownIfEnabled())
            {
                __instance.Controls.Throttle = 1f;
                ControlsPatches.RegisterExternalCommand("FullThrottle");
            }
            if (inputs.Slider1.Enabled)
            {
                float slider1 = Mathf.Clamp(inputs.Slider1.GetAxis() + __instance.Controls.OffsetSlider1, -1f, 1f);
                if (slider1 != __instance.Controls.Slider1)
                {
                    __instance.Controls.Slider1 = slider1;
                    ControlsPatches.RegisterExternalCommand("Slider1");
                }
            }
            if (inputs.Slider2.Enabled)
            {
                float slider2 = Mathf.Clamp(inputs.Slider2.GetAxis() + __instance.Controls.OffsetSlider2, -1f, 1f);
                if (slider2 != __instance.Controls.Slider2)
                {
                    __instance.Controls.Slider2 = slider2;
                    ControlsPatches.RegisterExternalCommand("Slider1");
                }
            }
            if (inputs.Slider3.Enabled)
            {
                float slider3 = Mathf.Clamp(inputs.Slider3.GetAxis() + __instance.Controls.OffsetSlider3, -1f, 1f);
                if (slider3 != __instance.Controls.Slider3)
                {
                    __instance.Controls.Slider3 = slider3;
                    ControlsPatches.RegisterExternalCommand("Slider1");
                }
            }
            if (inputs.Slider4.Enabled)
            {
                float slider4 = Mathf.Clamp(inputs.Slider4.GetAxis() + __instance.Controls.OffsetSlider4, -1f, 1f);
                if (slider4 != __instance.Controls.Slider4)
                {
                    __instance.Controls.Slider4 = slider4;
                    ControlsPatches.RegisterExternalCommand("Slider1");
                }
            }
            if (!____flightScene.TimeManager.Paused)
            {
                if (inputs.ActivateStage.GetButtonDownIfEnabled())
                {
                    Traverse.Create(__instance).Method("ActivateStage").GetValue();
                    ControlsPatches.RegisterExternalCommand("ActivateStage");
                }
                if (inputs.EvaToggleWalk.GetButtonUpIfEnabled())
                {
                    __instance.Controls.EvaWalk = !__instance.Controls.EvaWalk;
                    ____flightScene.FlightSceneUI.ShowMessage((__instance.Controls.EvaWalk ? "Walking" : "Running") ?? "");
                }
            }
            if (inputs.ActivationGroup1.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(1);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup1");
            }
            if (inputs.ActivationGroup2.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(2);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup2");
            }
            if (inputs.ActivationGroup3.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(3);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup3");
            }
            if (inputs.ActivationGroup4.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(4);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup4");
            }
            if (inputs.ActivationGroup5.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(5);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup5");
            }
            if (inputs.ActivationGroup6.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(6);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup6");
            }
            if (inputs.ActivationGroup7.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(7);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup7");
            }
            if (inputs.ActivationGroup8.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(8);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup8");
            }
            if (inputs.ActivationGroup9.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(9);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup9");
            }
            if (inputs.ActivationGroup10.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleActivationGroup(10);
                ControlsPatches.RegisterExternalCommand("ToggleActivationGroup10");
            }
            if (inputs.LockHeading.GetButtonDownIfEnabled())
            {
                if (!____navSphere.HeadingLocked)
                {
                    ____navSphere.LockCurrentHeading();
                    ControlsPatches.RegisterExternalCommand("LockCurrentHeading");
                }
                else
                {
                    ____navSphere.UnlockHeading();
                    ControlsPatches.RegisterExternalCommand("UnlockHeading");
                }
            }
            if (inputs.LockPrograde.GetButtonDownIfEnabled())
            {
                ____navSphere.ToggleProgradeLock();
                ControlsPatches.RegisterExternalCommand("LockPrograde");
            }
            if (inputs.LockRetrograde.GetButtonDownIfEnabled())
            {
                ____navSphere.ToggleRetrogradeLock();
                ControlsPatches.RegisterExternalCommand("LockRetrograde");
            }
            if (inputs.LockTarget.GetButtonDownIfEnabled())
            {
                ____navSphere.ToggleTargetLock();
                ControlsPatches.RegisterExternalCommand("LockTarget");
            }
            if (inputs.ToggleTranslationMode.GetButtonDownIfEnabled())
            {
                __instance.Controls.ToggleTranslationMode();
                ControlsPatches.RegisterExternalCommand("ToggleTranslationMode");
            }
            if (inputs.ActivateCameraLook.GetButtonDown())
            {
                inputs.ActivateCameraLook.Enabled = !inputs.ActivateCameraLook.Enabled;
            }
            return false;
        }
    }

    // 仅阻止玩家输入模式 - 使用 Prefix/Postfix 保存和恢复 Vizzy 的输入
    [HarmonyPatch(typeof(FlightControls), nameof(FlightControls.Update))]
    class FlightControlsPlayerInputOnlyPatch
    {
        static void Prefix(FlightControls __instance)
        {
            // 只有在启用了 "仅阻止玩家输入" 模式且应该阻止时才执行
            if (!ModSettings.Instance.BlockPlayerInputOnly || !ControlsPatches.ShouldBlockPlayerInput())
            {
                ControlsPatches._savedControlsState = null;
                return;
            }

            // 保存当前状态（Vizzy 可能已设置）
            ControlsPatches._savedControlsState = new CraftControlsState
            {
                Throttle = __instance.Controls.Throttle,
                Pitch = __instance.Controls.Pitch,
                Roll = __instance.Controls.Roll,
                Yaw = __instance.Controls.Yaw,
                Brake = __instance.Controls.Brake,
                TranslateUp = __instance.Controls.TranslateUp,
                TranslateRight = __instance.Controls.TranslateRight,
                TranslateForward = __instance.Controls.TranslateForward,
                Slider1 = __instance.Controls.Slider1,
                Slider2 = __instance.Controls.Slider2,
                Slider3 = __instance.Controls.Slider3,
                Slider4 = __instance.Controls.Slider4
            };
        }

        static void Postfix(FlightControls __instance)
        {
            // 只有在启用了 "仅阻止玩家输入" 模式且有保存状态时才执行
            if (ControlsPatches._savedControlsState == null) return;

            var state = ControlsPatches._savedControlsState.Value;

            // 恢复 Vizzy 设置的值
            __instance.Controls.Throttle = state.Throttle;
            __instance.Controls.Pitch = state.Pitch;
            __instance.Controls.Roll = state.Roll;
            __instance.Controls.Yaw = state.Yaw;
            __instance.Controls.Brake = state.Brake;
            __instance.Controls.TranslateUp = state.TranslateUp;
            __instance.Controls.TranslateRight = state.TranslateRight;
            __instance.Controls.TranslateForward = state.TranslateForward;
            __instance.Controls.Slider1 = state.Slider1;
            __instance.Controls.Slider2 = state.Slider2;
            __instance.Controls.Slider3 = state.Slider3;
            __instance.Controls.Slider4 = state.Slider4;

            // 清除状态
            ControlsPatches._savedControlsState = null;
        }
    }

    // 用于在 Patch 之间传递状态的辅助类
    internal struct CraftControlsState
    {
        public float Throttle;
        public float Pitch;
        public float Roll;
        public float Yaw;
        public float Brake;
        public float TranslateUp;
        public float TranslateRight;
        public float TranslateForward;
        public float Slider1;
        public float Slider2;
        public float Slider3;
        public float Slider4;
    }
}
