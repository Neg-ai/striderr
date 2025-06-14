// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Input;
using Stride.Core.Mathematics; // Required for Vector2
// using FirstPersonShooter.Core; // This namespace might not be needed if Utils class is not used or redefined

namespace MySurvivalGame.Game
{
    public class PlayerInput : SyncScript
    {
        /// <summary>
        /// Raised every frame with the intended direction of movement from the player.
        /// </summary>
        public static readonly EventKey<Vector3> MoveDirectionEventKey = new EventKey<Vector3>(); // MODIFIED: Uncommented

        public static readonly EventKey<Vector2> CameraDirectionEventKey = new EventKey<Vector2>();
        public static readonly EventKey SwitchCameraModeEventKey = new EventKey(); // MODIFIED: Uncommented
        public static readonly EventKey JumpEventKey = new EventKey(); // ADDED: For Jump
        public static readonly EventKey ToggleMeleeModeEventKey = new EventKey(); // ADDED: For Melee Mode Toggle
        public static readonly EventKey ToggleLockOnEventKey = new EventKey(); // ADDED: For Lock-On
        public static readonly EventKey<int> SwitchLockOnTargetEventKey = new EventKey<int>(); // ADDED: For Lock-On Target Switching
        public static readonly EventKey<int> HotbarSlotSelectedEventKey = new EventKey<int>(); // ADDED: For hotbar selection
        public static readonly EventKey InteractEventKey = new EventKey(); // ADDED: For interaction
        public static readonly EventKey LightAttackEventKey = new EventKey();
        public static readonly EventKey HeavyAttackEventKey = new EventKey();
        public static readonly EventKey DodgeEventKey = new EventKey();
        public static readonly EventKey BlockEventKey = new EventKey();
        public static readonly EventKey PrimaryAction_Held_EventKey = new EventKey();
        public static readonly EventKey PrimaryAction_Released_EventKey = new EventKey();


        // public static readonly EventKey<bool> ShootEventKey = new EventKey<bool>();
        // public static readonly EventKey<bool> ReloadEventKey = new EventKey<bool>();
        // public static readonly EventKey ShootReleasedEventKey = new EventKey();
        // public static readonly EventKey ToggleBuildModeEventKey = new EventKey();
        // public static readonly EventKey RotateBuildActionLeftEventKey = new EventKey();
        // public static readonly EventKey RotateBuildActionRightEventKey = new EventKey();
        // public static readonly EventKey CycleBuildableNextEventKey = new EventKey();
        // public static readonly EventKey CycleBuildablePrevEventKey = new EventKey();
        // public static readonly EventKey DebugDestroyEventKey = new EventKey();

        public float DeadZone { get; set; } = 0.25f;

        // This CameraComponent reference is not strictly needed by PlayerInput anymore if MoveDirectionEventKey is removed,
        // but it might be useful for other inputs later or if the FPS template's Camera property was used by other systems.
        // For now, it's kept as it was in the original template PlayerInput.
        public CameraComponent? Camera { get; set; } 

        /// <summary>
        /// Multiplies mouse movement by this amount to apply aim rotations
        /// </summary>
        public float MouseSensitivity { get; set; } = 100.0f;

