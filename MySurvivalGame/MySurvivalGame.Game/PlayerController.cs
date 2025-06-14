// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using System;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Physics;
using MySurvivalGame.Game.Combat; // Required for TargetableComponent
using System.Collections.Generic; // Required for List
using System.Linq; // Required for LINQ methods like OrderBy

// using FirstPersonShooter.Core; // MODIFIED: Removed ITargetable and this using for now

namespace MySurvivalGame.Game // MODIFIED: Namespace updated
{
    // ADDED: Enum for Melee Combat Mode
    public enum MeleeCombatMode 
    {
        Standard, // Represents 'Ark-like'
        SoulsLike 
    }

    public class PlayerController : SyncScript // MODIFIED: Removed ITargetable
    {
        [Display("Run Speed")]
        public float MaxRunSpeed { get; set; } = 5;

        [Display("Jump Speed")]
        public float JumpSpeed { get; set; } = 7.0f; // Default jump speed
        
        // Lock-On Configurable Properties
        [Display("Lock-On Max Distance")]
        public float LockOnMaxDistance { get; set; } = 15.0f;
        [Display("Lock-On Sweep Radius")]
        public float LockOnSweepSphereRadius { get; set; } = 2.0f; // Increased for better detection
        // Example filter group, ensure this matches your enemy setup
        public CollisionFilterGroupFlags LockOnTargetFilterGroup { get; set; } = CollisionFilterGroupFlags.CharacterFilter; 


        public static readonly EventKey<float> RunSpeedEventKey = new EventKey<float>(); // This can be used by an animation controller later

        // This component is the physics representation of a controllable character
        private CharacterComponent character;
        private Simulation simulation; // For physics queries (ShapeSweep)

        // Input Event Receivers
        private readonly EventReceiver<Vector3> moveDirectionEvent = new EventReceiver<Vector3>(PlayerInput.MoveDirectionEventKey);
        private readonly EventReceiver jumpEvent = new EventReceiver(PlayerInput.JumpEventKey); // ADDED: For Jump
        private readonly EventReceiver toggleMeleeModeReceiver = new EventReceiver(PlayerInput.ToggleMeleeModeEventKey); // ADDED: For Melee Mode Toggle
        private readonly EventReceiver toggleLockOnReceiver; // Initialized in Start
        private readonly EventReceiver<int> switchLockOnTargetReceiver; // Initialized in Start
        private readonly EventReceiver lightAttackReceiver;
        private readonly EventReceiver heavyAttackReceiver;
        private readonly EventReceiver dodgeReceiver;
        private readonly EventReceiver blockReceiver;


        // Lock-On State Properties
        public Entity CurrentLockOnTarget { get; private set; }
        public bool IsLockedOn => CurrentLockOnTarget != null;
        private List<Entity> potentialTargets = new List<Entity>();
        private int currentTargetIndex = -1;

        // ADDED: Property for Current Melee Combat Mode
        [Display("Melee Combat Mode")]
        public MeleeCombatMode CurrentMeleeMode { get; private set; } = MeleeCombatMode.Standard;

        // Stamina Properties
        public float CurrentStamina { get; private set; } = 100f;
        public float MaxStamina { get; private set; } = 100f;
        public float StaminaRegenRate { get; set; } = 15f; // Points per second
        public float JumpStaminaCost { get; set; } = 10f;
        public float DodgeStaminaCost { get; set; } = 20f;
        public float LightAttackStaminaCost { get; set; } = 15f;
        public float HeavyAttackStaminaCost { get; set; } = 30f;
        public float BlockStaminaDrainRate { get; set; } = 25f; // Stamina per second while holding block
        private bool isBlocking = false;
        private bool isRegeneratingStamina = true; // Flag to control stamina regen

        public Entity PlayerCameraEntity { get; set; }
        private PlayerInput playerInputScript; // To access KeysBlock


        public PlayerController() // Constructor to initialize readonly event receivers
        {
            toggleLockOnReceiver = new EventReceiver(PlayerInput.ToggleLockOnEventKey);
            switchLockOnTargetReceiver = new EventReceiver<int>(PlayerInput.SwitchLockOnTargetEventKey);
            lightAttackReceiver = new EventReceiver(PlayerInput.LightAttackEventKey);
            heavyAttackReceiver = new EventReceiver(PlayerInput.HeavyAttackEventKey);
            dodgeReceiver = new EventReceiver(PlayerInput.DodgeEventKey);
            blockReceiver = new EventReceiver(PlayerInput.BlockEventKey);
        }

