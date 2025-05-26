// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;
using Stride.Engine.Events; 
using MySurvivalGame.Game.Weapons; 
using MySurvivalGame.Game.Player;   
using MySurvivalGame.Data.Items; // MODIFIED: For ItemData, ItemStack, ItemDatabase
using MySurvivalGame.Game.World; 
using Stride.Physics; 
using Stride.Core.Mathematics;
using System; // For Activator
using System.Collections.Generic; // For Dictionary

namespace MySurvivalGame.Game.Player 
{
    public class PlayerEquipment : ScriptComponent
    {
        public BaseWeapon CurrentWeapon { get; private set; }
        private int equippedSlotIndex = -1; 
        private PlayerInventoryComponent playerInventory;

        // TODO: Populate this map with ItemID -> ScriptType mappings
        private Dictionary<string, Type> itemScriptMap = new Dictionary<string, Type>(); 

        private EventReceiver<bool> shootEventReceiver;
        private EventReceiver<bool> reloadEventReceiver;
        private EventReceiver shootReleasedEventReceiver; 
        private EventReceiver interactReceiver; 
        private EventReceiver<int> hotbarSlotSelectedReceiver; // For hotbar selection

        public override void Start()
        {
            base.Start(); 

            playerInventory = Entity.Get<PlayerInventoryComponent>();
            if (playerInventory == null)
            {
                Log.Error("PlayerEquipment: PlayerInventoryComponent not found on the entity.");
                // Potentially disable this component or throw an error
                return;
            }

            // Example mapping - this would ideally be data-driven or more robust
            itemScriptMap["stone_pickaxe"] = typeof(Hatchet); // Assuming Hatchet script can serve as a generic tool for now

            shootEventReceiver = new EventReceiver<bool>(PlayerInput.ShootEventKey);
            reloadEventReceiver = new EventReceiver<bool>(PlayerInput.ReloadEventKey);
            shootReleasedEventReceiver = new EventReceiver(PlayerInput.ShootReleasedEventKey);
            interactReceiver = new EventReceiver(PlayerInput.InteractEventKey);
            hotbarSlotSelectedReceiver = new EventReceiver<int>(PlayerInput.HotbarSlotSelectedEventKey);
        }

        private void UnequipCurrentWeapon()
        {
            if (CurrentWeapon != null)
            {
                Log.Info($"PlayerEquipment: Unequipping '{CurrentWeapon.Entity?.Name ?? "weapon"}'.");
                CurrentWeapon.OnUnequip(this.Entity);
                if (CurrentWeapon.Entity != null && CurrentWeapon.Entity.Parent == this.Entity)
                {
                    this.Entity.RemoveChild(CurrentWeapon.Entity);
                    // Optionally, pool or destroy CurrentWeapon.Entity if it was dynamically created for the weapon
                    // For now, just removing it as a child. If it was from a prefab, this is fine.
                    // If it was new Entity("EquippedWeaponEntity"), it should be Entity.Scene = null;
                }
                CurrentWeapon = null;
            }
            equippedSlotIndex = -1;
        }

