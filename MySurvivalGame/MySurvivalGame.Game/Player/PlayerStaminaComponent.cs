using Stride.Engine;
using Stride.Core; // For [DataMember]
using System; // For Action

namespace MySurvivalGame.Game.Player // Or MySurvivalGame.Game.Combat
{
    public class PlayerStaminaComponent : SyncScript // Using SyncScript for Update method
    {
        [DataMember(0)]
        public float MaxStamina { get; set; } = 100f;

        private float _currentStamina;
        [DataMember(1)] // Allow setting in editor for testing, but primarily managed by script
        public float CurrentStamina
        {
            get => _currentStamina;
            set
            {
                float previousStamina = _currentStamina;
                _currentStamina = Math.Clamp(value, 0f, MaxStamina);

                // If value changed, invoke event
                if (Math.Abs(previousStamina - _currentStamina) > float.Epsilon)
                {
                    OnStaminaChanged?.Invoke(_currentStamina, MaxStamina);
                }
            }
        }

        [DataMember(2)]
        public float StaminaRegenRate { get; set; } = 10f; // Stamina points per second

        [DataMember(3)]
        public float StaminaRegenDelay { get; set; } = 2.0f; // Seconds after stamina usage before regen starts

        // Conceptual property for stamina cost of running, not directly used in this component's Update loop
        // but can be referenced by PlayerController.
        [DataMember(4)]
        public float StaminaDrainRateRun { get; set; } = 5f; // Stamina points per second while running

        [DataMember(5)]
        public bool CanRegenerate { get; set; } = true;

        // Internal timer for regeneration delay
        internal float timeSinceLastStaminaUse = 0f; // internal to allow PlayerEquipment to reset it if needed, or private

        // Events
        public event Action<float, float> OnStaminaChanged; // currentStamina, maxStamina
        public event Action OnStaminaDepleted;
        public event Action OnStaminaAvailable; // Fired when stamina regenerates from a depleted state

        public override void Start()
        {
            // Initialize CurrentStamina to MaxStamina when the component starts
            // and ensure OnStaminaChanged is invoked for initial UI setup.
            _currentStamina = MaxStamina; // Set private field directly to ensure event fires if MaxStamina is also initial value
            CurrentStamina = MaxStamina;  // Then set public property to trigger setter logic and event
            Log.Info($"PlayerStaminaComponent Started: MaxStamina={MaxStamina}, CurrentStamina={CurrentStamina}");
        }

        public override void Update()
        {
            float deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;

            if (CanRegenerate && CurrentStamina < MaxStamina)
            {
                timeSinceLastStaminaUse += deltaTime;

                if (timeSinceLastStaminaUse >= StaminaRegenDelay)
                {
                    bool wasDepleted = CurrentStamina <= 0; // Check before regeneration

                    CurrentStamina += StaminaRegenRate * deltaTime;
                    // CurrentStamina setter already clamps to MaxStamina and invokes OnStaminaChanged.

                    if (wasDepleted && CurrentStamina > 0)
                    {
                        OnStaminaAvailable?.Invoke();
                        Log.Info("Stamina: Now available after being depleted.");
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to consume a specified amount of stamina.
        /// </summary>
        /// <param name="amountToConsume">The amount of stamina to consume. Must be positive.</param>
        /// <returns>True if stamina was sufficient and consumed, false otherwise.</returns>
        public bool TryConsumeStamina(float amountToConsume)
        {
            if (amountToConsume <= 0) // Cannot consume zero or negative stamina
            {
                // Optional: Log a warning if this case is not expected.
                // Log.Warning("TryConsumeStamina called with non-positive amount.");
                return true; // Technically, zero cost is always "afforded".
            }

            if (CurrentStamina >= amountToConsume)
            {
                float oldStamina = CurrentStamina;
                CurrentStamina -= amountToConsume; // Setter handles OnStaminaChanged
                timeSinceLastStaminaUse = 0f;     // Reset regeneration delay timer

                Log.Info($"Stamina: Consumed {amountToConsume}. Old: {oldStamina}, New: {CurrentStamina}");

                if (CurrentStamina <= 0)
                {
                    OnStaminaDepleted?.Invoke();
                    Log.Info("Stamina: Depleted.");
                }
                return true;
            }
            else
            {
                Log.Info($"Stamina: Insufficient stamina. Need: {amountToConsume}, Have: {CurrentStamina}.");
                return false; // Not enough stamina
            }
        }
    }
}
