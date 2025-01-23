using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameNetcodeStuff;
using HarmonyLib;
using TooManyEmotes;
using TooManyEmotes.Config;
using TooManyEmotes.Patches;
using TooManyEmotes.UI;
using TooManyEmotesAlternateControls.Compatibility;
using UnityEngine;

namespace TooManyEmotesAlternateControls.Patchers
{
    [HarmonyPatch]
    class ThirdPersonEmoteControllerPatcher
    {

        private static EmoteController emoteControllerLocal;
        private static PlayerControllerB localPlayerController;
        private static Camera emoteCamera;
        private static Camera gameplayCamera;
        private static Transform localPlayerCameraContainer;
        private static Transform emoteCameraPivot;
        private static int cameraCollideLayerMask;
        private static float targetCameraDistance;
        private static Vector2 clampCameraDistance;

        private static bool _isMovingWhileEmoting;
        private static bool isMovingWhileEmoting { get { return IsMovingWhileEmoting(); } }


        private static float _timeOfLastMovingCheck = 0f;

        private static Vector3 prevCameraDirection = new Vector3(0, 0, 0);

        // Initilizes the variables after the original mod
        // TODO: I may need to initialize variables in here. I believe some references get lost after the first initialization
        [HarmonyPatch(typeof(ThirdPersonEmoteController), "InitLocalPlayerController")]
        [HarmonyPostfix]
        private static void InitValues()
        {
            emoteControllerLocal = EmoteControllerPlayer.emoteControllerLocal;
            localPlayerController = StartOfRound.Instance?.localPlayerController;
            emoteCamera = (Camera)Traverse.Create(typeof(ThirdPersonEmoteController)).Field("emoteCamera").GetValue();
            gameplayCamera = (Camera)Traverse.Create(typeof(ThirdPersonEmoteController)).Field("gameplayCamera").GetValue();
            localPlayerCameraContainer = (Transform)Traverse.Create(typeof(ThirdPersonEmoteController)).Property("localPlayerCameraContainer").GetValue();
            emoteCameraPivot = (Transform)Traverse.Create(typeof(ThirdPersonEmoteController)).Field("emoteCameraPivot").GetValue();
            cameraCollideLayerMask = (int)Traverse.Create(typeof(ThirdPersonEmoteController)).Field("cameraCollideLayerMask").GetValue();
            targetCameraDistance = (float)Traverse.Create(typeof(ThirdPersonEmoteController)).Field("targetCameraDistance").GetValue();
            clampCameraDistance = (Vector2)Traverse.Create(typeof(ThirdPersonEmoteController)).Field("clampCameraDistance").GetValue();

            _isMovingWhileEmoting = (bool)Traverse.Create(typeof(ThirdPersonEmoteController)).Property("isMovingWhileEmoting").GetValue();
        }

