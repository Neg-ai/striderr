// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using System;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Physics;
using MySurvivalGame.Game.Player; // Required for PlayerLockOnManager
using MySurvivalGame.Game.Combat; // Required for TargetableComponent

namespace MySurvivalGame.Game // MODIFIED: Namespace updated
{
    public class PlayerController : SyncScript
    {
        [Display("Run Speed")]
        public float MaxRunSpeed { get; set; } = 5;

        public static readonly EventKey<float> RunSpeedEventKey = new EventKey<float>(); // This can be used by an animation controller later

        // This component is the physics representation of a controllable character
        private CharacterComponent character;

        // Correctly references PlayerInput from MySurvivalGame.Game namespace
        private readonly EventReceiver<Vector3> moveDirectionEvent = new EventReceiver<Vector3>(PlayerInput.MoveDirectionEventKey);
        private readonly EventReceiver jumpEvent = new EventReceiver(PlayerInput.JumpEventKey);

        // Lock-on state
        private Entity lockedTarget;
        private EventReceiver<Entity> targetLockedListener;
        private EventReceiver targetUnlockedListener;
        // private PlayerLockOnManager playerLockOnManager; // Not strictly needed if only accessing static events

        public bool IsMoving => character != null && character.LinearVelocity.LengthSquared() > 0.01f; // ADDED: IsMoving property


        /// <summary>
        /// Called when the script is first initialized
        /// </summary>
        public override void Start()
        {
            base.Start();

            // Will search for an CharacterComponent within the same entity as this script
            character = Entity.Get<CharacterComponent>();
            if (character == null) throw new ArgumentException("Please add a CharacterComponent to the entity containing PlayerController!");

            // Subscribe to lock-on events
            // Note: PlayerLockOnManager must be on an entity and its events broadcast correctly.
            // Assuming PlayerLockOnManager is on the same Player entity or its events are globally accessible.
            targetLockedListener = new EventReceiver<Entity>(PlayerLockOnManager.TargetLockedEvent, HandleTargetLocked);
            targetUnlockedListener = new EventReceiver(PlayerLockOnManager.TargetUnlockedEvent, HandleTargetUnlocked);
            
            // Optionally, try to get the PlayerLockOnManager if direct interaction is needed later
            // playerLockOnManager = Entity.Get<PlayerLockOnManager>();
        }

        public override void Cancel()
        {
            targetLockedListener?.Dispose();
            targetUnlockedListener?.Dispose();
            base.Cancel();
        }

        private void HandleTargetLocked(Entity target)
        {
            Log.Info("PlayerController: Target locked.");
            lockedTarget = target;
        }

        private void HandleTargetUnlocked()
        {
            Log.Info("PlayerController: Target unlocked.");
            lockedTarget = null;
        }
        
        /// <summary>
        /// Called on every frame update
        /// </summary>
        public override void Update()
        {
            HandleJump(); // ADDED: Handle jump input
            Move();
        }

        private void HandleJump() // ADDED: Method to handle jump
        {
            if (jumpEvent.TryReceive())
            {
                character?.Jump(); // Use null-conditional operator for safety, though character should be set in Start
            }
        }

