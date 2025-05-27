// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;
using Stride.Physics;
using Stride.Engine.Events; // Required for EventKey and EventReceiver
using MySurvivalGame.Game.Player; // Required for PlayerLockOnManager
using MySurvivalGame.Game.Combat; // Required for TargetableComponent

namespace MySurvivalGame.Game // MODIFIED: Namespace updated
{
    public enum CameraMode
    {
        FPS,
        TPS
    }

    public class PlayerCamera : SyncScript
    {
        // --- FPS Settings ---
        /// <summary>
        /// The default height of the FPS camera target relative to the character root
        /// </summary>
        public float FpsTargetHeight { get; set; } = 1.6f;

        // --- TPS Settings ---
        /// <summary>
        /// The default distance from the player to the TPS camera
        /// </summary>
        public float DefaultTpsDistance { get; set; } = 4.0f;

        /// <summary>
        /// The default height offset of the TPS camera target relative to the character root
        /// </summary>
        public float DefaultTpsHeightOffset { get; set; } = 1.8f; // Slightly above player head

        /// <summary>
        /// How far the TPS camera should stay from obstacles
        /// </summary>
        public float TpsCollisionMargin { get; set; } = 0.2f;

        // --- Common Settings ---
        /// <summary>
        /// The mouse sensitivity. Note: PlayerInput script might also apply sensitivity.
        /// This value might need to be 1.0 if PlayerInput already handles it.
        /// </summary>
        public float CameraSensitivity { get; set; } = 1.0f; // MODIFIED: Default to 1.0 assuming PlayerInput handles sensitivity

        /// <summary>
        /// The minimum rotation X in degrees
        /// </summary>
        public float RotationXMin { get; set; } = -70.0f;

        /// <summary>
        /// The maximum rotation X in degrees
        /// </summary>
        public float RotationXMax { get; set; } = 70.0f;

        /// <summary>
        /// The player entity this camera is attached to. This should be the root player entity.
        /// </summary>
        public Entity Player { get; set; }

        /// <summary>
        /// The input component for the player. This should be the PlayerInput script instance on the Player entity.
        /// </summary>
        public PlayerInput PlayerInput { get; set; }

        private float yaw;
        private float pitch;
        private CameraMode currentMode = CameraMode.FPS;
        private Simulation simulation; 
        private Vector2 currentCameraInputDelta; // Stores mouse/gamepad input from PlayerInput

        // Event listeners
        private EventReceiver<Vector2> cameraDirectionEventListener;
        private EventListener<EventKey> switchCameraModeEventListener;
        private EventReceiver<Entity> targetLockedListener;
        private EventReceiver targetUnlockedListener;

        // Lock-on state
        private Entity lockedTarget;
        private PlayerLockOnManager playerLockOnManager;


        public override void Start()
        {
            // Default values
            yaw = 0.0f; // Initialize yaw from player's current orientation if needed
            pitch = 0.0f;
            currentCameraInputDelta = Vector2.Zero;

            // Initialize from Player's current rotation to ensure camera starts facing where player is.
            if (Player != null)
            {
                Player.Transform.Rotation.ToYawPitchRoll(out yaw, out var initialPitch, out _);
                // Use initialPitch for the camera's pitch, respecting constraints.
                // This assumes player's forward is along Z. If not, adjust yaw offset.
                pitch = MathUtil.Clamp(initialPitch, MathUtil.DegreesToRadians(RotationXMin), MathUtil.DegreesToRadians(RotationXMax));

            }


            Game.IsMouseVisible = false; // Consider managing this globally or based on UI state

            simulation = this.GetSimulation();

            if (PlayerInput == null && Player != null)
            {
                // Try to get PlayerInput from the Player entity if not set
                PlayerInput = Player.Get<PlayerInput>();
            }
            
            // Subscribe to events from PlayerInput
            if (PlayerInput != null)
            {
                // It's important that CameraDirectionEventKey is static in PlayerInput
                cameraDirectionEventListener = new EventReceiver<Vector2>(PlayerInput.CameraDirectionEventKey);
                // It's important that SwitchCameraModeEventKey is static in PlayerInput
                switchCameraModeEventListener = new EventListener<EventKey>(PlayerInput.SwitchCameraModeEventKey, HandleSwitchCameraMode);

            }
            else
            {
                Log.Error("PlayerInput is not assigned or found on Player entity for PlayerCamera. Camera will not respond to input.");
            }

            // Attempt to get PlayerLockOnManager from the Player entity
            if (Player != null)
            {
                playerLockOnManager = Player.Get<PlayerLockOnManager>();
                if (playerLockOnManager != null)
                {
                    targetLockedListener = new EventReceiver<Entity>(PlayerLockOnManager.TargetLockedEvent, HandleTargetLocked);
                    targetUnlockedListener = new EventReceiver(PlayerLockOnManager.TargetUnlockedEvent, HandleTargetUnlocked);
                }
                else
                {
                    Log.Warning("PlayerLockOnManager not found on Player entity. Lock-on camera adjustments will not function.");
                }
            }
            
            UpdateCameraModeVisuals(); // Initial setup for player model visibility
        }

