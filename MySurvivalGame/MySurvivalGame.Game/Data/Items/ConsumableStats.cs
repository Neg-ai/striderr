using Stride.Core; // For DataContract and DataMember

namespace MySurvivalGame.Data.Items
{
    [DataContract]
    public class ConsumableStats
    {
        /// <summary>
        /// Amount of health restored when consumed. Can be negative for damaging consumables.
        /// </summary>
        [DataMember]
        public float HealthChange { get; set; } = 0f;

        /// <summary>
        /// Amount of hunger restored when consumed. Can be negative.
        /// </summary>
        [DataMember]
        public float HungerChange { get; set; } = 0f;

        /// <summary>
        /// Amount of thirst restored when consumed. Can be negative.
        /// </summary>
        [DataMember]
        public float ThirstChange { get; set; } = 0f;

        /// <summary>
        /// Amount of stamina restored or changed when consumed. Can be negative.
        /// </summary>
        [DataMember]
        public float StaminaChange { get; set; } = 0f;

        /// <summary>
        /// Duration of any temporary effects (buffs/debuffs) in seconds. 
        /// 0 if the effect is instant.
        /// </summary>
        [DataMember]
        public float EffectDuration { get; set; } = 0f;

        /// <summary>
        /// Name or ID of a status effect to apply (e.g., "Poisoned", "WellFed", "SpeedBoost").
        /// This would likely correspond to a separate system that handles status effect logic.
        /// </summary>
        [DataMember]
        public string StatusEffectID { get; set; }

        /// <summary>
        /// Potency or magnitude of the status effect, if applicable.
        /// </summary>
        [DataMember]
        public float StatusEffectPotency { get; set; } = 0f;
    }
}
