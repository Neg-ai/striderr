using Stride.Engine;
using Stride.Core; // For [DataMember]
using MySurvivalGame.Game.Data.Items; // MODIFIED: For ItemData, ItemStack, ToolStats
using MySurvivalGame.Game.Player; // For PlayerInventoryComponent
using MySurvivalGame.Game.Audio;

namespace MySurvivalGame.Game.World
{
    public enum ResourceNodeType 
    {
        Wood,
        Stone,
        MetalOre,
        Generic 
    }

    // MODIFIED: To better align with ToolStats.ToolSpecificType or general categories
    public enum RequiredToolCategory 
    {
        Any,    // Any tool or hand can harvest
        Hand,   // Only hand or a generic/no tool can harvest
        Axe,    // Requires a tool of type Hatchet (or a future Axe type)
        Pickaxe,// Requires a tool of type Pickaxe
        Drill,  // Requires a tool of type Drill
        Shovel  // Example for future expansion
    }

    public class ResourceNodeComponent : SyncScript 
    {
        [DataMember] 
        public ResourceNodeType NodeType { get; set; } = ResourceNodeType.Wood;

        [DataMember]
        public int TotalResources { get; set; } = 100;

        [DataMember]
        public int HarvestAmountPerHit { get; set; } = 10;

        [DataMember]
        public RequiredToolCategory ToolCategory { get; set; } = RequiredToolCategory.Axe;
        
        public bool HitNode(ItemStack hittingToolStack, PlayerInventoryComponent playerInventory)
        {
            if (TotalResources <= 0)
            {
                Log.Info($"Node '{this.Entity.Name}' is already depleted.");
                return false;
            }

            if (playerInventory == null)
            {
                Log.Error($"ResourceNodeComponent on '{this.Entity.Name}': PlayerInventoryComponent is null. Cannot process hit.");
                return false;
            }

            ItemData currentTool = hittingToolStack?.Item;
            ToolStats toolData = currentTool?.ToolData;

            bool toolCompatible = false;
            string usedToolTypeName = "Hands"; // Default to hands

            if (ToolCategory == RequiredToolCategory.Any)
            {
                toolCompatible = true;
                if (toolData != null) usedToolTypeName = toolData.Type.ToString();
            }
            else if (ToolCategory == RequiredToolCategory.Hand)
            {
                toolCompatible = (hittingToolStack == null || toolData == null || toolData.Type == ToolStats.ToolSpecificType.Generic);
                // If a specific tool is equipped but node needs Hand, it's incompatible unless tool is 'Generic'
                if (hittingToolStack != null && toolData != null && toolData.Type != ToolStats.ToolSpecificType.Generic)
                {
                    toolCompatible = false;
                }
                 if (toolData != null) usedToolTypeName = toolData.Type.ToString();

            }
            else if (toolData != null) // A specific tool type is required, and player has a tool
            {
                usedToolTypeName = toolData.Type.ToString();
                switch (ToolCategory)
                {
                    case RequiredToolCategory.Axe:
                        // Assuming Hatchet is the enum value for axe-like tools
                        toolCompatible = (toolData.Type == ToolStats.ToolSpecificType.Hatchet);
                        break;
                    case RequiredToolCategory.Pickaxe:
                        toolCompatible = (toolData.Type == ToolStats.ToolSpecificType.Pickaxe);
                        break;
                    case RequiredToolCategory.Drill:
                        toolCompatible = (toolData.Type == ToolStats.ToolSpecificType.Drill);
                        break;
                    // case RequiredToolCategory.Shovel: // Example for future
                    //    toolCompatible = (toolData.Type == ToolStats.ToolSpecificType.Shovel);
                    //    break;
                    default:
                        toolCompatible = false; // Unknown or unsupported tool category
                        break;
                }
            }
            // If a specific tool type is required (ToolCategory != Any/Hand) but player has no tool (toolData is null),
            // then toolCompatible remains false by default.

            if (!toolCompatible)
            {
                Log.Info($"Ineffective hit on '{this.Entity.Name}'. Required: {ToolCategory}, Used: {usedToolTypeName}.");
                // Play an ineffective hit sound if desired
                GameSoundManager.PlaySound("Hit_Ineffective", this.Entity.Transform.WorldMatrix.TranslationVector);
                return false;
            }

            int actualHarvestAmount = System.Math.Min(HarvestAmountPerHit, TotalResources);

            // Bonus from tool - Example: increase actualHarvestAmount based on toolData.Efficiency or BonusMultiplier
            if (toolData != null && toolData.BonusResource != ToolStats.SpecialBonusResource.None)
            {
                bool bonusApplies = false;
                switch (NodeType)
                {
                    case ResourceNodeType.Wood: bonusApplies = toolData.BonusResource == ToolStats.SpecialBonusResource.Wood; break;
                    case ResourceNodeType.Stone: bonusApplies = toolData.BonusResource == ToolStats.SpecialBonusResource.Stone; break;
                    case ResourceNodeType.MetalOre: bonusApplies = toolData.BonusResource == ToolStats.SpecialBonusResource.Metal; break;
                }
                if (bonusApplies)
                {
                    actualHarvestAmount = (int)(actualHarvestAmount * toolData.BonusMultiplier);
                    Log.Info($"Tool bonus applied! Original amount: {HarvestAmountPerHit}, Modified: {actualHarvestAmount}");
                }
            }


            string harvestedItemID = null;
            switch (NodeType)
            {
                case ResourceNodeType.Wood: harvestedItemID = "wood"; break;
                case ResourceNodeType.Stone: harvestedItemID = "stone"; break;
                case ResourceNodeType.MetalOre: harvestedItemID = "iron_ore"; break; // Assuming "iron_ore" is defined in ItemDatabase
                case ResourceNodeType.Generic: harvestedItemID = "generic_resource"; break; // Assuming this is defined
            }

            if (string.IsNullOrEmpty(harvestedItemID))
            {
                Log.Error($"ResourceNode on '{this.Entity.Name}': No harvestedItemID defined for NodeType {NodeType}.");
                return false;
            }
            
            // AddItem returns the quantity actually added
            int quantityAdded = playerInventory.AddItem(harvestedItemID, actualHarvestAmount);

            if (quantityAdded > 0)
            {
                TotalResources -= quantityAdded; // Only subtract what was actually taken
                Log.Info($"Harvested {quantityAdded} of '{harvestedItemID}' from '{this.Entity.Name}' with {usedToolTypeName}. Remaining node resources: {TotalResources}");

                string hitSoundName = "Hit_Generic";
                switch (NodeType)
                {
                    case ResourceNodeType.Wood: hitSoundName = "Hit_Wood"; break;
                    case ResourceNodeType.Stone: hitSoundName = "Hit_Stone"; break;
                    case ResourceNodeType.MetalOre: hitSoundName = "Hit_MetalOre"; break;
                }
                GameSoundManager.PlaySound(hitSoundName, this.Entity.Transform.WorldMatrix.TranslationVector);

                if (TotalResources <= 0)
                {
                    Log.Info($"Node '{this.Entity.Name}' depleted.");
                    GameSoundManager.PlaySound("Node_Depleted", this.Entity.Transform.WorldMatrix.TranslationVector);
                    this.Entity.Enabled = false; // Disable the node
                }
                return true; // Harvesting was successful
            }
            else
            {
                Log.Warning($"Inventory full or item '{harvestedItemID}' could not be added. Could not take resources from '{this.Entity.Name}'.");
                // Do NOT reduce TotalResources if item couldn't be added.
                return false; // Harvesting failed (e.g., inventory full)
            }
        }
    }
}