        public void EquipItemFromSlot(int slotIndex)
        {
            if (playerInventory == null)
            {
                Log.Error("PlayerEquipment: PlayerInventoryComponent is missing.");
                return;
            }

            if (slotIndex < 0 || slotIndex >= playerInventory.MaxInventorySlots)
            {
                Log.Warning($"PlayerEquipment: Invalid slot index {slotIndex}.");
                UnequipCurrentWeapon(); // Unequip if out of bounds index means nothing selected
                return;
            }
            
            // If trying to equip the same slot that's already equipped, do nothing (or re-equip if desired)
            // For now, let's allow re-equipping to refresh the item, might be useful.
            // if (slotIndex == equippedSlotIndex && CurrentWeapon != null)
            // {
            //     Log.Info($"PlayerEquipment: Slot {slotIndex} is already equipped.");
            //     return; 
            // }

            UnequipCurrentWeapon(); // Unequip previous item

            ItemStack itemStack = playerInventory.GetItemStack(slotIndex);

            if (itemStack == null || itemStack.Item == null)
            {
                Log.Info($"PlayerEquipment: No item in slot {slotIndex} to equip.");
                // equippedSlotIndex is already -1 from UnequipCurrentWeapon
                return;
            }

            equippedSlotIndex = slotIndex; // Set new equipped slot
            ItemData itemToEquip = itemStack.Item;
            Log.Info($"PlayerEquipment: Attempting to equip '{itemToEquip.ItemName}' from slot {slotIndex}. Type: {itemToEquip.Type}");

            // Determine if the item is a weapon or tool that needs a script
            if (itemToEquip.Type == ItemType.Weapon || itemToEquip.Type == ItemType.Tool)
            {
                // Try to find a script for this itemID
                // For now, hardcoding for "stone_pickaxe" to use Hatchet script
                Type weaponScriptType = null;
                if (itemToEquip.ItemID == "stone_pickaxe") // Example specific lookup
                {
                    weaponScriptType = typeof(Hatchet); // Using Hatchet as a placeholder for pickaxe
                }
                // else if (itemScriptMap.TryGetValue(itemToEquip.ItemID, out var foundScriptType))
                // {
                //    weaponScriptType = foundScriptType;
                // }

                if (weaponScriptType != null)
                {
                    Log.Info($"PlayerEquipment: Found script type '{weaponScriptType.Name}' for item '{itemToEquip.ItemName}'.");
                    var newWeaponEntity = new Entity("EquippedItemInstance"); // Name for debugging
                    
                    CurrentWeapon = (BaseWeapon)Activator.CreateInstance(weaponScriptType);
                    if (CurrentWeapon == null)
                    {
                        Log.Error($"PlayerEquipment: Failed to create instance of script type '{weaponScriptType.Name}'.");
                        equippedSlotIndex = -1; // Failed to equip
                        return;
                    }
                    
                    newWeaponEntity.Add(CurrentWeapon); 
                    this.Entity.AddChild(newWeaponEntity); 

                    CurrentWeapon.Configure(itemToEquip); // Pass ItemData to the weapon script
                    CurrentWeapon.OnEquip(this.Entity); // Notify script it's equipped
                    Log.Info($"PlayerEquipment: Successfully equipped '{itemToEquip.ItemName}' with script '{weaponScriptType.Name}'.");
                }
                else
                {
                    Log.Warning($"PlayerEquipment: No specific weapon/tool script found for ItemID '{itemToEquip.ItemID}'. Item will be 'held' but might not have actions.");
                    // CurrentWeapon remains null, no script-based actions will occur.
                }
            }
            else
            {
                Log.Info($"PlayerEquipment: Item '{itemToEquip.ItemName}' is not a weapon or tool. Equipped as passive item.");
                // CurrentWeapon remains null.
            }
        }

        private void SecondaryAction() // ADDED
        {
            if (equippedSlotIndex == -1)
            {
                // Log.Info("PlayerEquipment: No item equipped for secondary action.");
                return;
            }

            ItemStack stack = playerInventory.GetItemStack(equippedSlotIndex);
            if (stack == null || stack.Item == null)
            {
                Log.Error($"PlayerEquipment: Equipped slot {equippedSlotIndex} contains no item stack for secondary action. Unequipping.");
                UnequipCurrentWeapon();
                return;
            }

            if (stack.Item.Type != ItemType.Weapon && stack.Item.Type != ItemType.Tool)
            {
                // Log.Info($"PlayerEquipment: Item '{stack.Item.ItemName}' in slot {equippedSlotIndex} is not a weapon or tool. Cannot perform secondary action.");
                return;
            }
            
            if (stack.CurrentDurability <= 0)
            {
                Log.Info($"PlayerEquipment: Item '{stack.Item.ItemName}' is broken! Durability: {stack.CurrentDurability}. Cannot perform secondary action.");
                return; 
            }

            if (CurrentWeapon != null)
            {
                Log.Info($"PlayerEquipment: Performing SecondaryAction for '{stack.Item.ItemName}' using script {CurrentWeapon.GetType().Name}. Durability before: {stack.CurrentDurability}");
                CurrentWeapon.SecondaryAction(); // Delegate to the weapon script
                
                // Durability consumption for secondary action
                float durabilityCost = 0.5f; // Example cost, can be different from primary
                playerInventory.DecreaseDurability(equippedSlotIndex, durabilityCost);
            }
            else
            {
                Log.Warning($"PlayerEquipment: Item '{stack.Item.ItemName}' is a weapon/tool but has no active script (CurrentWeapon is null). Secondary action not performed by script.");
                // Fallback or generic action if any? For now, just consume durability if it's a generic tool action.
                // float durabilityCost = 0.5f;
                // playerInventory.DecreaseDurability(equippedSlotIndex, durabilityCost);
            }
        }