        // Key bindings for movement, shooting, etc., are commented out as their corresponding event keys are.
        public List<Keys> KeysLeft { get; set; } = new List<Keys>() { Keys.A, Keys.Left }; // MODIFIED: Uncommented
        public List<Keys> KeysRight { get; set; } = new List<Keys>() { Keys.D, Keys.Right }; // MODIFIED: Uncommented
        public List<Keys> KeysUp { get; set; } = new List<Keys>() { Keys.W, Keys.Up }; // MODIFIED: Uncommented
        public List<Keys> KeysDown { get; set; } = new List<Keys>() { Keys.S, Keys.Down }; // MODIFIED: Uncommented
        public List<Keys> KeysJump { get; set; } = new List<Keys>() { Keys.Space }; // ADDED: For Jump
        public List<Keys> KeysToggleMeleeMode { get; set; } = new List<Keys>() { Keys.V }; // ADDED: For Melee Mode Toggle
        public List<Keys> KeysToggleLockOn { get; set; } = new List<Keys>() { Keys.MiddleMouseButton, Keys.R3 }; // ADDED: For Lock-On
        public List<Keys> KeysSwitchLockOnTargetNext { get; set; } = new List<Keys>(); // MODIFIED: Mouse wheel handled directly
        public List<Keys> KeysSwitchLockOnTargetPrevious { get; set; } = new List<Keys>(); // MODIFIED: Mouse wheel handled directly
        // Gamepad for target switching (RightShoulder/LeftShoulder) will be handled if we add specific gamepad logic or map them to abstract buttons.
        // For now, MouseWheel is primary. If R/L Shoulder are needed, they can be added to these lists or as separate bindings.
        public List<Keys> KeysLightAttack { get; set; } = new List<Keys>() { Keys.X };
        public List<Keys> KeysHeavyAttack { get; set; } = new List<Keys>() { Keys.C };
        public List<Keys> KeysDodge { get; set; } = new List<Keys>() { Keys.LeftControl };
        public List<Keys> KeysBlock { get; set; } = new List<Keys>() { Keys.MouseButtonRight };


        // public List<Keys> KeysReload { get; set; } = new List<Keys>() { Keys.R };
        public List<Keys> KeysSwitchCamera { get; set; } = new List<Keys>() { Keys.T }; // MODIFIED: Uncommented
        // public List<Keys> KeysToggleBuildMode { get; set; } = new List<Keys>() { Keys.B };
        // public List<Keys> KeysRotateBuildLeft { get; set; } = new List<Keys>() { Keys.OemComma };
        // public List<Keys> KeysRotateBuildRight { get; set; } = new List<Keys>() { Keys.OemPeriod };
        // public List<Keys> KeysCycleBuildableNext { get; set; } = new List<Keys>() { Keys.PageUp };
        // public List<Keys> KeysCycleBuildablePrev { get; set; } = new List<Keys>() { Keys.PageDown };
        // public List<Keys> KeysDebugDestroy { get; set; } = new List<Keys>() { Keys.K };

        private bool isPrimaryActionKeyDown = false; // Track hold state for primary action

        public PlayerInput()
        {
            // Fix single frame input lag
            Priority = -1000;
        }