        // Stops the original mod's camera code from running
        [HarmonyPatch(typeof(ThirdPersonEmoteController), "UseFreeCamWhileEmoting")]
        [HarmonyPrefix]
        private static bool IgnoreOriginalFreeCam(ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "PlayerLookInput")]
        [HarmonyPrefix]
        private static bool UseFreeCam(PlayerControllerB __instance)
        {
            //Initialize value in camera direction if needed
            if (prevCameraDirection.magnitude < 0.1f)
            {
                prevCameraDirection = emoteCameraPivot.forward;
            }

            if (__instance != localPlayerController || emoteControllerLocal == null)
                return true;

            if (ConfigSettings.disableEmotesForSelf.Value || LCVRCompat.LoadedAndEnabled)
                return true;

            if (emoteControllerLocal.IsPerformingCustomEmote())
            {
                if (ThirdPersonEmoteController.firstPersonEmotesEnabled)
                {
                    Plugin.Logger.LogMessage($"gamplayCamera: {gameplayCamera}, localPlayerCameraContainer: {localPlayerCameraContainer}");
                    if (StartOfRound.Instance.activeCamera != gameplayCamera)
                    {
                        StartOfRound.Instance.SwitchCamera(gameplayCamera);
                        ThirdPersonEmoteController.CallChangeAudioListenerToObject(gameplayCamera.gameObject);
                        emoteCamera.enabled = false;
                        if (localPlayerController.currentlyHeldObjectServer != null)
                            localPlayerController.currentlyHeldObjectServer.parentObject = localPlayerController.localItemHolder;
                    }
                    localPlayerCameraContainer.SetPositionAndRotation(localPlayerController.playerGlobalHead.position, localPlayerController.transform.rotation);
                    return isMovingWhileEmoting;
                }

                if (StartOfRound.Instance.activeCamera != emoteCamera)
                {
                    emoteCamera.enabled = true;
                    StartOfRound.Instance.SwitchCamera(emoteCamera);
                    ThirdPersonEmoteController.CallChangeAudioListenerToObject(emoteCamera.gameObject);
                    if (localPlayerController.currentlyHeldObjectServer != null)
                        localPlayerController.currentlyHeldObjectServer.parentObject = localPlayerController.serverItemHolder;
                }

                // Moves camera to create third person view
                Vector3 targetPosition = Vector3.back * Mathf.Clamp(targetCameraDistance, clampCameraDistance.x, clampCameraDistance.y);
                emoteCamera.transform.localPosition = Vector3.Lerp(emoteCamera.transform.localPosition, targetPosition, 10 * Time.deltaTime);

                if (!localPlayerController.quickMenuManager.isMenuOpen && !EmoteMenu.isMenuOpen)
                {
                    bool isPlayerMoving = localPlayerController.moveInputVector.magnitude > 0.2f;

                    //emoteCameraPivot.transform.localEulerAngles = gameplayCamera.transform.localEulerAngles;

                    // Moves emote camera rather than the player camera
                    Vector2 vector = localPlayerController.playerActions.Movement.Look.ReadValue<Vector2>() * 0.008f * IngamePlayerSettings.Instance.settings.lookSensitivity;
                    // If player is moving, avoid doubling up rotation caused by player control
                    if (!isPlayerMoving) { emoteCameraPivot.Rotate(new Vector3(0f, vector.x, 0f)); }
                    float cameraPitch = emoteCameraPivot.localEulerAngles.x - vector.y;
                    cameraPitch = cameraPitch > 180 ? cameraPitch - 360 : cameraPitch;
                    cameraPitch = Mathf.Clamp(cameraPitch, -45, 45);
                    emoteCameraPivot.transform.localEulerAngles = new Vector3(cameraPitch, emoteCameraPivot.localEulerAngles.y, 0f);

                    // Move player towards camera is moving
                    if (isPlayerMoving)
                    {
                        Vector3 cameraDirection = new Vector3(emoteCameraPivot.transform.forward.x, 0, emoteCameraPivot.transform.forward.z);
                        Vector3 oldCameraDirection = emoteCamera.transform.forward;
                        localPlayerController.transform.rotation = Quaternion.RotateTowards(localPlayerController.transform.rotation, Quaternion.LookRotation(cameraDirection), Time.deltaTime * 720f);
                        Vector3 localCameraDirection = emoteCameraPivot.transform.parent.InverseTransformDirection(oldCameraDirection);
                        emoteCameraPivot.transform.localRotation = Quaternion.LookRotation(localCameraDirection);
                    }

                    // Stops camera from clipping
                    if (Physics.Raycast(emoteCameraPivot.position, -emoteCameraPivot.forward * targetCameraDistance, out var hit, targetCameraDistance, cameraCollideLayerMask))
                        emoteCamera.transform.localPosition = Vector3.back * Mathf.Clamp(hit.distance - 0.2f, 0, targetCameraDistance);

                    // Syncs player rotation
                    //localPlayerController.NetworkObject.SynchronizeTransform = true;

                    prevCameraDirection = emoteCameraPivot.forward;

                    // Allow player to control normally while moving
                    return isPlayerMoving;
                }
            }
            return true;
        }

        // Disallows isMovingWhileEmoting from being retrieved every frame
        private static bool IsMovingWhileEmoting()
        {
            if (Time.time - _timeOfLastMovingCheck > 0.1f)
            {
                _isMovingWhileEmoting = (bool)Traverse.Create(typeof(ThirdPersonEmoteController)).Property("isMovingWhileEmoting").GetValue();
            }
            return _isMovingWhileEmoting;
        }
    }
}
