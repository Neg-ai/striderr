// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using System.Collections.Generic;
using System.Linq; // For FirstOrDefault
using MySurvivalGame.Game.Items; // For MockInventoryItem
using Stride.Core; // For DataMemberIgnore attribute if needed by ScriptComponent
using Stride.Engine;

namespace MySurvivalGame.Game.Player
{
    public class PlayerInventoryComponent : ScriptComponent
    {
        public List<MockInventoryItem> AllPlayerItems { get; private set; } = new List<MockInventoryItem>();

        [DataMember] // Make it configurable in editor
        public float MaxWeight { get; set; } = 50.0f;
        public float CurrentWeight { get; private set; }

        // This Start method is for testing purposes, to populate initial inventory
        public override void Start()
        {
            base.Start();
            if (AllPlayerItems.Count == 0) // Only add if inventory is empty
            {
                // Weights: Wood (0.1f/unit), Stone (0.2f/unit), Potion (0.1f), Pistol (2.5f)
                // New tools: Pickaxe (3.0f), Hatchet (2.0f)
                AddItem(new MockInventoryItem("Wood", "Resource", "A sturdy piece of wood.", null, 30, null, 64, EquipmentType.None, 0.1f));
                AddItem(new MockInventoryItem("Stone", "Resource", "A common grey stone.", null, 90, null, 64, EquipmentType.None, 0.2f));
                
                // Removed "Iron Axe", "Logging Axe", "Stone Pickaxe"
                // Add new Pickaxe
                AddItem(new WeaponToolData(
                    name: "Pickaxe",
                    itemType: "Tool",
                    description: "A basic pickaxe for breaking rocks and mining ore.",
                    equipmentType: EquipmentType.Tool,
                    damage: 10f,
                    fireRate: 1.0f, // swings per second
                    range: 2.0f,
                    maxDurability: 100f,
                    bonusType: SpecialBonusType.Mining,
                    icon: null,
                    quantity: 1,
                    maxStackSize: 1,
                    initialDurability: 100f,
                    clipSize: 0, currentAmmoInClipPersisted: 0, reserveAmmoPersisted: 0, requiredAmmoName: "",
                    weight: 3.0f
                ));

                // Add new Hatchet
                AddItem(new WeaponToolData(
                    name: "Hatchet",
                    itemType: "Tool",
                    description: "A basic hatchet for chopping wood.",
                    equipmentType: EquipmentType.Tool,
                    damage: 12f,
                    fireRate: 1.2f,
                    range: 1.8f,
                    maxDurability: 80f,
                    bonusType: SpecialBonusType.Woodcutting,
                    icon: null,
                    quantity: 1,
                    maxStackSize: 1,
                    initialDurability: 80f,
                    clipSize: 0, currentAmmoInClipPersisted: 0, reserveAmmoPersisted: 0, requiredAmmoName: "",
                    weight: 2.0f
                ));

                AddItem(new MockInventoryItem("Health Potion", "Consumable", "Restores health.", null, 5, null, 10, EquipmentType.Consumable, 0.1f));
                
                // Replace "Old Pistol" with "Pistol"
                AddItem(new WeaponToolData(
                    name: "Pistol",
                    itemType: "Weapon",
                    description: "A standard semi-automatic pistol.",
                    equipmentType: EquipmentType.Weapon,
                    damage: 20f,
                    fireRate: 3.0f, // shots per second
                    range: 50f,
                    maxDurability: 100f,
                    bonusType: SpecialBonusType.Combat,
                    icon: null,
                    quantity: 1,
                    maxStackSize: 1,
                    initialDurability: 100f,
                    clipSize: 12,
                    currentAmmoInClipPersisted: 12,
                    reserveAmmoPersisted: 48,
                    requiredAmmoName: "9mm Bullet", // Example ammo type
                    weight: 1.5f
                    // projectileSpeed is default 0f, not explicitly set for pistol here
                ));

                // Add new Bow
                AddItem(new WeaponToolData(
                    name: "Bow",
                    itemType: "Weapon",
                    description: "A simple wooden bow.",
                    equipmentType: EquipmentType.Weapon,
                    damage: 40f, // Applied by projectile
                    fireRate: 1.0f, // Conceptual draw/nock speed
                    range: 0f, // Range is projectile-based
                    maxDurability: 70f,
                    bonusType: SpecialBonusType.None,
                    icon: null,
                    quantity: 1,
                    maxStackSize: 1,
                    initialDurability: 70f,
                    clipSize: 1, // Represents 1 nocked arrow
                    currentAmmoInClipPersisted: 1, // Start with an arrow nocked
                    reserveAmmoPersisted: 20, // Arrow count
                    requiredAmmoName: "Arrow", // Conceptual
                    weight: 2.0f,
                    projectileSpeed: 50f
                ));

                // Add new Assault Rifle
                AddItem(new WeaponToolData(
                    name: "Assault Rifle",
                    itemType: "Weapon",
                    description: "A fully automatic rifle.",
                    equipmentType: EquipmentType.Weapon,
                    damage: 15f,
                    fireRate: 8.0f, // FireRateHz for BaseRangedWeapon
                    range: 70f,
                    maxDurability: 150f,
                    bonusType: SpecialBonusType.Combat,
                    icon: null,
                    quantity: 1,
                    maxStackSize: 1,
                    initialDurability: 150f,
                    clipSize: 30,
                    currentAmmoInClipPersisted: 30,
                    reserveAmmoPersisted: 90,
                    requiredAmmoName: "5.56mm Bullet", // Example ammo type
                    weight: 3.5f,
                    projectileSpeed: 0f // Not used for hitscan
                ));

                // Add new Grenade
                AddItem(new WeaponToolData(
                    name: "Grenade",
                    itemType: "Weapon", // Or a new "Throwable" type if defined
                    description: "A throwable explosive device.",
                    equipmentType: EquipmentType.Weapon, // Or other suitable type
                    damage: 0f, // Direct damage is 0, projectile handles AOE
                    fireRate: 1.0f, // Time between throws
                    range: 0f, // Range determined by throw
                    maxDurability: 0f, // Non-durable, consumed on use
                    bonusType: SpecialBonusType.None,
                    icon: null,
                    quantity: 5, // Start with 5 grenades
                    maxStackSize: 10, // Stackable up to 10
                    initialDurability: null, // Non-durable
                    clipSize: 1, // Represents one grenade "ready" or "in hand"
                    currentAmmoInClipPersisted: 1, // Start with one ready if has quantity
                    reserveAmmoPersisted: 5, // This is the total count, effectively
                    requiredAmmoName: "", // Self-contained
                    weight: 0.5f,
                    projectileSpeed: 0f, // Not used directly, ThrowForce is used
                    isThrowable: true, // Mark as throwable
                    fuseTime: 3.0f,
                    throwForce: 15.0f,
                    explosionDamage: 100f,
                    aoeRadius: 5.0f
                ));
            }
            RecalculateCurrentWeight(); // Calculate initial weight
        }