        public override void Update()
        {
            if (playerInventory == null) return;

            // Hotbar selection
            if (hotbarSlotSelectedReceiver.TryReceive(out int selectedSlot))
            {
                Log.Info($"PlayerEquipment: Hotbar slot {selectedSlot + 1} selected via input.");
                EquipItemFromSlot(selectedSlot); // Hotbar indices are 0-based internally
            }

            if (interactReceiver.TryReceive())
            {
                AttemptResourceGather();
            }

            if (shootEventReceiver.TryReceive(out bool shootPressed) && shootPressed)
            {
                PrimaryAction();
            }

            if (shootReleasedEventReceiver.TryReceive()) 
            {
                if (CurrentWeapon is BaseBowWeapon bowWeapon) 
                {
                    bowWeapon.OnPrimaryActionReleased();
                }
            }

            if (reloadEventReceiver.TryReceive(out bool reloadPressed) && reloadPressed)
            {
                Reload();
            }
        }

        private void PrimaryAction()
        {
            if (equippedSlotIndex == -1)
            {
                // Log.Info("PlayerEquipment: No item equipped for primary action.");
                return;
            }

            ItemStack stack = playerInventory.GetItemStack(equippedSlotIndex);
            if (stack == null || stack.Item == null)
            {
                Log.Error($"PlayerEquipment: Equipped slot {equippedSlotIndex} contains no item stack. Unequipping.");
                UnequipCurrentWeapon();
                return;
            }

            if (stack.Item.Type != ItemType.Weapon && stack.Item.Type != ItemType.Tool)
            {
                Log.Info($"PlayerEquipment: Item '{stack.Item.ItemName}' in slot {equippedSlotIndex} is not a weapon or tool. Cannot perform primary action.");
                return;
            }
            
            if (stack.CurrentDurability <= 0)
            {
                Log.Info($"PlayerEquipment: Item '{stack.Item.ItemName}' is broken! Durability: {stack.CurrentDurability}");
                // Optionally play a "broken tool/weapon" sound or visual feedback
                return; 
            }

            if (CurrentWeapon != null)
            {
                Log.Info($"PlayerEquipment: Performing PrimaryAction for '{stack.Item.ItemName}' using script {CurrentWeapon.GetType().Name}. Durability before: {stack.CurrentDurability}");
                CurrentWeapon.PrimaryAction(); // Delegate to the weapon script
                
                // Durability consumption
                float durabilityCost = 1.0f; // Example cost, can be action/item specific
                playerInventory.DecreaseDurability(equippedSlotIndex, durabilityCost);
                // Log.Info($"PlayerEquipment: Durability after action for '{stack.Item.ItemName}': {playerInventory.GetItemStack(equippedSlotIndex)?.CurrentDurability}");
            }
            else
            {
                Log.Warning($"PlayerEquipment: Item '{stack.Item.ItemName}' is a weapon/tool but has no active script (CurrentWeapon is null). Primary action not performed by script.");
                // Fallback or generic action if any? For now, just consume durability.
                float durabilityCost = 1.0f;
                playerInventory.DecreaseDurability(equippedSlotIndex, durabilityCost);
            }
        }