        private void Move()
        {
            // Process lock-on events first
            targetLockedListener?.TryReceive();
            targetUnlockedListener?.TryReceive();

            Vector3 currentMoveInput = Vector3.Zero; // This will hold the camera-relative input from PlayerInput
            bool hasMoveInput = moveDirectionEvent.TryReceive(out currentMoveInput);

            if (lockedTarget != null && character != null)
            {
                var targetable = lockedTarget.Get<TargetableComponent>();
                if (targetable != null && targetable.IsTargetable)
                {
                    Vector3 playerPosition = Entity.Transform.WorldMatrix.TranslationVector;
                    Vector3 targetPosition = targetable.GetWorldLockOnPoint(); // Using the precise lock-on point

                    // Direction from player to target, ignoring Y for planar movement
                    Vector3 directionToTarget = targetPosition - playerPosition;
                    directionToTarget.Y = 0; // Project onto XZ plane
                    directionToTarget.Normalize();

                    // Player's right vector, perpendicular to directionToTarget
                    Vector3 playerRight = Vector3.Cross(Vector3.UnitY, directionToTarget);
                    playerRight.Normalize(); // Ensure it's a unit vector

                    // Deconstruct the camera-relative input `currentMoveInput`
                    // `currentMoveInput` is already scaled by PlayerInput's logic (e.g. keyboard gives unit length vectors, gamepad thumbstick is normalized)
                    // It represents where the player *wants* to go relative to the camera.
                    // We need to project this intent onto the target-relative axes.

                    float forwardIntent = 0f;
                    float strafeIntent = 0f;

                    if (hasMoveInput && currentMoveInput.LengthSquared() > 0.01f) // Check if there's significant input
                    {
                        // The `currentMoveInput` is world-space direction based on camera.
                        // To make it target-relative:
                        // Forward movement is how much of `currentMoveInput` aligns with `directionToTarget`.
                        // Strafe movement is how much of `currentMoveInput` aligns with `playerRight`.
                        
                        // This reinterpretation of camera-relative input to target-relative can be tricky.
                        // A simpler model: use raw player input (e.g. from PlayerInput if it provided a Vector2)
                        // For now, we assume PlayerInput's `currentMoveInput` (Vector3) Z is forward, X is strafe relative to camera.
                        // We need to map this to target-relative movement.

                        // Get the player's intended direction magnitude
                        float inputMagnitude = currentMoveInput.Length();

                        // Project the camera-relative input vector onto the target-relative axes
                        // This interprets the camera-relative input in the context of the target
                        forwardIntent = Vector3.Dot(currentMoveInput, directionToTarget);
                        strafeIntent = Vector3.Dot(currentMoveInput, playerRight);
                        
                        // Preserve the original input's magnitude for speed consistency
                        Vector3 combinedIntent = new Vector3(strafeIntent, 0, forwardIntent);
                        if (combinedIntent.LengthSquared() > 0.01f)
                        {
                           combinedIntent.Normalize(); // Normalize the projected intent
                           combinedIntent *= inputMagnitude; // Reapply original magnitude
                           forwardIntent = combinedIntent.Z;
                           strafeIntent = combinedIntent.X;
                        }
                        else // If projection results in near-zero (e.g. input is perpendicular to both axes)
                        {
                            forwardIntent = 0f;
                            strafeIntent = 0f;
                        }
                    }
                    
                    Vector3 finalVelocity = (directionToTarget * forwardIntent + playerRight * strafeIntent) * MaxRunSpeed;
                    finalVelocity.Y = character.LinearVelocity.Y; // Preserve current vertical velocity (e.g. from jumping/gravity)
                    character.SetVelocity(finalVelocity);
                    
                    RunSpeedEventKey.Broadcast(new Vector2(strafeIntent, forwardIntent).Length());
                }
                else
                {
                    // Locked target is no longer valid (e.g. destroyed)
                    lockedTarget = null; // Clear local reference, PlayerLockOnManager will handle global
                    MoveFreely(currentMoveInput, hasMoveInput); // Fallback to normal movement
                }
            }
            else
            {
                MoveFreely(currentMoveInput, hasMoveInput);
            }
        }

        private void MoveFreely(Vector3 moveInput, bool hasMoveInput)
        {
            if (hasMoveInput)
            {
                // moveInput is already world-space relative to camera
                Vector3 currentVelocity = character.LinearVelocity;
                character.SetVelocity(new Vector3(moveInput.X * MaxRunSpeed, currentVelocity.Y, moveInput.Z * MaxRunSpeed));
                RunSpeedEventKey.Broadcast(moveInput.Length());
            }
            else
            {
                character.SetVelocity(new Vector3(0, character.LinearVelocity.Y, 0)); // Preserve Y for gravity/jump
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