        /// <summary>
        /// Called when the script is first initialized
        /// </summary>
        public override void Start()
        {
            base.Start();

            // Will search for an CharacterComponent within the same entity as this script
            character = Entity.Get<CharacterComponent>();
            if (character == null) throw new ArgumentException("Please add a CharacterComponent to the entity containing PlayerController!");

            simulation = this.GetSimulation();
            if (simulation == null) throw new ArgumentException("PlayerController requires a Simulation context.");

            playerInputScript = Entity.Get<PlayerInput>();
            if (playerInputScript == null)
            {
                Log.Error("PlayerController requires a PlayerInput script on the same entity to function correctly for block logic.");
            }

            PlayerCameraEntity = Entity.Scene.Entities.FirstOrDefault(e => e.Get<PlayerCamera>() != null);
            if (PlayerCameraEntity == null)
            {
                Log.Warning("PlayerController could not find an entity with PlayerCamera script. Locked-on movement might be affected.");
            }
        }
        
        /// <summary>
        /// Called on every frame update
        /// </summary>
        public override void Update()
        {
            var deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;
            isRegeneratingStamina = true; // Assume true, actions will set it to false

            // Handle Block Logic (Holding and Draining)
            // This needs to be checked early to potentially stop stamina regen
            if (isBlocking)
            {
                bool blockKeyHeld = (playerInputScript != null && playerInputScript.KeysBlock.Any(key => Input.IsKeyDown(key))) || Input.IsGamepadButtonDown(0, GamepadButton.LeftTrigger);
                if (blockKeyHeld && CurrentStamina > 0)
                {
                    CurrentStamina -= BlockStaminaDrainRate * deltaTime;
                    isRegeneratingStamina = false; // No regen while actively blocking
                    if (CurrentStamina < 0) CurrentStamina = 0;
                }
                else
                {
                    isBlocking = false;
                    Log.Info("Block released or stamina depleted.");
                }
            }

            // Handle incoming Block Event (to start blocking)
            if (blockReceiver.TryReceive())
            {
                if (!isBlocking && CurrentStamina > 0) // Can only start blocking if not already blocking and has stamina
                {
                    isBlocking = true;
                    isRegeneratingStamina = false; // No regen when initiating block
                    Log.Info("Block Action Triggered: Started blocking.");
                    // PlayerAnimationController will handle visuals. Stamina drain handled above.
                }
                else if (isBlocking)
                {
                    // If block key is pressed again while already blocking, stop blocking (toggle behavior for press)
                    isBlocking = false;
                    Log.Info("Block Action Triggered: Stopped blocking (toggle).");
                }
                else
                {
                    Log.Info("Cannot block: Already blocking or no stamina.");
                }
            }


            // Handle Jump
            if (jumpEvent.TryReceive())
            {
                if (CurrentStamina >= JumpStaminaCost)
                {
                    CurrentStamina -= JumpStaminaCost;
                    isRegeneratingStamina = false;
                    OnJumpRequested();
                    Log.Info($"Jump Action Triggered. Stamina: {CurrentStamina}");
                }
                else
                {
                    Log.Info($"Jump failed. Insufficient stamina: {CurrentStamina}/{JumpStaminaCost}");
                }
            }

            // Handle Light Attack
            if (lightAttackReceiver.TryReceive())
            {
                if (CurrentStamina >= LightAttackStaminaCost)
                {
                    CurrentStamina -= LightAttackStaminaCost;
                    isRegeneratingStamina = false;
                    Log.Info($"Light Attack Action Triggered. Stamina: {CurrentStamina}");
                    // TODO: Trigger animation, deal damage, etc.
                }
                else
                {
                    Log.Info($"Light Attack failed. Insufficient stamina: {CurrentStamina}/{LightAttackStaminaCost}");
                }
            }

            // Handle Heavy Attack
            if (heavyAttackReceiver.TryReceive())
            {
                if (CurrentStamina >= HeavyAttackStaminaCost)
                {
                    CurrentStamina -= HeavyAttackStaminaCost;
                    isRegeneratingStamina = false;
                    Log.Info($"Heavy Attack Action Triggered. Stamina: {CurrentStamina}");
                    // TODO: Trigger animation, deal damage, etc.
                }
                else
                {
                    Log.Info($"Heavy Attack failed. Insufficient stamina: {CurrentStamina}/{HeavyAttackStaminaCost}");
                }
            }

            // Handle Dodge
            if (dodgeReceiver.TryReceive())
            {
                if (CurrentStamina >= DodgeStaminaCost)
                {
                    CurrentStamina -= DodgeStaminaCost;
                    isRegeneratingStamina = false;
                    Log.Info($"Dodge Action Triggered. Stamina: {CurrentStamina}");
                    // TODO: Perform dodge movement, trigger animation
                }
                else
                {
                    Log.Info($"Dodge failed. Insufficient stamina: {CurrentStamina}/{DodgeStaminaCost}");
                }
            }


            // Handle Melee Mode Toggle
            if (toggleMeleeModeReceiver.TryReceive())
            {
                CurrentMeleeMode = (CurrentMeleeMode == MeleeCombatMode.Standard) ? MeleeCombatMode.SoulsLike : MeleeCombatMode.Standard;
                Log.Info($"Melee mode switched to: {CurrentMeleeMode}");
                // If switching out of SoulsLike while locked on, disable lock-on
                if (CurrentMeleeMode == MeleeCombatMode.Standard && IsLockedOn)
                {
                    ClearLockOn();
                }
            }

            // Handle Lock-On Toggle
            if (toggleLockOnReceiver.TryReceive())
            {
                HandleLockOnToggle();
            }

            // Handle Switch Lock-On Target
            if (switchLockOnTargetReceiver.TryReceive(out int direction))
            {
                HandleSwitchTarget(direction);
            }

            // Validate current lock-on target
            if (IsLockedOn)
            {
                if (CurrentLockOnTarget == null || !CurrentLockOnTarget.Scene != null || // Check if entity is still valid (not removed from scene)
                    (CurrentLockOnTarget.Transform.WorldMatrix.TranslationVector - Entity.Transform.WorldMatrix.TranslationVector).LengthSquared() > LockOnMaxDistance * LockOnMaxDistance)
                {
                    ClearLockOn();
                }
            }

            Vector3 receivedMoveDirection = Vector3.Zero;
            bool hasMovementInput = moveDirectionEvent.TryReceive(out receivedMoveDirection);

            Move(receivedMoveDirection, hasMovementInput);

            // Stamina Regeneration
            if (isRegeneratingStamina && CurrentStamina < MaxStamina)
            {
                CurrentStamina += StaminaRegenRate * deltaTime;
                if (CurrentStamina > MaxStamina)
                {
                    CurrentStamina = MaxStamina;
                }
            }
        }

