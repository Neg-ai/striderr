// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Engine;
using Stride.Engine.Events; 
// REMOVED: using MySurvivalGame.Game.Items; 
// REMOVED: using MySurvivalGame.Game.UI.Scripts; 
// REMOVED: using System.Linq; 

namespace MySurvivalGame.Game.Player
{
    public class PlayerHotbarManager : ScriptComponent
    {
        // The HotbarItems array is removed. PlayerInventoryComponent is the source of truth.
        // Hotbar slots are assumed to be the first N slots of the PlayerInventoryComponent.
        // Example: If hotbar has 8 slots, these are indices 0-7 in PlayerInventoryComponent.InventorySlots.
        
        private EventReceiver<int> hotbarSlotSelectedReceiver; 
        private PlayerEquipment playerEquipment;
        // private PlayerInventoryComponent playerInventory; // Not strictly needed if PlayerEquipment handles consumable logic

        // The UpdateHotbarSlot method is removed as PlayerInventoryComponent.OnInventoryChanged 
        // will trigger InventoryPanelScript.RefreshInventoryDisplay(), which updates all UI slots including hotbar.

        public override void Start()
        {
            playerEquipment = Entity.Get<PlayerEquipment>();
            if (playerEquipment == null)
            {
                Log.Error("PlayerHotbarManager: PlayerEquipment component not found on this entity or parent.");
            }

            // playerInventory = Entity.Get<PlayerInventoryComponent>(); // If needed for direct consumable check, but prefer PlayerEquipment handles it
            // if (playerInventory == null)
            // {
            //     Log.Error("PlayerHotbarManager: PlayerInventoryComponent not found on this entity or parent.");
            // }

            Log.Info("PlayerHotbarManager started.");
            hotbarSlotSelectedReceiver = new EventReceiver<int>(MySurvivalGame.Game.PlayerInput.HotbarSlotSelectedEventKey);
        }

        public override void Update() 
        {
            if (hotbarSlotSelectedReceiver.TryReceive(out int selectedHotbarIndex)) // selectedHotbarIndex is 0-7 for keys 1-8
            {
                if (playerEquipment == null)
                {
                    Log.Error("PlayerHotbarManager: PlayerEquipment component is missing, cannot equip item.");
                    return;
                }

                // The selectedHotbarIndex directly corresponds to the slot index in PlayerInventoryComponent.InventorySlots
                // PlayerEquipment.EquipItemFromSlot will handle equipping weapons/tools.
                // If the item is a consumable, PlayerEquipment.PrimaryAction (when triggered) should handle its use.
                Log.Info($"PlayerHotbarManager: Hotbar slot UI index {selectedHotbarIndex + 1} (data index {selectedHotbarIndex}) selected. Relaying to PlayerEquipment.");
                playerEquipment.EquipItemFromSlot(selectedHotbarIndex);
                
                // The old logic for directly consuming items here is removed.
                // PlayerEquipment.PrimaryAction() will now check if the equipped/selected item is a consumable
                // and then call playerInventory.ConsumeItemBySlot(selectedHotbarIndex, 1).
            }
        }
    }
}
