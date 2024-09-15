using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using TooManyEmotes.UI;
using TooManyEmotesAlternateControls.Compatibility;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine;

namespace TooManyEmotesAlternateControls.Patchers
{
    [HarmonyPatch]
    internal class EmoteMenuPatcher
    {
        private static int lastEmoteIndex = -1;
        private static bool hasMovedCursorInMenu = false;

        [HarmonyPatch(typeof(EmoteMenu), "GetInput")]
        [HarmonyPrefix]
        private static bool SetLastEmoteUsed()
        {
            if (hasMovedCursorInMenu == true)
                return true;

            if (!EmoteMenu.isMenuOpen || ConfigSettings.disableEmotesForSelf.Value || LCVRCompat.LoadedAndEnabled)
                return true;

            if (AdditionalPanelUI.hovered || EmoteMenu.hoveredLoadoutUIIndex != -1)
                return true;
            else
            {
                // Allows the player to quickly use their last emote until they hover over an item
                // A lot of this is repeated logic, but it will leave it to the original once it detects the cursor over an emote
                Vector2 direction;
                RectTransform referenceElement = EmoteMenu.emoteUIElementsList[0].uiRectTransform;
                float distanceThreshold = Vector2.Distance(EmoteMenu.menuGameObject.transform.position, referenceElement.position) * 0.815f;
                if (!EmoteMenu.usingController)
                {
                    Vector3 rawMousePosition = Mouse.current.position.ReadValue();
                    Camera uiCamera = HUDManager.Instance.HUDContainer.GetComponentInParent<Canvas>().worldCamera;
                    rawMousePosition.z = Mathf.Abs(uiCamera.transform.position.z - EmoteMenu.menuTransform.position.z);
                    direction = uiCamera.ScreenToWorldPoint(rawMousePosition) - EmoteMenu.menuTransform.position;
                }
                else
                    direction = EmoteMenu.currentThumbstickPosition;

                int emoteIndex = -1;
                //Allow player to quickly use their last emote
                if (!hasMovedCursorInMenu) { emoteIndex = lastEmoteIndex; }
                if ((!EmoteMenu.usingController && direction.magnitude >= distanceThreshold/*0.425f*/) || (EmoteMenu.usingController && EmoteMenu.currentThumbstickPosition != Vector2.zero))
                {
                    hasMovedCursorInMenu = true;
                    float angle = Mathf.Atan2(direction.y, -direction.x) * Mathf.Rad2Deg - 67.5f;
                    if (angle < 0) angle += 360;
                    emoteIndex = Mathf.FloorToInt(angle / 45);
                }
                if (emoteIndex != EmoteMenu.hoveredEmoteUIIndex)
                    EmoteMenu.OnHoveredNewElement(emoteIndex);
                return false;



                //// Applies the last emote selection until they manually hover over an emote
                //int emoteIndex = EmoteMenu.hoveredEmoteUIIndex;
                //if (!hasMovedCursorInMenu)
                //{
                //    // Set up value to be changed to the last used emote
                //    if (EmoteMenu.hoveredEmoteUIIndex == -1 || hover)
                //    {
                //        emoteIndex = lastEmoteIndex;
                //    }
                //    // Detect if the player has hovered over an emote manually
                //    if (lastEmoteIndex != EmoteMenu.hoveredEmoteUIIndex)
                //    {
                //        hasMovedCursorInMenu = true;
                //    }
                //}
                //// If we caused a change to the hovered emote, apply it to the UI
                //if (EmoteMenu.hoveredEmoteUIIndex != emoteIndex)
                //{
                //    EmoteMenu.OnHoveredNewElement(emoteIndex);
                //}
            }
        }

        [HarmonyPatch(typeof(EmoteMenu), "CloseEmoteMenu")]
        [HarmonyPrefix]
        private static void OnEmoteMenuClose()
        {
            // Saves last emote so it can be quickly used again
            if (EmoteMenu.hoveredEmoteUIIndex != -1)
            {
                lastEmoteIndex = EmoteMenu.hoveredEmoteUIIndex;
            }
        }

        [HarmonyPatch(typeof(EmoteMenu), "OpenEmoteMenu")]
        [HarmonyPostfix]
        private static void ResetHasMovedCursorInMenu()
        {
            hasMovedCursorInMenu = false;
        }

    }
}
