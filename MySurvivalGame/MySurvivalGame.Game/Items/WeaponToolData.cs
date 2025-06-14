using Stride.Graphics; // For Texture, if base class needs it directly
using MySurvivalGame.Game.Items; // For MockInventoryItem and EquipmentType

namespace MySurvivalGame.Game.Items
{
    public enum SpecialBonusType // Could be expanded (e.g., specific enemy types, etc.)
    {
        None,
        Mining,
        Woodcutting,
        Combat // General combat bonus, or could be more specific
    }

    public class WeaponToolData : MockInventoryItem
    {
        public float Damage { get; set; }
        public float FireRate { get; set; } // Shots or swings per second
        public float Range { get; set; } // Effective range in game units (e.g., meters)
        public SpecialBonusType BonusType { get; set; }
        public float DurabilityPoints { get; set; }
        public float MaxDurabilityPoints { get; set; }
        public bool IsBroken { get; private set; } 

        // Ammo properties - relevant if CurrentEquipmentType is a ranged weapon
        public int ClipSize { get; set; } = 0; // Standard clip capacity for this weapon type
        public int CurrentAmmoInClip_Persisted { get; set; } = 0; // Ammo in clip when unequipped
        public int ReserveAmmo_Persisted { get; set; } = 0;     // Reserve ammo when unequipped
        public string RequiredAmmoName { get; set; } = string.Empty; // ADDED: E.g., "Arrow", "9mm Bullet"
        public float ProjectileSpeed { get; set; } = 0f; // ADDED: For weapons that fire projectiles

        // Properties for throwables/explosives
        public float FuseTime { get; set; } = 3.0f;
        public float ThrowForce { get; set; } = 15.0f;
        public float ExplosionDamage { get; set; } = 100f;
        public float AoeRadius { get; set; } = 5.0f;

        // Constructor
        public WeaponToolData(
            string name, 
            string itemType, // General type like "Axe", "Pistol"
            string description, 
            EquipmentType equipmentType, // Specific like Weapon, Tool
            float damage,
            float fireRate,
            float range,
            float maxDurability,
            SpecialBonusType bonusType = SpecialBonusType.None,
            Texture icon = null, 
            int quantity = 1, 
            int maxStackSize = 1, 
            float? initialDurability = null,
            // Ammo related parameters - only relevant for certain types
            int clipSize = 0, 
            int currentAmmoInClipPersisted = 0, 
            int reserveAmmoPersisted = 0,
            string requiredAmmoName = "",
            float weight = 0.1f,
            float projectileSpeed = 0f,
            // Throwable/Explosive specific parameters
            bool isThrowable = false, // Passed to base MockInventoryItem
            float fuseTime = 3.0f,
            float throwForce = 15.0f,
            float explosionDamage = 0f, // Default to 0 if not explosive
            float aoeRadius = 0f      // Default to 0 if not explosive
        ) : base(name, itemType, description, icon, quantity, (initialDurability ?? maxDurability) / maxDurability, maxStackSize, equipmentType, weight, isThrowable)
        {
            Damage = damage; // For direct hit damage if applicable, or base for explosion if not overridden
            FireRate = fireRate;
            Range = range;
            BonusType = bonusType;
            MaxDurabilityPoints = maxDurability;
            DurabilityPoints = initialDurability ?? maxDurability; 
            
            if (equipmentType == EquipmentType.Weapon) 
            {
                this.ClipSize = clipSize; 
                this.CurrentAmmoInClip_Persisted = currentAmmoInClipPersisted; 
                this.ReserveAmmo_Persisted = reserveAmmoPersisted; 
                this.RequiredAmmoName = requiredAmmoName;
                this.ProjectileSpeed = projectileSpeed;
            }

            // Assign throwable/explosive specific properties
            if (isThrowable || equipmentType == EquipmentType.Deployable) // Consider Deployable as potentially explosive too
            {
                this.FuseTime = fuseTime;
                this.ThrowForce = throwForce;
                this.ExplosionDamage = explosionDamage;
                this.AoeRadius = aoeRadius;
            }
            
            UpdateBaseDurability();
            IsBroken = DurabilityPoints <= 0; 
        }

        // Method to update durability and IsBroken status, and sync base.Durability
        public void UpdateDurability(float newDurabilityPoints)
        {
            DurabilityPoints = newDurabilityPoints;
            if (DurabilityPoints < 0) DurabilityPoints = 0;
            if (DurabilityPoints > MaxDurabilityPoints) DurabilityPoints = MaxDurabilityPoints;

            IsBroken = DurabilityPoints <= 0;
            
            UpdateBaseDurability();
        }

        private void UpdateBaseDurability()
        {
            // Sync with base.Durability for UI
            if (MaxDurabilityPoints > 0)
                base.Durability = DurabilityPoints / MaxDurabilityPoints;
            else
                base.Durability = null; // Or 1.0f if it should appear full but non-applicable
        }
    }
}
