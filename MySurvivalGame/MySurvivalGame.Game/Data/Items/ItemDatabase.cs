using System.Collections.Generic;

namespace MySurvivalGame.Data.Items
{
    /// <summary>
    /// TEMPORARY Item Database for testing purposes.
    /// In a real game, this would be loaded from data files (e.g., JSON, XML, ScriptableObjects).
    /// </summary>
    public static class ItemDatabase
    {
        public static Dictionary<string, ItemData> Definitions { get; private set; } = new Dictionary<string, ItemData>();

        static ItemDatabase()
        {
            // Resources
            Definitions["wood"] = new ItemData
            {
                ItemID = "wood",
                ItemName = "Wood",
                Description = "A sturdy piece of wood.",
                IconPath = "UI/Icons/Items/Wood", // Example path
                MaxStackSize = 50,
                Type = ItemType.Resource
            };

            Definitions["stone"] = new ItemData
            {
                ItemID = "stone",
                ItemName = "Stone",
                Description = "A common piece of stone.",
                IconPath = "UI/Icons/Items/Stone", // Example path
                MaxStackSize = 50,
                Type = ItemType.Resource
            };

            // Tools
            Definitions["stone_pickaxe"] = new ItemData
            {
                ItemID = "stone_pickaxe",
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
                    Damage = 8.0f,
                    Range = 1.8f
                    // MaxDurability would be set here if it existed in ToolStats
                }
            };

            // Consumables (Example from previous task)
            Definitions["health_potion_small"] = new ItemData
            {
                ItemID = "health_potion_small",
                ItemName = "Small Health Potion",
                Description = "Restores a small amount of health.",
                IconPath = "UI/Icons/Items/HealthPotionSmall",
                MaxStackSize = 10,
                Type = ItemType.Consumable,
                ConsumableData = new ConsumableStats
                {
                    HealthChange = 25f,
                }
            };

            // Ammunition (Example from previous task)
             Definitions["arrow_basic"] = new ItemData
            {
                ItemID = "arrow_basic",
                ItemName = "Basic Arrow",
                Description = "A simple wooden arrow.",
                IconPath = "UI/Icons/Items/ArrowBasic",
                MaxStackSize = 50,
                Type = ItemType.Ammunition,
                AmmoData = new AmmunitionStats
                {
                    DamageType = AmmunitionStats.AmmoDamageType.Kinetic,
                    ProjectileCount = 1,
                    DamageModifier = 0f
                }
            };
        }

        public static ItemData GetItem(string itemID)
        {
            Definitions.TryGetValue(itemID, out var item);
            // Could return a clone if ItemData instances are mutable and shared,
            // but for now, assuming ItemData is treated as immutable after definition.
            return item; 
        }
    }
}