        public override void Cancel()
        {
            // Unsubscribe from events to prevent memory leaks
            PlayerInput?.SwitchCameraModeEventKey.RemoveListener(HandleSwitchCameraMode);
            targetLockedListener?.Dispose(); // EventReceiver uses IDisposable for cleanup
            targetUnlockedListener?.Dispose();

            base.Cancel();
        }

        private void HandleTargetLocked(Entity target)
        {
            if (target != null)
            {
                Log.Info($"PlayerCamera: Target locked: {target.Name}");
                lockedTarget = target;
            }
        }

        private void HandleTargetUnlocked()
        {
            Log.Info("PlayerCamera: Target unlocked.");
            lockedTarget = null;
        }
        
        private void HandleSwitchCameraMode(EventKey sender) // MODIFIED: Parameter changed to EventKey
        {
            currentMode = (currentMode == CameraMode.FPS) ? CameraMode.TPS : CameraMode.FPS;
            if (currentMode == CameraMode.FPS && lockedTarget != null)
            {
                // If switching to FPS while locked on, optionally clear the lock
                // playerLockOnManager?.ClearLockOnTarget(); // This would call HandleTargetUnlocked
                Log.Info("Switched to FPS mode, maintaining lock-on if active. Camera will not follow target in FPS.");
            }
            UpdateCameraModeVisuals();
        }

        private void UpdateCameraModeVisuals()
        {
            if (Player == null) return;

            // Player model visibility logic
            // Attempt to find a ModelComponent on a child named "PlayerModelPlaceholder" or the first child.
            var playerModelEntity = Player.GetChild("PlayerModelPlaceholder") ?? (Player.GetChildren().Count > 0 ? Player.GetChild(0) : null);
            var playerModel = playerModelEntity?.Get<ModelComponent>();

            if (playerModel != null)
            {
                playerModel.Enabled = (currentMode == CameraMode.TPS);
            }
            else
            {
                Log.Warning("Player model (or placeholder) not found as a child of Player. Cannot set visibility for FPS/TPS switch.");
            }

            if (currentMode == CameraMode.FPS)
            {
                Log.Info("Switched to FPS mode.");
            }
            else // TPS Mode
            {
                Log.Info("Switched to TPS mode.");
            }
        }