        private void Reload()
        {
            if (CurrentWeapon != null)
            {
                ItemStack stack = playerInventory.GetItemStack(equippedSlotIndex);
                if (stack != null && stack.CurrentDurability <= 0)
                {
                    Log.Info($"PlayerEquipment: Cannot reload, {CurrentWeapon.Entity?.Name ?? "Current weapon"} is broken (via ItemStack).");
                    return;
                }
                Log.Info($"PlayerEquipment: Reloading '{CurrentWeapon.Entity?.Name ?? "weapon"}'.");
                CurrentWeapon.Reload();
                // Reload might also consume durability or have other effects.
            }
            else
            {
                // Log.Info("PlayerEquipment: No weapon equipped to reload.");
            }
        }
        
        private void AttemptResourceGather()
        {
            if (equippedSlotIndex == -1)
            {
                // Log.Info("PlayerEquipment: No item equipped for resource gathering.");
                // Allow hand gathering if implemented in ResourceNodeComponent.HitNode
            }

            ItemStack currentToolStack = playerInventory.GetItemStack(equippedSlotIndex);
            ItemData currentToolData = currentToolStack?.Item; // This can be null if no item is equipped

            if (currentToolStack != null && currentToolStack.CurrentDurability <= 0)
            {
                Log.Info($"PlayerEquipment: Tool '{currentToolStack.Item.ItemName}' is broken! Cannot gather.");
                return;
            }

            var playerInput = this.Entity.Get<PlayerInput>();
            if (playerInput == null || playerInput.Camera == null)
            {
                Log.Error("PlayerEquipment.AttemptResourceGather: PlayerInput or Camera not found.");
                return;
            }

            var camera = playerInput.Camera; 
            var simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error("PlayerEquipment.AttemptResourceGather: Physics simulation not found.");
                return;
            }

            Matrix cameraWorldMatrix = camera.Entity.Transform.WorldMatrix;
            Vector3 raycastStart = cameraWorldMatrix.TranslationVector;
            Vector3 raycastForward = cameraWorldMatrix.Forward;
            float gatherRange = 2.0f; 

            var hitResult = simulation.Raycast(raycastStart, raycastStart + raycastForward * gatherRange);

            if (hitResult.Succeeded && hitResult.Collider != null)
            {
                var hitEntity = hitResult.Collider.Entity;
                var resourceNode = hitEntity?.Get<MySurvivalGame.Game.World.ResourceNodeComponent>();

                if (resourceNode != null)
                {
                    Log.Info($"PlayerEquipment: Interacted with '{hitEntity.Name}' which has a ResourceNodeComponent.");
                    
                    var harvestedItem = resourceNode.HitNode(currentToolData, playerInventory); // Pass ItemData or null

                    if (harvestedItem != null) 
                    {
                        Log.Info($"PlayerEquipment: Successfully harvested '{harvestedItem.ItemName}' using '{currentToolData?.ItemName ?? "Hands"}'.");
                        
                        if (currentToolData != null && (currentToolData.Type == ItemType.Tool || currentToolData.Type == ItemType.Weapon))
                        {
                            // Durability cost for successful gather with a tool
                            float durabilityCost = 1.0f; 
                            playerInventory.DecreaseDurability(equippedSlotIndex, durabilityCost);
                            // Log.Info($"PlayerEquipment: Tool '{currentToolData.ItemName}' durability after gathering: {playerInventory.GetItemStack(equippedSlotIndex)?.CurrentDurability}.");
                        }
                    }
                }
            }
        }
        
        // REMOVED: Old EquipWeapon(BaseWeapon newWeapon) - replaced by EquipItemFromSlot
        // REMOVED: Old EquipItem(MockInventoryItem itemToEquip) - replaced by EquipItemFromSlot
        // REMOVED: Old TriggerCurrentWeaponPrimary() - logic moved into PrimaryAction()
        // REMOVED: Old TriggerCurrentWeaponSecondary() - will be re-added if needed, simpler for now
    }
}