        private void OnJumpRequested()
        {
            character.Jump(Vector3.UnitY * JumpSpeed); 
        }

        private void HandleLockOnToggle()
        {
            if (IsLockedOn)
            {
                ClearLockOn();
            }
            else
            {
                if (CurrentMeleeMode == MeleeCombatMode.SoulsLike) // Only allow lock-on in SoulsLike mode
                {
                    AcquireTargetsAndLockOn();
                }
                else
                {
                    Log.Info("Lock-on is only available in SoulsLike melee mode.");
                }
            }
        }

        private void AcquireTargetsAndLockOn()
        {
            potentialTargets.Clear();
            currentTargetIndex = -1;
            CurrentLockOnTarget = null;

            // Define the sweep shape and parameters
            var sphereShape = new SphereColliderShape(LockOnSweepSphereRadius);
            Matrix startMatrix = Entity.Transform.WorldMatrix; 
            Vector3 sweepDirection = Entity.Transform.WorldMatrix.Forward; // Use camera's forward if player model doesn't rotate with camera pitch
            
            // If player model rotates independently of camera pitch (common), use Player's forward for sweep.
            // For now, assuming Entity.Transform.WorldMatrix.Forward is appropriate for player's facing direction for targeting.
            // Adjust if PlayerCamera directly controls player entity's yaw.
            
            // Perform the sweep
            // Note: ShapeSweep's 'to' parameter is a delta movement, not a world position.
            // So, we sweep from 'startMatrix' by 'sweepDirection * LockOnMaxDistance'.
            var sweepEndTranslation = sweepDirection * LockOnMaxDistance;
            // The 'to' parameter in ShapeSweep is relative to the 'startMatrix'
            // So, to sweep from startMatrix to startMatrix + sweepEndTranslation, 'to' should be sweepEndTranslation.
            // No, the 'to' parameter is the end world matrix for the shape.
            // Let's construct the end matrix:
            Matrix endMatrix = Matrix.Translation(startMatrix.TranslationVector + sweepEndTranslation) * Matrix.Rotation(startMatrix.Rotation);


            // Using HitResult list for ShapeSweep
            List<HitResult> sweepResults = new List<HitResult>();
            // Sweep from player's current position/orientation forward.
            // The 'to' matrix should represent the end position and orientation of the *shape*, not the character.
            // For a simple forward sweep, we can just translate the start matrix.
            Matrix sweepToMatrix = Matrix.Translation(Entity.Transform.Position + Entity.Transform.WorldMatrix.Forward * LockOnMaxDistance) * Entity.Transform.Rotation;


            this.simulation.ShapeSweep(sphereShape, startMatrix, sweepToMatrix, sweepResults, LockOnTargetFilterGroup);

            var validTargets = new List<Entity>();
            foreach (var hitResult in sweepResults)
            {
                var targetEntity = hitResult.Collider.Entity;
                if (targetEntity != Entity && // Not self
                    targetEntity.Get<TargetableComponent>() != null &&
                    !validTargets.Contains(targetEntity)) // Avoid duplicates
                {
                    float distanceSquared = (targetEntity.Transform.WorldMatrix.TranslationVector - Entity.Transform.WorldMatrix.TranslationVector).LengthSquared();
                    if (distanceSquared <= LockOnMaxDistance * LockOnMaxDistance)
                    {
                        validTargets.Add(targetEntity);
                    }
                }
            }

            if (validTargets.Any())
            {
                // Sort by distance (closest first)
                potentialTargets = validTargets.OrderBy(t => 
                    (t.Transform.WorldMatrix.TranslationVector - Entity.Transform.WorldMatrix.TranslationVector).LengthSquared()
                ).ToList();

                currentTargetIndex = 0;
                CurrentLockOnTarget = potentialTargets[currentTargetIndex];
                Log.Info($"Locked on to: {CurrentLockOnTarget.Name}");
            }
            else
            {
                Log.Info("No targets found for lock-on.");
                ClearLockOn();
            }
        }
        