        public override void Update()
        {
            // Character movement
            //  The character movement can be controlled by a game controller or a keyboard
            //  The character receives input in 3D world space, so that it can be controlled by an AI script as well
            //  For this reason we map the 2D user input to a 3D movement using the current camera
            { // MODIFIED: Uncommented block
                // Game controller: left stick
                var moveDirection = Input.GetLeftThumbAny(DeadZone);
                var isDeadZoneLeft = moveDirection.Length() < DeadZone;
                if (isDeadZoneLeft)
                    moveDirection = Vector2.Zero;
                else
                    moveDirection.Normalize();

                // Keyboard
                if (KeysLeft.Any(key => Input.IsKeyDown(key)))
                    moveDirection += -Vector2.UnitX;
                if (KeysRight.Any(key => Input.IsKeyDown(key)))
                    moveDirection += +Vector2.UnitX;
                if (KeysUp.Any(key => Input.IsKeyDown(key)))
                    moveDirection += +Vector2.UnitY;
                if (KeysDown.Any(key => Input.IsKeyDown(key)))
                    moveDirection += -Vector2.UnitY;

                // Broadcast the movement vector as a world-space Vector3 to allow characters to be controlled
                // The Utils class will be needed here.
                var worldSpeed = (Camera != null)
                    ? MySurvivalGame.Game.Core.Utils.LogicDirectionToWorldDirection(moveDirection, Camera, Vector3.UnitY) // MODIFIED: Added Core namespace
                    : new Vector3(moveDirection.X, 0, moveDirection.Y); 

                MoveDirectionEventKey.Broadcast(worldSpeed);
            }

            // Camera rotation
            {
                // Game controller: right stick
                var cameraDirection = Input.GetRightThumbAny(DeadZone);
                var isDeadZoneRight = cameraDirection.Length() < DeadZone;
                if (isDeadZoneRight)
                    cameraDirection = Vector2.Zero;
                else
                    cameraDirection.Normalize();
                
                cameraDirection *= (float)Game.UpdateTime.Elapsed.TotalSeconds;

                // Mouse-based camera rotation.
                if (Input.IsMouseButtonDown(MouseButton.Left)) // Or any other button to lock mouse
                {
                    Input.LockMousePosition(true);
                    Game.IsMouseVisible = false;
                }
                if (Input.IsKeyPressed(Keys.Escape))
                {
                    Input.UnlockMousePosition();
                    Game.IsMouseVisible = true;
                }
                if (Input.IsMousePositionLocked)
                {
                    // Adjust sensitivity application if necessary. Original was just MouseSensitivity.
                    // The template multiplies by MouseSensitivity, but some prefer to divide by a factor.
                    // For now, keeping it as in the template.
                    cameraDirection += new Vector2(Input.MouseDelta.X, -Input.MouseDelta.Y) * (MouseSensitivity / 1000.0f); // Adjusted sensitivity scaling
                }

                CameraDirectionEventKey.Broadcast(cameraDirection);
            }

            // Shooting logic commented out
            /*
            {
                // ... (shooting logic) ...
                ShootEventKey.Broadcast(didShoot);
                // ... (shoot release logic) ...
                ShootReleasedEventKey.Broadcast();
            }
            */

            // Reload logic commented out
            /*
            {
                // ... (reload logic) ...
                ReloadEventKey.Broadcast(isReloading);
            }
            */

            // Camera mode switch logic
            {
                if (KeysSwitchCamera.Any(key => Input.IsKeyPressed(key))) // MODIFIED: Uncommented block
                {
                    SwitchCameraModeEventKey.Broadcast();
                }
            }

            // Building mode logic commented out
            /*
            {
                // ... (building mode logic) ...
            }
            */

            // Jump logic
            {
                if (KeysJump.Any(key => Input.IsKeyPressed(key)))
                {
                    JumpEventKey.Broadcast();
                }
            }

            // Melee Mode Toggle Logic
            {
                if (KeysToggleMeleeMode.Any(key => Input.IsKeyPressed(key)))
                {
                    ToggleMeleeModeEventKey.Broadcast();
                }
            }

            // Lock-On Logic
            {
                if (KeysToggleLockOn.Any(key => Input.IsKeyPressed(key)))
                {
                    ToggleLockOnEventKey.Broadcast();
                }

                // Mouse Wheel for Lock-On Target Switching
                if (Input.MouseWheelDelta > 0)
                {
                    SwitchLockOnTargetEventKey.Broadcast(1);
                }
                else if (Input.MouseWheelDelta < 0)
                {
                    SwitchLockOnTargetEventKey.Broadcast(-1);
                }

                // Gamepad RightShoulder/LeftShoulder for target switching
                // Using IsGamepadButtonReleasedAny to ensure it fires once per press
                if (Input.IsGamepadButtonReleasedAny(0, GamepadButton.RightShoulder))
                {
                     SwitchLockOnTargetEventKey.Broadcast(1);
                }
                if (Input.IsGamepadButtonReleasedAny(0, GamepadButton.LeftShoulder))
                {
                     SwitchLockOnTargetEventKey.Broadcast(-1);
                }
                // Allow keyboard to also switch targets if lists are populated
                if (KeysSwitchLockOnTargetNext.Any(key => Input.IsKeyPressed(key)))
                {
                    SwitchLockOnTargetEventKey.Broadcast(1);
                }
                if (KeysSwitchLockOnTargetPrevious.Any(key => Input.IsKeyPressed(key)))
                {
                    SwitchLockOnTargetEventKey.Broadcast(-1);
                }
            }

            // Light Attack (Primary Action) - Initial Press, Held, Released
            // Initial Press (existing logic for LightAttackEventKey)
            if (KeysLightAttack.Any(key => Input.IsKeyPressed(key)) || Input.IsGamepadButtonPressedAny(0, GamepadButton.RightTrigger))
            {
                LightAttackEventKey.Broadcast();
            }

            // Held / Released Logic for Primary Action (KeysLightAttack and RightTrigger)
            bool primaryActionCurrentlyPressed = false;
            foreach (var key in KeysLightAttack)
            {
                if (Input.IsKeyDown(key))
                {
                    primaryActionCurrentlyPressed = true;
                    break;
                }
            }
            if (!primaryActionCurrentlyPressed && Input.GetGamePad(0) != null) // Check gamepad only if keyboard not pressed
            {
                 if (Input.IsGamepadButtonDownAny(0, GamepadButton.RightTrigger)) // Assuming Right Trigger for primary action hold
                 {
                    primaryActionCurrentlyPressed = true;
                 }
            }

            if (primaryActionCurrentlyPressed)
            {
                if (!isPrimaryActionKeyDown)
                {
                    // This is the frame the key/button was pressed.
                    // LightAttackEventKey is already broadcast by IsKeyPressed/IsGamepadButtonPressedAny.
                    isPrimaryActionKeyDown = true;
                    // Potentially broadcast a specific "PrimaryAction_Down_EventKey" here if needed,
                    // but LightAttackEventKey might already serve this purpose.
                }
                PrimaryAction_Held_EventKey.Broadcast(); // Broadcast every frame it's held
            }
            else // Not currently pressed
            {
                if (isPrimaryActionKeyDown)
                {
                    // Was pressed, now released
                    PrimaryAction_Released_EventKey.Broadcast();
                    isPrimaryActionKeyDown = false;
                }
            }

            // Heavy Attack
            if (KeysHeavyAttack.Any(key => Input.IsKeyPressed(key)))
            {
                HeavyAttackEventKey.Broadcast();
            }

            // Dodge
            if (KeysDodge.Any(key => Input.IsKeyPressed(key)) || Input.IsGamepadButtonReleasedAny(0, GamepadButton.B))
            {
                DodgeEventKey.Broadcast();
            }

            // Block
            if (KeysBlock.Any(key => Input.IsKeyPressed(key)) || Input.IsGamepadButtonReleasedAny(0, GamepadButton.LeftTrigger))
            {
                BlockEventKey.Broadcast();
            }
            
            // Debug Destroy logic commented out
            /*
            {
                if (KeysDebugDestroy.Any(key => Input.IsKeyPressed(key)))
                {
                    DebugDestroyEventKey.Broadcast();
                }
            }
            */

            // ADDED: Hotbar Slot Selection Input
            if (Input.IsKeyPressed(Keys.D1)) HotbarSlotSelectedEventKey.Broadcast(0);
            if (Input.IsKeyPressed(Keys.D2)) HotbarSlotSelectedEventKey.Broadcast(1);
            if (Input.IsKeyPressed(Keys.D3)) HotbarSlotSelectedEventKey.Broadcast(2);
            if (Input.IsKeyPressed(Keys.D4)) HotbarSlotSelectedEventKey.Broadcast(3);
            if (Input.IsKeyPressed(Keys.D5)) HotbarSlotSelectedEventKey.Broadcast(4);
            if (Input.IsKeyPressed(Keys.D6)) HotbarSlotSelectedEventKey.Broadcast(5);
            if (Input.IsKeyPressed(Keys.D7)) HotbarSlotSelectedEventKey.Broadcast(6);
            if (Input.IsKeyPressed(Keys.D8)) HotbarSlotSelectedEventKey.Broadcast(7);

            // ADDED: Interaction Input
            if (Input.IsKeyPressed(Keys.E))
            {
                InteractEventKey.Broadcast();
            }
        }
    }
}
