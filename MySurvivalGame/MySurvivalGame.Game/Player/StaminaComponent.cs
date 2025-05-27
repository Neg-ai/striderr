// Copyright (c) My Survival Game. All rights reserved.
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Core; // Required for [Display] attribute
using Stride.Engine;
using Stride.Engine.Events;

namespace MySurvivalGame.Game.Player
{
    /// <summary>
    /// Manages the stamina for an entity, including regeneration and consumption.
    /// </summary>
    public class StaminaComponent : SyncScript
    {
        // --- Properties ---
        [Display("Max Stamina")]
        public float MaxStamina { get; set; } = 100.0f;

        [Display("Current Stamina")]
        public float CurrentStamina { get; private set; }

        [Display("Stamina Regeneration Rate (units/sec)")]
        public float StaminaRegenerationRate { get; set; } = 10.0f;

        [Display("Stamina Regen Delay (seconds)")]
        public float StaminaRegenDelay { get; set; } = 2.0f;
        
        [Display("Allow Regeneration")]
        public bool AllowRegeneration { get; set; } = true;

        // --- Internal State ---
        private float timeSinceLastStaminaUse = 0.0f;

        // --- Event ---
        /// <summary>
        /// Broadcasts the new stamina percentage (0.0 to 1.0) when stamina changes.
        /// </summary>
        public static readonly EventKey<float> StaminaChangedEvent = new EventKey<float>();

        public override void Start()
        {
            CurrentStamina = MaxStamina;
            timeSinceLastStaminaUse = StaminaRegenDelay; // Allow regen immediately if full at start
            Log.Info($"StaminaComponent started. Initial Stamina: {CurrentStamina}/{MaxStamina}");
            StaminaChangedEvent.Broadcast(CurrentStamina / MaxStamina);
        }

        public override void Update()
        {
            float previousStamina = CurrentStamina;

            timeSinceLastStaminaUse += (float)Game.UpdateTime.Elapsed.TotalSeconds;

            if (AllowRegeneration && CurrentStamina < MaxStamina && timeSinceLastStaminaUse >= StaminaRegenDelay)
            {
                CurrentStamina += StaminaRegenerationRate * (float)Game.UpdateTime.Elapsed.TotalSeconds;
                CurrentStamina = Math.Min(CurrentStamina, MaxStamina); // Clamp to max
            }

            if (Math.Abs(CurrentStamina - previousStamina) > 0.001f) // Check if stamina actually changed
            {
                StaminaChangedEvent.Broadcast(CurrentStamina / MaxStamina);
                // Log.Info($"Stamina updated: {CurrentStamina}/{MaxStamina}"); // Optional: for debugging
            }
        }

        /// <summary>
        /// Attempts to consume a specified amount of stamina.
        /// </summary>
        /// <param name="amount">The amount of stamina to consume.</param>
        /// <returns>True if stamina was consumed, false otherwise.</returns>
        public bool TryConsumeStamina(float amount)
        {
            if (amount <= 0) // Cannot consume zero or negative stamina
                return true; // Technically successful as no stamina needed to be consumed

            if (CurrentStamina >= amount)
            {
                CurrentStamina -= amount;
                timeSinceLastStaminaUse = 0.0f; // Reset regen delay timer
                StaminaChangedEvent.Broadcast(CurrentStamina / MaxStamina);
                Log.Info($"Consumed {amount} stamina. Current: {CurrentStamina}/{MaxStamina}");
                return true;
            }
            else
            {
                Log.Info($"Not enough stamina to consume {amount}. Current: {CurrentStamina}/{MaxStamina}");
                return false; 
            }
        }
    }
}