        private void HandleSwitchTarget(int direction)
        {
            if (!IsLockedOn || potentialTargets.Count <= 1)
            {
                // Re-acquire if list is empty but still locked on (shouldn't happen if validation is correct)
                if (potentialTargets.Count == 0 && IsLockedOn) AcquireTargetsAndLockOn(); 
                if (potentialTargets.Count <=1) return; // Still no targets or only one
            }

            currentTargetIndex = (currentTargetIndex + direction + potentialTargets.Count) % potentialTargets.Count;
            CurrentLockOnTarget = potentialTargets[currentTargetIndex];
            Log.Info($"Switched lock-on target to: {CurrentLockOnTarget.Name}");
        }

        private void ClearLockOn()
        {
            CurrentLockOnTarget = null;
            currentTargetIndex = -1;
            potentialTargets.Clear();
            Log.Info("Lock-on cleared.");
        }

        private void Move(Vector3 inputMoveDirection, bool hasMovementInput)
        {
            if (IsLockedOn && CurrentMeleeMode == MeleeCombatMode.SoulsLike && CurrentLockOnTarget != null)
            {
                var targetComponent = CurrentLockOnTarget.Get<TargetableComponent>();
                if (targetComponent == null) // Target might have lost its component
                {
                    ClearLockOn();
                    // Fallback to standard movement for this frame
                    DefaultMovement(inputMoveDirection, hasMovementInput);
                    return;
                }
                Vector3 targetLockOnPoint = targetComponent.GetWorldLockOnPoint();
                Vector3 directionToTarget = targetLockOnPoint - Entity.Transform.Position;
                directionToTarget.Y = 0; // Keep movement and rotation on the horizontal plane
                if (directionToTarget.LengthSquared() > MathUtil.ZeroTolerance) // Avoid normalization of zero vector
                {
                    directionToTarget.Normalize();
                    Entity.Transform.Rotation = Quaternion.LookRotation(directionToTarget, Vector3.UnitY);
                }

                // Reorient camera-relative input to be target-relative for strafing/orbiting
                // Player's "forward" is now always towards the target.
                Vector3 playerForward = directionToTarget;
                Vector3 playerRight = Vector3.Cross(Vector3.UnitY, playerForward); // Get player's right vector

                // inputMoveDirection.Z is camera's forward/backward, inputMoveDirection.X is camera's right/left
                // We want to map these to player's forward/backward (towards/away from target) and right/left (strafe)
                // This requires transforming the camera-relative input vector into world space, then projecting onto player's local axes.
                // However, PlayerInput already gives worldSpeed which is camera-relative movement in world space.
                // So, inputMoveDirection is already the intended world-space direction IF NOT LOCKED ON.
                // When locked on, we need to interpret its components differently.
                
                // If inputMoveDirection from PlayerInput is already world-space (based on camera),
                // its Z component would be "world forward from camera" and X "world right from camera".
                // We need to project this onto player's new forward (directionToTarget) and right.
                // A simpler approach: inputMoveDirection comes from PlayerInput, which is based on camera.
                // Let's assume inputMoveDirection.Y is "forward" from camera, inputMoveDirection.X is "right" from camera.
                // (PlayerInput maps W/S to Y, A/D to X, then LogicDirectionToWorldDirection converts this)
                // The 'inputMoveDirection' from PlayerInput is already a world vector.
                // We need to decide if 'W' moves towards target or still "camera forward".
                // For typical Souls-like, 'W' moves player towards where camera is pointing, but player faces target.
                // This is complex. Let's use the provided simpler model for now:
                // Z from input (W/S) moves along playerForward (towards/away from target)
                // X from input (A/D) moves along playerRight (strafing)
                
                // The inputMoveDirection from PlayerInput is (X, 0, Z) in world space relative to camera.
                // Let's re-interpret: input.Z for forward/backward, input.X for strafe.
                // The 'inputMoveDirection' given by PlayerInput is already a world vector based on camera view.
                // We need to decompose its magnitude and reapply it.
                float inputMagnitude = inputMoveDirection.Length(); // Original input strength
                if (inputMagnitude < MathUtil.ZeroTolerance) // No input
                {
                     character.SetVelocity(Vector3.Zero);
                     RunSpeedEventKey.Broadcast(0f);
                }
                else
                {
                    // If player presses 'W' (forward relative to camera), inputMoveDirection points camera's forward.
                    // We want the player to move, but keep facing target.
                    // The movement direction should be relative to camera's yaw, but player faces target.
                    // Get camera's current yaw rotation
                    var cameraEntity = PlayerCameraEntity ?? SceneSystem.Default.GraphicsCompositor.Cameras.FirstOrDefault()?.SceneCamera.Entity;
                    if (cameraEntity == null)
                    {
                        // Fallback or log error if no camera found
                        Log.Error("No camera entity found for locked-on movement calculation.");
                        DefaultMovement(inputMoveDirection, hasMovementInput); // Fallback to default
                        return;
                    }
                    // A better way: pass camera reference or get it from PlayerCamera script later.
                    // For now, let's assume a simplified strafing based on the input vector's components if it was local.
                    // This needs the original local input vector before camera transformation.
                    // Let's assume the PlayerInput.MoveDirectionEventKey sends a local camera-space Vector2 (X,Y) instead of world Vector3.
                    // If PlayerInput sends world vector:
                    Vector3 desiredVelocity = inputMoveDirection * MaxRunSpeed; // Movement is still camera-relative for direction, player faces target.
                    character.SetVelocity(desiredVelocity);
                    RunSpeedEventKey.Broadcast(inputMagnitude);
                }

            }
            else
            {
                DefaultMovement(inputMoveDirection, hasMovementInput);
            }
        }

        private void DefaultMovement(Vector3 inputMoveDirection, bool hasMovementInput)
        {
            if (hasMovementInput)
            {
                // The moveDirection received from PlayerInput is already world-space relative to camera
                character.SetVelocity(inputMoveDirection * MaxRunSpeed);

                // Broadcast normalized speed (magnitude of the input vector, could be > 1 if diagonal on keyboard)
                RunSpeedEventKey.Broadcast(inputMoveDirection.Length());
            }
            else
            {
                // Optional: If no movement input, explicitly set velocity to zero or apply damping
                character.SetVelocity(Vector3.Zero); // Or character.SetVelocity(character.LinearVelocity * dampingFactor);
                RunSpeedEventKey.Broadcast(0f);
            }
        }

        // MODIFIED: Removed ITargetable Implementation
        /*
        #region ITargetable Implementation
        public Vector3 GetTargetPosition()
        {
            // Return a point roughly in the center of the character model
            return Entity.Transform.Position + new Vector3(0, 1.0f, 0); // Assuming player origin is at feet, target is 1m up.
        }

        public Entity GetEntity()
        {
            return this.Entity;
        }
        #endregion
        */
    }
}
