using Stride.Core; // For DataContract and DataMember
// using Stride.Graphics; // For Texture, if we decide to use direct Texture reference later

namespace MySurvivalGame.Game.Data.Items
{
    [DataContract]
    public class ItemData
    {
        [DataMember]
        public string ItemID { get; set; }

        [DataMember]
        public string ItemName { get; set; }

        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Path to the icon texture (e.g., "UI/Icons/Items/MyItemIcon").
        /// The UI system will be responsible for loading this.
        /// </summary>
        [DataMember]
        public string IconPath { get; set; }
        // Alternatively, if direct Texture reference becomes feasible for data assets:
        // public Texture Icon { get; set; }

        [DataMember]
        public int MaxStackSize { get; set; } = 1;

        [DataMember]
        public ItemType Type { get; set; } = ItemType.Generic;

        // Type-specific data properties
        // Only one of these should typically be non-null, depending on the ItemType.
        [DataMember(AllowNull = true)]
        public WeaponStats WeaponData { get; set; }

        [DataMember(AllowNull = true)]
        public ToolStats ToolData { get; set; }

        [DataMember(AllowNull = true)]
        public ConsumableStats ConsumableData { get; set; }

        [DataMember(AllowNull = true)]
        public AmmunitionStats AmmoData { get; set; }

        // DeployableStats could be added here if needed:
        // [DataMember(AllowNull = true)]
        // public DeployableStats DeployableData { get; set; }

        // ResourceStats could be added here if resources have specific properties beyond ItemType.Resource
        // [DataMember(AllowNull = true)]
        // public ResourceStats ResourceData { get; set; }


        public ItemData() { } // Required for serialization

        /// <summary>
        /// Helper constructor for basic items.
        /// </summary>
        public ItemData(string itemID, string itemName, string description, ItemType type, int maxStackSize = 1, string iconPath = null)
        {
            ItemID = itemID;
            ItemName = itemName;
            Description = description;
            Type = type;
            MaxStackSize = maxStackSize;
            IconPath = iconPath;
        }
    }

    /*
    // Example Usage:

    // 1. Stone Pickaxe (Tool)
    var stonePickaxe = new ItemData
    {
        ItemID = "Tool_Pickaxe_Stone",
        ItemName = "Stone Pickaxe",
        Description = "A basic pickaxe for mining stone and ore.",
        IconPath = "UI/Icons/Items/PickaxeStone", // Example path
        MaxStackSize = 1,
        Type = ItemType.Tool,
        ToolData = new ToolStats
        {
            Type = ToolStats.ToolSpecificType.Pickaxe,
            Efficiency = 1.0f,
            BonusResource = ToolStats.SpecialBonusResource.Stone,
            BonusMultiplier = 1.2f, // 20% bonus for stone
            Damage = 8.0f, // Low damage if used as a weapon
            Range = 1.8f
        }
    };

    // 2. Health Potion (Consumable)
    var healthPotion = new ItemData
    {
        ItemID = "Consumable_HealthPotion_Small",
        ItemName = "Small Health Potion",
        Description = "Restores a small amount of health.",
        IconPath = "UI/Icons/Items/HealthPotionSmall", // Example path
        MaxStackSize = 10,
        Type = ItemType.Consumable,
        ConsumableData = new ConsumableStats
        {
            HealthChange = 25f,
            HungerChange = 0f,
            ThirstChange = 0f,
            StaminaChange = 0f,
            EffectDuration = 0f, // Instant effect
            StatusEffectID = null // No special status effect
        }
    };

    // 3. Basic Arrow (Ammunition)
    var basicArrow = new ItemData
    {
        ItemID = "Ammo_Arrow_Basic",
        ItemName = "Basic Arrow",
        Description = "A simple wooden arrow.",
        IconPath = "UI/Icons/Items/ArrowBasic",
        MaxStackSize = 50,
        Type = ItemType.Ammunition,
        AmmoData = new AmmunitionStats
        {
            DamageType = AmmunitionStats.AmmoDamageType.Kinetic,
            ProjectileCount = 1,
            DamageModifier = 0f // No extra damage beyond weapon's base
        }
    };

    // 4. Basic Rifle (Weapon)
    var basicRifle = new ItemData
    {
        ItemID = "Weapon_Rifle_Basic",
        ItemName = "Basic Rifle",
        Description = "A standard semi-automatic rifle.",
        IconPath = "UI/Icons/Items/RifleBasic",
        MaxStackSize = 1,
        Type = ItemType.Weapon,
        WeaponData = new WeaponStats
        {
            Damage = 30f,
            ProjectileCount = 1,
            FireRate = 2.0f, // 2 shots per second
            Range = 100f,
            RequiredAmmoItemID = "Ammo_Rifle_Standard", // Assumes this ammo item exists
            SpecialNotes = "Semi-automatic"
        }
    };
    */
}