        private void RecalculateCurrentWeight()
        {
            CurrentWeight = 0f;
            foreach (var item in AllPlayerItems)
            {
                if (item != null)
                {
                    CurrentWeight += item.Weight * item.Quantity; // Assuming item.Weight is per unit
                }
            }
            // Log.Info($"Current Weight Recalculated: {CurrentWeight}"); // Optional: for debugging
        }

        public bool AddItem(MockInventoryItem itemToAdd)
        {
            if (itemToAdd == null || itemToAdd.Quantity <= 0) return false;

            // --- Weight Check ---
            float weightOfItemToAddStack = itemToAdd.Weight * itemToAdd.Quantity;
            int quantityActuallyAdded = 0;

            // Try to stack first, this logic might need refinement if stacking part of a heavy incoming stack.
            // For now, we check if the *entire* incoming itemToAdd can fit before attempting to stack or add new.
            // This is a simplification. A more complex logic would try to add partial quantity.

            if (CurrentWeight + weightOfItemToAddStack > MaxWeight)
            {
                // Calculate how many units can be afforded based on remaining capacity
                float remainingCapacity = MaxWeight - CurrentWeight;
                if (remainingCapacity <= 0 || itemToAdd.Weight <= 0) // Cannot add anything if no capacity or item has no weight (or invalid weight)
                {
                    Log.Info($"PlayerInventory: Cannot add {itemToAdd.Name}. Overweight. Current: {CurrentWeight}, Max: {MaxWeight}, Item Stack Weight: {weightOfItemToAddStack}");
                    return false;
                }

                int affordableQuantity = (int)(remainingCapacity / itemToAdd.Weight);
                if (affordableQuantity <= 0)
                {
                    Log.Info($"PlayerInventory: Cannot add even one unit of {itemToAdd.Name}. Overweight. Current: {CurrentWeight}, Max: {MaxWeight}, Single Item Weight: {itemToAdd.Weight}");
                    return false;
                }

                // If we can only afford a partial quantity of the itemToAdd stack
                if (affordableQuantity < itemToAdd.Quantity)
                {
                    Log.Info($"PlayerInventory: Adding partial stack of {itemToAdd.Name}. Can only afford {affordableQuantity} out of {itemToAdd.Quantity} due to weight limit.");
                    // Create a new item instance with just the affordable quantity to attempt to add.
                    // The original itemToAdd is conceptually what was picked up, and its quantity will be reduced if this new temp item is successfully added.
                    var tempItemForPartialAdd = new MockInventoryItem(
                        itemToAdd.Name, itemToAdd.ItemType, itemToAdd.Description, itemToAdd.Icon,
                        affordableQuantity, itemToAdd.Durability, itemToAdd.MaxStackSize, itemToAdd.CurrentEquipmentType, itemToAdd.Weight);

                    // Now try to add this partial, affordable item
                    bool partialAddSuccess = AddItemInternal(tempItemForPartialAdd, out int partialQuantityActuallyAdded);
                    if(partialAddSuccess)
                    {
                        itemToAdd.Quantity -= partialQuantityActuallyAdded; // Reduce original item's quantity by what was taken
                        RecalculateCurrentWeight();
                        return true; // Indicate that at least some part was added
                    }
                    return false; // Could not even add the partial amount (e.g. no inventory slots, though current AddItemInternal doesn't check this)
                }
                // If the full stack is affordable after all (e.g. affordableQuantity == itemToAdd.Quantity), proceed to AddItemInternal
            }

            // If full item stack is fine by weight, or partial quantity was determined and is now itemToAdd for AddItemInternal
            bool success = AddItemInternal(itemToAdd, out quantityActuallyAdded);
            if (success)
            {
                RecalculateCurrentWeight();
            }
            return success;
        }

