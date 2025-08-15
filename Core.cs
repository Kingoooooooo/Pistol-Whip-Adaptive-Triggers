using MelonLoader;
using Il2Cpp;
using System.Collections;
using UnityEngine;
using System.Net.Sockets;
using Il2CppOculus.Platform;
using PSVR2Toolkit.CAPI;

[assembly: MelonInfo(typeof(PSVRTriggers.Core), "PSVRTriggers", "1.0.0", "kingo", null)]
[assembly: MelonGame("Cloudhead Games, Ltd.", "Pistol Whip")]

namespace PSVRTriggers
{
    public class Core : MelonMod
    {
        private IpcClient ipcClient;
        private int lastGunType = -1;
        private bool lastDualWieldState = false;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Initializing PSVR2 Triggers...");

            // Get IPC client instance and start connection
            ipcClient = IpcClient.Instance();
            if (ipcClient.Start())
            {
                MelonLogger.Msg("Successfully connected to PSVR2Toolkit!");
            }
            else
            {
                MelonLogger.Error("Failed to connect to PSVR2Toolkit. Make sure PSVR2Toolkit is running.");
            }
        }

        public override void OnUpdate()
        {
            if (ipcClient == null) return;

            PlayerGunManager manager = PlayerGunManager.instance;
            if (manager == null) return;

            Gun currentGun = manager.currentGunLeft ?? manager.currentGunRight;
            bool dualWieldActive = manager.DualWieldActive();

            if (currentGun == null) return;

            // Only update triggers if gun type or dual wield state changed
            int currentGunType = (int)manager.currentGunDominantHand.gunType;
            if (currentGunType == lastGunType && dualWieldActive == lastDualWieldState) return;

            lastGunType = currentGunType;
            lastDualWieldState = dualWieldActive;

            if (dualWieldActive)
            {
                ApplyDualWieldTriggerEffects(currentGunType);
            }
            else
            {
                ApplySingleHandTriggerEffects(currentGunType, manager.currentDominantHand);
            }
        }

        private void ApplyDualWieldTriggerEffects(int gunType)
        {
            switch (gunType)
            {
                case 0: // Gun type 0 - Light weapon effect
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Left, 2, 5, 4);
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Right, 2, 5, 4);
                    break;

                case 1: // Gun type 1 - Medium weapon effect
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Left, 2, 8, 8);
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Right, 2, 8, 8);
                    break;

                case 2: // Gun type 2 - Heavy weapon effect
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Left, 0, 4, 8);
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Right, 0, 4, 8);
                    break;

                case 3: // Gun type 3 - Multiple position feedback (burst fire?)
                    byte[] strengthPattern = new byte[10] { 5, 0, 0, 6, 0, 0, 7, 0, 0, 8 };
                    ipcClient.TriggerEffectMultiplePositionFeedback(EVRControllerType.Left, strengthPattern);
                    ipcClient.TriggerEffectMultiplePositionFeedback(EVRControllerType.Right, strengthPattern);
                    break;

                case 4: // Gun type 4 - No effect (silenced weapon?)
                    ipcClient.TriggerEffectDisable(EVRControllerType.Both);
                    break;

                case 5: // Gun type 5 - Slope feedback (charging weapon?)
                    ipcClient.TriggerEffectSlopeFeedback(EVRControllerType.Left, 1, 9, 8, 1);
                    ipcClient.TriggerEffectSlopeFeedback(EVRControllerType.Right, 1, 9, 8, 1);
                    break;

                default:
                    // Default to basic feedback for unknown gun types
                    ipcClient.TriggerEffectFeedback(EVRControllerType.Both, 128, 100);
                    break;
            }
        }

        private void ApplySingleHandTriggerEffects(int gunType, PlayerGunManager.EGunHandType dominantHand)
        {
            // Determine which controller based on dominant hand
            // Check what enum values are available - common ones are LeftHand/RightHand or Left_Hand/Right_Hand
            EVRControllerType controllerType;

            // Try different possible enum value names
            if (dominantHand.ToString().Contains("Left"))
            {
                controllerType = EVRControllerType.Left;
            }
            else if (dominantHand.ToString().Contains("Right"))
            {
                controllerType = EVRControllerType.Right;
            }
            else
            {
                // Default to right hand if we can't determine
                controllerType = EVRControllerType.Right;
                MelonLogger.Warning($"Unknown hand type: {dominantHand}, defaulting to Right");
            }

            switch (gunType)
            {
                case 0: // Gun type 0 - Light weapon effect
                    ipcClient.TriggerEffectWeapon(controllerType, 2, 4, 4);
                    break;

                case 1: // Gun type 1 - Medium weapon effect
                    ipcClient.TriggerEffectWeapon(controllerType, 2, 4, 8);
                    break;

                case 2: // Gun type 2 - Heavy weapon effect
                    ipcClient.TriggerEffectWeapon(controllerType, 0, 4, 8);
                    break;

                case 3: // Gun type 3 - Multiple position feedback
                    byte[] strengthPattern = new byte[10] { 5, 0, 0, 6, 0, 0, 7, 0, 0, 8 };
                    ipcClient.TriggerEffectMultiplePositionFeedback(controllerType, strengthPattern);
                    break;

                case 4: // Gun type 4 - No effect
                    ipcClient.TriggerEffectDisable(controllerType);
                    break;

                case 5: // Gun type 5 - Slope feedback
                    ipcClient.TriggerEffectSlopeFeedback(controllerType, 1, 9, 8, 1);
                    break;

                default:
                    // Default to basic feedback
                    ipcClient.TriggerEffectFeedback(controllerType, 128, 100);
                    break;
            }
        }

        public override void OnApplicationQuit()
        {
            // Clean up IPC connection when game closes
            if (ipcClient != null)
            {
                ipcClient.Stop();
                MelonLogger.Msg("PSVR2 IPC connection closed.");
            }
        }
    }
}