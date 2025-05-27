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

            if (playerControllerRef != null && playerControllerRef.IsLockedOn && playerControllerRef.CurrentLockOnTarget != null)
            {
                var targetComponent = playerControllerRef.CurrentLockOnTarget.Get<TargetableComponent>();
                if (targetComponent != null)
                {
                    Vector3 targetLockOnPoint = targetComponent.GetWorldLockOnPoint();
                    Vector3 playerReferencePosition;
                    if (currentMode == CameraMode.FPS)
                    {
                        playerReferencePosition = Player.Transform.WorldMatrix.TranslationVector + Vector3.UnitY * FpsTargetHeight;
                    }
                    else // TPS
                    {
                        // For TPS, the "head" or pivot point might be what we want to aim from, or camera position itself.
                        // Let's use the camera's current position as the reference for looking at the target's lock-on point.
                        // This creates a smoother follow cam rather than a rigid look-at from player model.
                        // Alternatively, use playerWorldPosition + Vector3.UnitY * DefaultTpsHeightOffset;
                        playerReferencePosition = Entity.Transform.Position; 
                    }

                    Vector3 directionToTarget = targetLockOnPoint - playerReferencePosition;
                    if (directionToTarget.LengthSquared() > MathUtil.ZeroTolerance)
                    {
                        directionToTarget.Normalize();
                        // Calculate yaw: angle around Y axis. Atan2 takes (x, z) or (z, x) depending on convention.
                        // Stride's forward is -Z. A common Atan2 usage for yaw from a direction vector (dx, dy, dz) is Atan2(dx, dz).
                        // If our camera's "0 yaw" faces -Z, then:
                        yaw = (float)Math.Atan2(directionToTarget.X, directionToTarget.Z); // This will make camera face target directly.
                        // Player input for yaw should ideally be for orbiting, not overriding this.
                        // For pitch:
                        pitch = (float)Math.Asin(-directionToTarget.Y); // Y is up, so -Y for standard pitch calculation from direction vector.
                    }
                }
                // Player input for camera movement (currentCameraInputDelta) will still be applied after this, allowing orbit/offset.
                // For a stricter lock, disable or modify input application here.
            }
            
            // Receive camera input delta from PlayerInput event IF NOT STRICTLY LOCKED or for orbit
            // If strictly locked, we might want to ignore currentCameraInputDelta or use it for orbiting.
            // For now, input is still processed.
            if (cameraDirectionEventListener != null && cameraDirectionEventListener.TryReceive(out var newCameraInput))
            {
                currentCameraInputDelta = newCameraInput;
            }

            // Apply sensitivity only if there's input to apply (and potentially not fully locked)
            if (currentCameraInputDelta.LengthSquared() > MathUtil.ZeroTolerance)
            {
                 // If locked on, player input could be used for orbiting adjustments rather than direct override.
                 // For now, it adds to the yaw/pitch calculated by lock-on.
                yaw -= currentCameraInputDelta.X * CameraSensitivity; 
                pitch -= currentCameraInputDelta.Y * CameraSensitivity; 
                currentCameraInputDelta = Vector2.Zero; // Reset after use for this frame
            }


            // Clamp pitch
            pitch = MathUtil.Clamp(pitch, MathUtil.DegreesToRadians(RotationXMin), MathUtil.DegreesToRadians(RotationXMax));

            // Player entity's rotation (yaw) should be controlled by PlayerController when locked on.
            // If not locked on, PlayerCamera controls Player's yaw.
            if (playerControllerRef == null || !playerControllerRef.IsLockedOn)
            {
                Player.Transform.Rotation = Quaternion.RotationY(yaw);
            }
            // Else, PlayerController handles Player.Transform.Rotation to face the target.

            // Camera entity's rotation (yaw and pitch)
            Entity.Transform.Rotation = Quaternion.RotationY(yaw) * Quaternion.RotationX(pitch);


            // --- Camera Positioning ---
            Vector3 cameraTargetPosition; // The point the camera looks at or originates from for raycasting
            Vector3 desiredCameraPosition; // The final position the camera should move to

            var playerWorldPosition = Player.Transform.WorldMatrix.TranslationVector;

            if (currentMode == CameraMode.FPS)
            {
                // Camera is positioned at the FpsTargetHeight on the Player entity.
                // Player.Transform.Position is the base of the player.
                // The Camera entity (this.Entity) is what needs to be positioned.
                // If Camera is a child of Player, its local position is relative to Player.
                desiredCameraPosition = Player.Transform.WorldMatrix.TranslationVector + Vector3.Transform(new Vector3(0, FpsTargetHeight, 0), Player.Transform.Rotation);
                // More simply, if camera is child of player and player rotation is already set:
                // Entity.Transform.LocalPosition = new Vector3(0, FpsTargetHeight, 0);
                // Entity.Transform.Position will be automatically calculated.
                // For FPS, the camera entity should be directly at the eye height, and share player's yaw, and have its own pitch.
                // The Player entity rotates with yaw. The Camera entity (this script is on) also rotates with yaw AND pitch.
                // So, the camera's position is relative to the already rotated Player.
                cameraTargetPosition = playerWorldPosition + Vector3.UnitY * FpsTargetHeight; // More accurate if player is base
                Entity.Transform.Position = cameraTargetPosition;
                Entity.Transform.Rotation = Quaternion.RotationY(yaw) * Quaternion.RotationX(pitch);


            }
            else // TPS Mode
            {
                // TPS camera orbits around a point slightly above the player's root.
                cameraTargetPosition = playerWorldPosition + Vector3.UnitY * DefaultTpsHeightOffset;
                
                // Offset direction is based on the camera's current full rotation (yaw and pitch)
                // Matrix rotationMatrix = Matrix.RotationYawPitchRoll(yaw, pitch, 0); // Using Entity.Transform.Rotation is also fine
                // Vector3 offsetDirection = Vector3.TransformNormal(-Vector3.UnitZ, rotationMatrix);
                Vector3 offsetDirection = Vector3.TransformNormal(-Vector3.UnitZ, Entity.Transform.Rotation); // Entity.Transform.Rotation is already yaw*pitch

                desiredCameraPosition = cameraTargetPosition + offsetDirection * DefaultTpsDistance;

                // Basic Collision Detection
                var raycastStart = cameraTargetPosition;
                var characterComponent = Player?.Get<CharacterComponent>(); // Get character component of the parent

                // Use a slightly smaller ray for collision to avoid starting inside geometry if player is too close to a wall
                var rayDirection = desiredCameraPosition - raycastStart;
                var rayLength = rayDirection.Length();
                if (rayLength > MathUtil.ZeroTolerance) // Ensure rayDirection is not zero
                {
                    rayDirection.Normalize();
                }
                
                // Perform raycast from a point slightly in front of the target towards the desired camera position
                var raycastActualStart = raycastStart; // + offsetDirection * 0.1f; // Optional: start ray slightly away from pivot

                HitResult hitResult;
                // Only cast if ray is not too short
                if (rayLength > TpsCollisionMargin) 
                {
                     hitResult = simulation.Raycast(raycastActualStart, desiredCameraPosition,
                        characterComponent != null ? new[] { characterComponent } : null, // Exclude player's character collider
                        CollisionFilterGroups.Default, // Collide with default group
                        CollisionFilterGroupFlags.Default); // Collide with default group flags
                }
                else
                {
                    hitResult = new HitResult { Succeeded = false }; // Treat very short rays as no hit to prevent issues
                }


                if (hitResult.Succeeded)
                {
                    // Move camera to hit point, plus a small margin so it doesn't clip into the geometry
                    // The offsetDirection is from camera to target, so we use -offsetDirection to push away from wall
                    desiredCameraPosition = hitResult.Point - offsetDirection * TpsCollisionMargin; // Use the defined margin
                }
                Entity.Transform.Position = desiredCameraPosition;
                Entity.Transform.Rotation = Quaternion.RotationY(yaw) * Quaternion.RotationX(pitch);
            }
        }
    }
}
