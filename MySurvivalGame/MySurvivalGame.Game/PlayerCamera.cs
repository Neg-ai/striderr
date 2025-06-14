// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;
using Stride.Physics;
using Stride.Engine.Events; // Required for EventKey and EventReceiver

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
        public float DefaultTpsDistance { get; set; } = 3.0f; // Subtask specified 3.0f

        /// <summary>
        /// The default height offset of the TPS camera target relative to the character root (from Player's origin)
        /// </summary>
        public float DefaultTpsHeightOffset { get; set; } = 0.5f; // Subtask specified new Vector3(0, 0.5f, 0) for orbit target offset

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
        /// (Optional) Explicitly assign the player's model entity to hide in FPS mode.
        /// If not set, it will try to find a suitable model in Player's children.
        /// </summary>
        public Entity PlayerModelToHide { get; set; }


        /// <summary>
        /// The input component for the player. This should be the PlayerInput script instance on the Player entity.
        /// </summary>
        public PlayerInput PlayerInput { get; set; }

        private float yaw;
        private float pitch;
        private CameraMode currentMode = CameraMode.FPS;
        private Simulation simulation; 
        private Vector2 currentCameraInputDelta; // Stores mouse/gamepad input from PlayerInput

using MySurvivalGame.Game.Combat; // For TargetableComponent

        // Event listeners
        private EventReceiver<Vector2> cameraDirectionEventListener;
        private EventListener<EventKey> switchCameraModeEventListener;

        private PlayerController playerControllerRef; // Reference to PlayerController for lock-on state

        // Camera smoothing and lock-on orbit variables
        private Vector3 targetCameraPosition;
        private Quaternion targetCameraRotation;
        public float CameraSmoothingFactor { get; set; } = 10.0f; // Increased for faster smoothing
        private Entity previousLockOnTarget = null;
        private float currentOrbitYaw = 0f;
        private float currentOrbitPitch = 0f;
        public float MaxOrbitPitch { get; set; } = MathUtil.PiOverFour; // Max 45 degrees orbit pitch
        public float MinOrbitPitch { get; set; } = -MathUtil.PiOverFour; // Min -45 degrees orbit pitch


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

                // Get PlayerController reference
                playerControllerRef = Player.Get<PlayerController>();
                if (playerControllerRef == null)
                {
                    Log.Error("PlayerCamera could not find PlayerController component on the Player entity.");
                }
            }
            else
            {
                Log.Error("PlayerCamera: Player entity is not assigned.");
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
            
            UpdateCameraModeVisuals(); // Initial setup for player model visibility
        }

        public override void Cancel()
        {
            // Unsubscribe from events to prevent memory leaks
            PlayerInput?.SwitchCameraModeEventKey.RemoveListener(HandleSwitchCameraMode);
            // EventReceiver does not need explicit removal like this, it's managed by its lifetime.
            // if (cameraDirectionEventListener != null) { /* clean up if necessary, usually not for EventReceiver */ }

            base.Cancel();
        }
        
        private void HandleSwitchCameraMode(EventKey sender) // MODIFIED: Parameter changed to EventKey
        {
            currentMode = (currentMode == CameraMode.FPS) ? CameraMode.TPS : CameraMode.FPS;
            UpdateCameraModeVisuals();
        }

        private void UpdateCameraModeVisuals()
        {
            if (Player == null) return;

            ModelComponent playerModel = null;

            if (PlayerModelToHide != null)
            {
                playerModel = PlayerModelToHide.Get<ModelComponent>();
                if (playerModel == null)
                {
                    Log.Warning($"PlayerCamera: PlayerModelToHide '{PlayerModelToHide.Name}' does not have a ModelComponent.");
                }
            }

            if (playerModel == null) // Fallback if PlayerModelToHide is not set or has no ModelComponent
            {
                var fallbackEntity = Player.GetChild("PlayerModelPlaceholder") ?? (Player.GetChildren().Count > 0 ? Player.GetChild(0) : null);
                if (fallbackEntity != null)
                {
                    playerModel = fallbackEntity.Get<ModelComponent>();
                }

                if (playerModel == null && PlayerModelToHide == null) // Only log warning if PlayerModelToHide was also not attempted
                {
                    Log.Warning("PlayerCamera: Player model to hide not explicitly set and fallback model (or placeholder) not found as a child of Player. Cannot set visibility for FPS/TPS switch.");
                }
                else if (playerModel == null && PlayerModelToHide != null) // Warning if PlayerModelToHide was set but invalid, and fallback also failed
                {
                     Log.Warning("PlayerCamera: Fallback player model also not found. Cannot set visibility for FPS/TPS switch.");
                }
            }

            if (playerModel != null)
            {
                playerModel.Enabled = (currentMode == CameraMode.TPS);
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
            if (Player == null || PlayerInput == null || Entity == null)
                return;

            float deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;

            // Receive camera input delta from PlayerInput event
            if (cameraDirectionEventListener != null && cameraDirectionEventListener.TryReceive(out var newCameraInput))
            {
                currentCameraInputDelta = newCameraInput;
            }

            Quaternion baseIdealRotation;
            Vector3 baseIdealPosition;
            var playerWorldPosition = Player.Transform.WorldMatrix.TranslationVector;

            bool justLockedOn = false;
            bool justUnlocked = false;

            if (playerControllerRef != null && playerControllerRef.IsLockedOn && playerControllerRef.CurrentLockOnTarget != null)
            {
                if (previousLockOnTarget != playerControllerRef.CurrentLockOnTarget || previousLockOnTarget == null)
                {
                    justLockedOn = true; // Target changed or first lock-on
                    currentOrbitYaw = 0f;
                    currentOrbitPitch = 0f;
                }

                var targetComponent = playerControllerRef.CurrentLockOnTarget.Get<TargetableComponent>();
                if (targetComponent != null)
                {
                    Vector3 targetLockOnPoint = targetComponent.GetWorldLockOnPoint();
                    Vector3 cameraLookFromPosition; // Position from which the camera calculates its look-at direction

                    if (currentMode == CameraMode.FPS)
                    {
                        cameraLookFromPosition = playerWorldPosition + Vector3.UnitY * FpsTargetHeight;
                        baseIdealPosition = cameraLookFromPosition; // FPS camera stays at eye height
                    }
                    else // TPS
                    {
                        // TPS camera orbits around a point slightly above the player's root.
                        Vector3 tpsPivotPoint = playerWorldPosition + Vector3.UnitY * DefaultTpsHeightOffset;
                        // Initial desired rotation to look at target (without orbit)
                        Quaternion lookAtTargetRotation = Quaternion.LookRotation(targetLockOnPoint - tpsPivotPoint, Vector3.UnitY);

                        // Apply orbital input
                        currentOrbitYaw -= currentCameraInputDelta.X * CameraSensitivity; // Yaw is inverted for typical camera controls
                        currentOrbitPitch -= currentCameraInputDelta.Y * CameraSensitivity;
                        currentOrbitPitch = MathUtil.Clamp(currentOrbitPitch, MinOrbitPitch, MaxOrbitPitch);

                        // Combine base look-at with orbit. Orbit applies relative to the direction to target.
                        // This can be tricky. A common way: Get yaw/pitch from lookAtTargetRotation, add orbit, then recombine.
                        // Or, more directly:
                        Quaternion orbitRotation = Quaternion.RotationY(currentOrbitYaw) * Quaternion.RotationX(currentOrbitPitch);
                        baseIdealRotation = lookAtTargetRotation * orbitRotation;

                        Vector3 offsetDirection = Vector3.TransformNormal(-Vector3.UnitZ, baseIdealRotation);
                        baseIdealPosition = tpsPivotPoint + offsetDirection * DefaultTpsDistance;

                        // TPS Collision
                        var raycastStart = tpsPivotPoint;
                        var characterComponent = Player?.Get<CharacterComponent>();
                        var rayDirection = baseIdealPosition - raycastStart;
                        var rayLength = rayDirection.Length();
                        if (rayLength > MathUtil.ZeroTolerance) rayDirection.Normalize();

                        if (rayLength > TpsCollisionMargin)
                        {
                            var hitResult = simulation.Raycast(raycastStart, baseIdealPosition,
                                characterComponent != null ? new[] { characterComponent } : null,
                                CollisionFilterGroups.Default, CollisionFilterGroupFlags.Default);
                            if (hitResult.Succeeded)
                            {
                                baseIdealPosition = hitResult.Point - rayDirection * TpsCollisionMargin;
                            }
                        }
                    }
                     // For FPS, the direction to target directly gives the rotation.
                    if (currentMode == CameraMode.FPS)
                    {
                        Vector3 directionToTarget = targetLockOnPoint - baseIdealPosition;
                        if (directionToTarget.LengthSquared() > MathUtil.ZeroTolerance) directionToTarget.Normalize();
                        baseIdealRotation = Quaternion.LookRotation(directionToTarget, Vector3.UnitY);
                        // FPS Orbit (optional, typically less orbit in FPS lock-on)
                        // currentOrbitYaw -= currentCameraInputDelta.X * CameraSensitivity;
                        // currentOrbitPitch -= currentCameraInputDelta.Y * CameraSensitivity;
                        // currentOrbitPitch = MathUtil.Clamp(currentOrbitPitch, MinOrbitPitch, MaxOrbitPitch);
                        // baseIdealRotation *= Quaternion.RotationY(currentOrbitYaw) * Quaternion.RotationX(currentOrbitPitch);
                    }

                    targetCameraRotation = baseIdealRotation;
                    targetCameraPosition = baseIdealPosition;
                }
                else // Target component became null
                {
                     // Fallback to non-locked on behavior by not setting targetCamera explicitly here, it will be set below
                }
            }
            else // Not locked on (or playerControllerRef is null)
            {
                if (previousLockOnTarget != null) // Was locked on, now isn't
                {
                    justUnlocked = true;
                    // Yaw and pitch should naturally take over from player input.
                    // Reset orbit so it doesn't stick if re-locking quickly.
                    currentOrbitYaw = 0f;
                    currentOrbitPitch = 0f;
                }

                // Standard camera input processing
                yaw -= currentCameraInputDelta.X * CameraSensitivity;
                pitch -= currentCameraInputDelta.Y * CameraSensitivity;
                pitch = MathUtil.Clamp(pitch, MathUtil.DegreesToRadians(RotationXMin), MathUtil.DegreesToRadians(RotationXMax));

                if (Player != null) Player.Transform.Rotation = Quaternion.RotationY(yaw); // Player follows camera yaw

                targetCameraRotation = Quaternion.RotationY(yaw) * Quaternion.RotationX(pitch);

                if (currentMode == CameraMode.FPS)
                {
                    targetCameraPosition = playerWorldPosition + Vector3.UnitY * FpsTargetHeight;
                }
                else // TPS Mode
                {
                    Vector3 tpsPivotPoint = playerWorldPosition + Vector3.UnitY * DefaultTpsHeightOffset;
                    Vector3 offsetDirection = Vector3.TransformNormal(-Vector3.UnitZ, targetCameraRotation);
                    baseIdealPosition = tpsPivotPoint + offsetDirection * DefaultTpsDistance;

                    var raycastStart = tpsPivotPoint;
                    var characterComponent = Player?.Get<CharacterComponent>();
                    var rayDirection = baseIdealPosition - raycastStart;
                    var rayLength = rayDirection.Length();
                    if (rayLength > MathUtil.ZeroTolerance) rayDirection.Normalize();

                    if (rayLength > TpsCollisionMargin)
                    {
                        var hitResult = simulation.Raycast(raycastStart, baseIdealPosition,
                            characterComponent != null ? new[] { characterComponent } : null,
                            CollisionFilterGroups.Default, CollisionFilterGroupFlags.Default);
                        if (hitResult.Succeeded)
                        {
                            baseIdealPosition = hitResult.Point - rayDirection * TpsCollisionMargin;
                        }
                    }
                    targetCameraPosition = baseIdealPosition;
                }
            }
            currentCameraInputDelta = Vector2.Zero; // Reset input delta

            // Apply smoothing
            if (justLockedOn || justUnlocked) // Snap if lock state just changed
            {
                Entity.Transform.Position = targetCameraPosition;
                Entity.Transform.Rotation = targetCameraRotation;
            }
            else
            {
                Entity.Transform.Position = Vector3.Lerp(Entity.Transform.Position, targetCameraPosition, deltaTime * CameraSmoothingFactor);
                Entity.Transform.Rotation = Quaternion.Slerp(Entity.Transform.Rotation, targetCameraRotation, deltaTime * CameraSmoothingFactor);
            }

            previousLockOnTarget = playerControllerRef?.CurrentLockOnTarget;
        }
    }
}
