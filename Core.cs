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
        private Gun lastCurrentGun = null;

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

            // Log detailed information for debugging
            MelonLogger.Msg($"Update - Current gun left: {manager.currentGunLeft != null}, Current gun right: {manager.currentGunRight != null}, Dual wield: {dualWieldActive}");
            
            if (currentGun == null) return;

            // Log the gun object references to see if they change
            MelonLogger.Msg($"Update - Left gun object: {manager.currentGunLeft}, Right gun object: {manager.currentGunRight}, Current gun object: {currentGun}");
            
            // Determine which hand is actually being used by checking which gun is the current gun
            EVRControllerType activeController = EVRControllerType.Right; // Default
            if (currentGun == manager.currentGunLeft)
            {
                activeController = EVRControllerType.Left;
                MelonLogger.Msg("Determined active controller is LEFT based on current gun");
            }
            else if (currentGun == manager.currentGunRight)
            {
                activeController = EVRControllerType.Right;
                MelonLogger.Msg("Determined active controller is RIGHT based on current gun");
            }
            
            Gun dominantGun = currentGun; // The current gun is the one being used
            int currentGunType = (int)dominantGun.gunType;
            
            // Log gun information
            if (manager.currentGunLeft != null)
                MelonLogger.Msg($"Update - Left gun type: {(int)manager.currentGunLeft.gunType}");
            if (manager.currentGunRight != null)
                MelonLogger.Msg($"Update - Right gun type: {(int)manager.currentGunRight.gunType}");
            MelonLogger.Msg($"Update - Dominant gun type: {currentGunType}");
            
            // Check if this is a valid gun type
            if (currentGunType < 0 || currentGunType > 5)
            {
                MelonLogger.Warning($"Unknown gun type detected: {currentGunType}");
            }
            
            // Detect changes by comparing the current gun object, gun type, and dual wield state
            bool gunChanged = (lastCurrentGun != currentGun);
            bool gunTypeChanged = (currentGunType != lastGunType);
            bool dualWieldChanged = (dualWieldActive != lastDualWieldState);
            
            // Log detailed information for debugging
            MelonLogger.Msg($"Update - Current gun changed: {gunChanged}, Gun type changed: {gunTypeChanged}, Dual wield changed: {dualWieldChanged}, Active controller: {activeController}");
            
            // Update triggers if gun, gun type, or dual wield state changed
            if (gunChanged || gunTypeChanged || dualWieldChanged)
            {
                MelonLogger.Msg($"Applying effects - Gun type: {currentGunType}, Dual wield: {dualWieldActive}, Active controller: {activeController}, Gun changed: {gunChanged}");
                
                lastGunType = currentGunType;
                lastDualWieldState = dualWieldActive;
                lastCurrentGun = currentGun;

                if (dualWieldActive)
                {
                    ApplyDualWieldTriggerEffects(currentGunType);
                }
                else
                {
                    ApplySingleHandTriggerEffectsDirect(currentGunType, activeController);
                }
            }
        }

        private void ApplyDualWieldTriggerEffects(int gunType)
        {
            // Reset all effects first to prevent conflicts
            ipcClient.TriggerEffectDisable(EVRControllerType.Both);
            
            // Small delay to ensure reset takes effect
            System.Threading.Thread.Sleep(10);
            
            MelonLogger.Msg($"Applying dual wield effect for gun type {gunType}");
            
            switch (gunType)
            {
                case 0: // Gun type 0 - Light weapon effect
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Left, 2, 4, 4);
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Right, 2, 4, 4);
                    break;

                case 1: // Gun type 1 - Medium weapon effect
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Left, 2, 4, 8);
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Right, 2, 4, 8);
                    break;

                case 2: // Gun type 2 - Heavy weapon effect - Multiple position feedback
                    byte[] strengthPattern2 = new byte[10] { 4, 0, 0, 5, 0, 0, 6, 0, 0, 7 };
                    ipcClient.TriggerEffectMultiplePositionFeedback(EVRControllerType.Left, strengthPattern2);
                    ipcClient.TriggerEffectMultiplePositionFeedback(EVRControllerType.Right, strengthPattern2);
                    break;

                case 3: // Gun type 3 - Slope feedback
                    ipcClient.TriggerEffectSlopeFeedback(EVRControllerType.Left, 1, 9, 8, 1);
                    ipcClient.TriggerEffectSlopeFeedback(EVRControllerType.Right, 1, 9, 8, 1);
                    break;

                case 4: // Gun type 4 - No effect (silenced weapon?)
                    // Already disabled above, no additional action needed
                    break;

                case 5: // Gun type 5 - Strong feedback
                    MelonLogger.Msg("Applying gun type 5 strong feedback on both controllers: position=5, strength=8");
                    ipcClient.TriggerEffectWeapon(EVRControllerType.Both, 2, 4, 4);
                    break;

                default:
                    // Default to basic feedback for unknown gun types
                    MelonLogger.Msg($"Unknown gun type {gunType}, applying default feedback");
                    ipcClient.TriggerEffectFeedback(EVRControllerType.Both, 128, 100);
                    break;
            }
        }

        private void ApplySingleHandTriggerEffectsDirect(int gunType, EVRControllerType controllerType)
        {
            // Reset all effects first to prevent conflicts
            ipcClient.TriggerEffectDisable(EVRControllerType.Both);
            
            // Small delay to ensure reset takes effect
            System.Threading.Thread.Sleep(10);
            
            MelonLogger.Msg($"Applying single hand effect for gun type {gunType} on {controllerType} controller");

            switch (gunType)
            {
                case 0: // Gun type 0 - Light weapon effect
                    ipcClient.TriggerEffectWeapon(controllerType, 2, 4, 4);
                    break;

                case 1: // Gun type 1 - Medium weapon effect
                    ipcClient.TriggerEffectWeapon(controllerType, 2, 4, 8);
                    break;

                case 2: // Gun type 2 - Heavy weapon effect - Multiple position feedback
                    byte[] strengthPattern2 = new byte[10] { 4, 0, 0, 5, 0, 0, 6, 0, 0, 7 };
                    ipcClient.TriggerEffectMultiplePositionFeedback(controllerType, strengthPattern2);
                    break;

                case 3: // Gun type 3 - Slope feedback
                    ipcClient.TriggerEffectSlopeFeedback(controllerType, 1, 9, 8, 1);
                    break;

                case 4: // Gun type 4 - No effect
                    // Already disabled above, no additional action needed
                    break;

                case 5: // Gun type 5 - Strong feedback
                    MelonLogger.Msg($"Applying gun type 5 strong feedback on {controllerType}: position=5, strength=8");
                    ipcClient.TriggerEffectWeapon(controllerType, 2, 4, 4);
                    break;

                default:
                    // Default to basic feedback
                    MelonLogger.Msg($"Unknown gun type {gunType}, applying default feedback");
                    ipcClient.TriggerEffectFeedback(controllerType, 5, 5);
                    break;
            }
        }

        public override void OnApplicationQuit()
        {
            // Reset all effects before closing
            if (ipcClient != null)
            {
                MelonLogger.Msg("Resetting all trigger effects before closing...");
                ipcClient.TriggerEffectDisable(EVRControllerType.Both);
                System.Threading.Thread.Sleep(50); // Give time for the reset to take effect
                
                ipcClient.Stop();
                MelonLogger.Msg("PSVR2 IPC connection closed.");
            }
        }
    }
}