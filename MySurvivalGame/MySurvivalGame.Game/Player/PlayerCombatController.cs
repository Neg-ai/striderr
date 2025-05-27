// Copyright (c) My Survival Game. All rights reserved.
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;
using Stride.Core.Mathematics;
using Stride.Input; // For PlayerInput event keys
using Stride.Engine.Events; // For EventReceivers
// using MySurvivalGame.Game.Combat; // For TargetableComponent, if needed directly later
// using MySurvivalGame.Game.Animations; // If a separate PlayerAnimationController exists

namespace MySurvivalGame.Game.Player
{
    public enum PlayerState
    {
        Idle,
        Moving,
        LockedOnIdle,
        LockedOnMoving,
        // Future states: Attacking, Dodging, Blocking, HitStun, Dead
    }

    public enum MeleeMode // ADDED: MeleeMode Enum
    {
        Standard,  // e.g., Ark-like
        SoulsLike  // Precision melee
    }

    public class PlayerCombatController : SyncScript
    {
        // --- Component References (to be assigned in Start or from editor) ---
        public PlayerController PlayerController { get; set; }
        public PlayerInput PlayerInputComponent { get; set; } // If direct access needed beyond static events
        public AnimationComponent AnimationComp { get; set; }
        public PlayerLockOnManager LockOnManager { get; set; }
        // public PlayerAnimationController PlayerAnimationController { get; set; } // Alternative
        // public StaminaComponent StaminaComponent { get; set; } // To be created

        // --- State ---
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
        public MeleeMode CurrentMeleeMode { get; private set; } = MeleeMode.Standard; // ADDED: Current Melee Mode Property

        // --- Event Receivers ---
        // Example: private EventReceiver lightAttackListener;
        private EventReceiver toggleMeleeModeListener; // ADDED: EventReceiver for melee mode toggle

        public override void Start()
        {
            // Attempt to get components from the same entity if not set in editor
            PlayerController = Entity.Get<PlayerController>();
            PlayerInputComponent = Entity.Get<PlayerInput>(); // Often not needed if events are static
            AnimationComp = Entity.Get<AnimationComponent>();
            LockOnManager = Entity.Get<PlayerLockOnManager>();
            
            // If AnimationComp is on a child (e.g., "PlayerModel"), use:
            // var modelEntity = Entity.FindChild("PlayerModel"); // Or your model entity name
            // if (modelEntity != null) AnimationComp = modelEntity.Get<AnimationComponent>();

            // StaminaComponent = Entity.Get<StaminaComponent>(); // When StaminaComponent exists

            if (PlayerController == null) Log.Error("PlayerCombatController: PlayerController component not found on entity.");
            if (LockOnManager == null) Log.Error("PlayerCombatController: PlayerLockOnManager component not found on entity.");


            // Subscribe to input events
            // Example:
            // if (PlayerInput != null) // Or check static event key availability
            // {
            //    lightAttackListener = new EventReceiver(PlayerInput.LightAttackEventKey, HandleLightAttack);
            // }
            toggleMeleeModeListener = new EventReceiver(PlayerInput.ToggleMeleeModeEventKey, HandleToggleMeleeMode); // ADDED: Subscription

            Log.Info("PlayerCombatController started.");
        }

        public override void Update()
        {
            // Process event listeners
            // Example: lightAttackListener?.TryReceive();
            toggleMeleeModeListener?.TryReceive(); // ADDED: Process toggle melee mode event

            UpdatePlayerState();

            // Combat logic will go here in future tasks
            
            // Log.Info($"Player State: {CurrentState}");
        }

        private void HandleToggleMeleeMode() // ADDED: Handler for toggling melee mode
        {
            if (CurrentMeleeMode == MeleeMode.Standard)
            {
                CurrentMeleeMode = MeleeMode.SoulsLike;
            }
            else
            {
                CurrentMeleeMode = MeleeMode.Standard;
            }
            Log.Info($"Melee Mode Changed to: {CurrentMeleeMode}");
        }

        private void UpdatePlayerState()
        {
            if (PlayerController == null || LockOnManager == null)
            {
                // Not fully initialized, or components are missing. Avoid state changes.
                // Potentially set to a default or error state if appropriate.
                return;
            }

            bool isMoving = PlayerController.IsMoving; // Uses the new property from PlayerController

            if (LockOnManager.CurrentTarget != null)
            {
                if (isMoving)
                {
                    CurrentState = PlayerState.LockedOnMoving;
                }
                else
                {
                    CurrentState = PlayerState.LockedOnIdle;
                }
            }
            else // Not locked on
            {
                if (isMoving)
                {
                    CurrentState = PlayerState.Moving;
                }
                else
                {
                    CurrentState = PlayerState.Idle;
                }
            }
        }

        // Example event handler
        // private void HandleLightAttack()
        // {
        //     if (StaminaComponent != null && StaminaComponent.CurrentStamina > 10) // Example stamina check
        //     {
        //         Log.Info("Light attack initiated!");
        //         // Trigger animation, deduct stamina, raycast for hit, etc.
        //         // StaminaComponent.ConsumeStamina(10);
        //         // AnimationComp?.Play("LightAttack_AnimName");
        //     }
        //     else
        //     {
        //         Log.Info("Not enough stamina for light attack or StaminaComponent missing.");
        //     }
        // }

        public override void Cancel()
        {
            // Dispose event listeners
            // Example: lightAttackListener?.Dispose();
            toggleMeleeModeListener?.Dispose(); // ADDED: Dispose toggle melee mode listener
            base.Cancel();
        }
    }
}