        // Internal add logic without recursive weight checks to avoid loops
        private bool AddItemInternal(MockInventoryItem itemToAdd, out int quantityAdded)
        {
            quantityAdded = 0;
            int initialQuantityOfItemStack = itemToAdd.Quantity;

            // Attempt to stack with existing items
            if (itemToAdd.IsStackable)
            {
                foreach (var existingItem in AllPlayerItems)
                {
                    if (existingItem.Name == itemToAdd.Name && // Simple name check for stackability for now
                        existingItem.IsStackable &&
                        existingItem.Quantity < existingItem.MaxStackSize)
                    {
                        int canAdd = existingItem.MaxStackSize - existingItem.Quantity;
                        int willAdd = System.Math.Min(itemToAdd.Quantity, canAdd);

                        existingItem.Quantity += willAdd;
                        itemToAdd.Quantity -= willAdd;
                        quantityAdded += willAdd;
                        Log.Info($"PlayerInventory: Stacked {willAdd} of {itemToAdd.Name}. Remaining to add: {itemToAdd.Quantity}");
                        if (itemToAdd.Quantity <= 0)
                        {
                            // Fully stacked the provided item's quantity
                            return true;
                        }
                    }
                }
            }

            // If item still has quantity, add as new stack or new item
            if (itemToAdd.Quantity > 0)
            {
                // Optional: Check for inventory capacity (max slot limit)
                // For now, just add.
                // Create a new instance to ensure we're adding a fresh copy if it's a new stack
                var newItemInstance = new MockInventoryItem(
                    itemToAdd.Name, 
                    itemToAdd.ItemType, 
                    itemToAdd.Description, 
                    itemToAdd.Icon, 
                    itemToAdd.Quantity, // This is the remaining quantity from itemToAdd
                    itemToAdd.Durability, 
                    itemToAdd.MaxStackSize, 
                    itemToAdd.CurrentEquipmentType,
                    itemToAdd.Weight // Ensure weight is carried over
                );
                AllPlayerItems.Add(newItemInstance);
                quantityAdded += newItemInstance.Quantity; // Added the rest as a new stack
                Log.Info($"PlayerInventory: Added new stack of {newItemInstance.Quantity} of {newItemInstance.Name}.");
                return true;
            }

            // If quantityAdded > 0 but itemToAdd.Quantity became 0 through stacking, it's a success.
            // If itemToAdd.Quantity was >0 initially but couldn't be added as new stack (e.g. slot limit if implemented),
            // and no stacking occurred, then quantityAdded would be 0.
            return quantityAdded > 0; // Return true if any quantity was successfully added/stacked.
        }

        public void RemoveItem(MockInventoryItem itemToRemove)
        {
            if (itemToRemove == null) return;
            var itemInstance = AllPlayerItems.FirstOrDefault(i => i.UniqueId == itemToRemove.UniqueId);
            if (itemInstance != null) 
            {
                bool removed = AllPlayerItems.Remove(itemInstance);
                if(removed)
                {
                    Log.Info($"PlayerInventory: Removed item {itemToRemove.Name} (ID: {itemToRemove.UniqueId}).");
                    RecalculateCurrentWeight();
                }
            }
        }

        public bool TryConsumeQuantity(System.Guid itemId, int quantityToConsume)
        {
            var itemInstance = AllPlayerItems.FirstOrDefault(i => i.UniqueId == itemId);
            if (itemInstance != null)
            {
                if (itemInstance.Quantity >= quantityToConsume)
                {
                    itemInstance.Quantity -= quantityToConsume;
                    Log.Info($"PlayerInventory: Consumed {quantityToConsume} of {itemInstance.Name}. Remaining: {itemInstance.Quantity}.");
                    if (itemInstance.Quantity <= 0)
                    {
                        AllPlayerItems.Remove(itemInstance);
                        Log.Info($"PlayerInventory: Removed empty stack of {itemInstance.Name} after consumption.");
                    }
                    RecalculateCurrentWeight();
                    return true;
                }
                else
                {
                    Log.Warning($"PlayerInventory: Not enough quantity to consume {quantityToConsume} of {itemInstance.Name}. Has: {itemInstance.Quantity}.");
                }
            }
            else
            {
                 Log.Warning($"PlayerInventory: Item with ID {itemId} not found for consumption.");
            }
            return false;
        }
    }
}
