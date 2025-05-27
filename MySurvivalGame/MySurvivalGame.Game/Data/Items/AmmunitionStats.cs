using Stride.Core; // For DataContract and DataMember

namespace MySurvivalGame.Data.Items
{
    [DataContract]
    public class AmmunitionStats
    {
        public enum AmmoDamageType
        {
            Kinetic,      // Standard bullets, arrows
            Explosive,    // Grenades, rockets
            Energy,       // Plasma, laser
            Incendiary,   // Fire-based
            Special       // For unique effects not covered above
        }

        /// <summary>
        /// The type of damage this ammunition primarily deals.
        /// </summary>
        [DataMember]
        public AmmoDamageType DamageType { get; set; } = AmmoDamageType.Kinetic;

        /// <summary>
        /// For ammunition types like shotgun shells, this indicates how many projectiles are fired.
        /// For most bullets/arrows, this would be 1.
        /// </summary>
        [DataMember]
        public int ProjectileCount { get; set; } = 1;

        /// <summary>
        /// An additional damage modifier applied by the ammo itself (e.g. +5 for armor-piercing rounds).
        /// This is separate from the weapon's base damage.
        /// </summary>
        [DataMember]
        public float DamageModifier { get; set; } = 0f;
        
        /// <summary>
        /// Describes any special properties of the ammunition (e.g., "Tracer", "Armor Piercing", "Ricochet").
        /// </summary>
        [DataMember]
        public string SpecialNotes { get; set; }
    }
}