        public override void Update()
        {
            if (Player == null || PlayerInput == null || Entity == null) // Entity is the camera entity itself
                return;

            // Receive camera input delta from PlayerInput event
            if (cameraDirectionEventListener != null && cameraDirectionEventListener.TryReceive(out var newCameraInput))
            {
                currentCameraInputDelta = newCameraInput;
            }

            // Apply sensitivity (assuming PlayerInput sends raw/normalized delta, and this script scales it)
            // PlayerInput.cs (from previous task) scales mouse delta by (MouseSensitivity / 1000.0f)
            // and gamepad delta by DeltaTime.
            // So, CameraDirectionEventKey effectively carries already-scaled rotation values.
            // The CameraSensitivity here should ideally be 1.0f if PlayerInput handles all scaling.
            // If PlayerInput.MouseSensitivity is e.g. 100, and here CameraSensitivity is 20, the net effect is 2000x.
            // Process event queues for lock-on
            targetLockedListener?.TryReceive();
            targetUnlockedListener?.TryReceive();

            // --- Camera Rotation ---
            if (lockedTarget != null && currentMode == CameraMode.TPS)
            {
                var targetable = lockedTarget.Get<TargetableComponent>();
                if (targetable != null && targetable.IsTargetable)
                {
                    var targetWorldPos = targetable.GetWorldLockOnPoint();
                    var cameraPivotPos = Player.Transform.WorldMatrix.TranslationVector + Vector3.UnitY * DefaultTpsHeightOffset;
                    
                    // Player still rotates freely with yaw input for now, camera tries to follow target
                    // This allows player to "break" lock by turning away, or for target to move out of comfortable view arc
                    yaw -= currentCameraInputDelta.X * CameraSensitivity;
                    // Player.Transform.Rotation = Quaternion.RotationY(yaw); // Player rotates based on their input

                    // Calculate direction to target
                    Vector3 directionToTarget = targetWorldPos - cameraPivotPos;
                    directionToTarget.Normalize();

                    // Derive yaw and pitch from this direction
                    float targetYaw = MathF.Atan2(directionToTarget.X, directionToTarget.Z); // Note: Stride uses (X,Z) for Y-axis rotation
                    float targetPitch = MathF.Asin(-directionToTarget.Y);

                    // Apply player's pitch input as an offset/adjustment, clamped
                    // This allows player to look slightly up/down relative to target
                    float desiredPitch = targetPitch - (currentCameraInputDelta.Y * CameraSensitivity);
                    pitch = MathUtil.Clamp(desiredPitch, MathUtil.DegreesToRadians(RotationXMin), MathUtil.DegreesToRadians(RotationXMax));
                    
                    // The camera's final yaw will be mostly determined by the target.
                    // Player's horizontal input could be used to "strafe" view or switch targets (handled by LockOnManager)
                    // For now, camera directly faces target, player can rotate independently.
                    // To make player face target: Player.Transform.Rotation = Quaternion.RotationY(targetYaw);
                    // To make camera strictly follow target from player's back:
                    Entity.Transform.Rotation = Quaternion.RotationY(targetYaw) * Quaternion.RotationX(pitch);

                    // Player entity itself should rotate towards the target if we want strafing movement
                    Player.Transform.Rotation = Quaternion.LookAtLH(Player.Transform.WorldMatrix.TranslationVector, new Vector3(targetWorldPos.X, Player.Transform.WorldMatrix.TranslationVector.Y, targetWorldPos.Z), Vector3.UnitY).ToEulerAngles().Y;


                }
                else
                {
                    // Target became invalid, clear it
                    playerLockOnManager?.ClearLockOnTarget(); // This will trigger HandleTargetUnlocked
                    // Fallback to normal rotation for this frame
                    UpdateNormalCameraRotation();
                }
            }
            else
            {
                UpdateNormalCameraRotation();
            }
            currentCameraInputDelta = Vector2.Zero; // Reset after use for this frame


            // --- Camera Positioning ---
            Vector3 cameraPivotPosition; // The point the camera looks at or originates from for raycasting
            Vector3 desiredCameraPosition; // The final position the camera should move to

            var playerWorldPosition = Player.Transform.WorldMatrix.TranslationVector;
            Vector3 desiredCameraPosition;

            if (currentMode == CameraMode.FPS)
            {
                cameraPivotPosition = playerWorldPosition + Vector3.UnitY * FpsTargetHeight;
                Entity.Transform.Position = cameraPivotPosition;
                // FPS Rotation is already set based on normal input or if locked (but not visually following target in FPS)
            }
            else // TPS Mode
            {
                cameraPivotPosition = playerWorldPosition + Vector3.UnitY * DefaultTpsHeightOffset;
                
                // Offset direction is based on the camera's current full rotation (yaw and pitch),
                // which is now influenced by the locked target if active.
                Vector3 offsetDirection = Vector3.Transform(-Vector3.UnitZ, Entity.Transform.Rotation); // Use camera's actual rotation
                desiredCameraPosition = cameraPivotPosition + offsetDirection * DefaultTpsDistance;

                // Basic Collision Detection
                if (simulation != null)
                {
                    var raycastStart = cameraPivotPosition;
                    var raycastEnd = desiredCameraPosition;
                    var hitResult = simulation.Raycast(raycastStart, raycastEnd, CollisionFilterGroups.DefaultFilter, CollisionFilterGroupFlags.DefaultFilter);

                    if (hitResult.Succeeded)
                    {
                        desiredCameraPosition = hitResult.Point + (raycastStart - raycastEnd).Normalized() * TpsCollisionMargin;
                    }
                }
                Entity.Transform.Position = desiredCameraPosition;
            }
        }

        private void UpdateNormalCameraRotation()
        {
            // Apply sensitivity 
            yaw -= currentCameraInputDelta.X * CameraSensitivity;
            pitch -= currentCameraInputDelta.Y * CameraSensitivity;

            // Clamp pitch
            pitch = MathUtil.Clamp(pitch, MathUtil.DegreesToRadians(RotationXMin), MathUtil.DegreesToRadians(RotationXMax));

            // Player orientation (only yaw, pitch is for camera only)
            Player.Transform.Rotation = Quaternion.RotationY(yaw);
            
            // Camera's rotation
            Entity.Transform.Rotation = Quaternion.RotationY(yaw) * Quaternion.RotationX(pitch);
        }
    }
}
