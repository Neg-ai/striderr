using Stride.Core; // For DataContract and DataMember

namespace MySurvivalGame.Game.Data.Items
{
    [DataContract]
    public class WeaponStats
    {
        /// <summary>
        /// Damage per projectile. For weapons that fire a single projectile, this is the total damage.
        /// For weapons like shotguns, this is the damage of one pellet.
        /// </summary>
        [DataMember]
        public float Damage { get; set; }

        /// <summary>
        /// Number of projectiles fired in a single shot. Defaults to 1.
        /// Example: A shotgun might fire 8 projectiles.
        /// </summary>
        [DataMember]
        public int ProjectileCount { get; set; } = 1;

        /// <summary>
        /// Attacks per second.
        /// </summary>
        [DataMember]
        public float FireRate { get; set; }

        /// <summary>
        /// Effective range of the weapon in meters.
        /// For melee weapons, this could be a short distance (e.g., 1.5f or 2.0f).
        /// </summary>
        [DataMember]
        public float Range { get; set; }

        /// <summary>
        /// ItemID of the ammunition required for this weapon.
        /// Null or empty if no ammunition is required (e.g., melee weapons).
        /// </summary>
        [DataMember]
        public string RequiredAmmoItemID { get; set; }

        /// <summary>
        /// Maximum ammunition capacity of the weapon's clip or magazine.
        /// 0 if the weapon does not use a clip (e.g., some shotguns, melee).
        /// </summary>
        [DataMember]
        public int ClipSize { get; set; } = 0;

        /// <summary>
        /// Describes any special properties of the weapon (e.g., "High recoil", "Area mining", "AOE, shield dmg", "Piercing").
        /// </summary>
        [DataMember]
        public string SpecialNotes { get; set; }

        // It might be useful to add more specific structured properties later,
        // such as enums for damage types (e.g., Piercing, Explosive, Energy)
        // or flags for capabilities (e.g., FullAuto, ScopeAvailable).
        // For now, SpecialNotes covers these.
    }
}
