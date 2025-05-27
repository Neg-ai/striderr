// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using System;
using System.Collections.Generic;
using System.Linq;
using MySurvivalGame.Data.Items; // For ItemData, ItemStack, ItemDatabase
using Stride.Core; // For DataMemberIgnore attribute if needed by ScriptComponent
using Stride.Engine;

namespace MySurvivalGame.Game.Player
{
    public class PlayerInventoryComponent : ScriptComponent
    {
        public List<ItemStack> InventorySlots { get; private set; }
        public int MaxInventorySlots { get; private set; } = 20; // Default 5x4 grid

        public event Action OnInventoryChanged;

        public override void Start()
        {
            base.Start();
            InitializeInventory();

            // Example: Add some items for testing if the inventory is empty
            if (InventorySlots.All(slot => slot == null))
            {
                Log.Info("PlayerInventory: Initializing with test items.");
                AddItem("wood", 15);
                AddItem("stone", 60); // Should create one full stack and one partial
                AddItem("stone_pickaxe", 1);
                AddItem("health_potion_small", 3);
                AddItem("arrow_basic", 25);
            }
        }

        public void InitializeInventory(int? maxSlots = null)
        {
            if (maxSlots.HasValue)
            {
                MaxInventorySlots = maxSlots.Value;
            }

            InventorySlots = new List<ItemStack>(MaxInventorySlots);
            for (int i = 0; i < MaxInventorySlots; i++)
            {
                InventorySlots.Add(null); // Initialize with empty slots
            }
            Log.Info($"PlayerInventory: Initialized with {MaxInventorySlots} slots.");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Adds a specified quantity of an item to the inventory.
        /// </summary>
        /// <param name="itemID">The ID of the item to add.</param>
        /// <param name="quantity">The quantity to add.</param>
        /// <returns>The quantity actually added (might be less than requested if inventory is full or item doesn't exist).</returns>
        public int AddItem(string itemID, int quantity)
        {
            if (quantity <= 0) return 0;

            ItemData itemData = ItemDatabase.GetItem(itemID);
            if (itemData == null)
            {
                Log.Warning($"PlayerInventory: ItemID '{itemID}' not found in database.");
                return 0;
            }

            int quantityRemainingToAdd = quantity;

            // 1. Try to stack with existing items
            for (int i = 0; i < MaxInventorySlots; i++)
            {
                if (InventorySlots[i] != null && InventorySlots[i].Item.ItemID == itemID)
                {
                    InventorySlots[i].CanAddItem(quantityRemainingToAdd, out int canAddToStack);
                    if (canAddToStack > 0)
                    {
                        InventorySlots[i].AddQuantity(canAddToStack);
                        quantityRemainingToAdd -= canAddToStack;
                        Log.Info($"PlayerInventory: Stacked {canAddToStack} of {itemID} in slot {i}. Remaining to add: {quantityRemainingToAdd}");
                        OnInventoryChanged?.Invoke();
                        if (quantityRemainingToAdd <= 0) return quantity; // All items added
                    }
                }
            }

            // 2. Try to add to new empty slots
            if (quantityRemainingToAdd > 0)
            {
                for (int i = 0; i < MaxInventorySlots; i++)
                {
                    if (InventorySlots[i] == null)
                    {
                        int quantityForNewStack = System.Math.Min(quantityRemainingToAdd, itemData.MaxStackSize);
                        InventorySlots[i] = new ItemStack(itemData, quantityForNewStack);
                        // Durability is handled by ItemStack constructor
                        quantityRemainingToAdd -= quantityForNewStack;
                        Log.Info($"PlayerInventory: Added new stack of {quantityForNewStack} of {itemID} to slot {i}. Remaining to add: {quantityRemainingToAdd}");
                        OnInventoryChanged?.Invoke();
                        if (quantityRemainingToAdd <= 0) return quantity; // All items added
                    }
                }
            }
            
            int quantityAdded = quantity - quantityRemainingToAdd;
            if (quantityRemainingToAdd > 0)
            {
                Log.Warning($"PlayerInventory: Inventory full or unable to stack. Could not add {quantityRemainingToAdd} of {itemID}. Successfully added {quantityAdded}.");
            }
            return quantityAdded;
        }

        /// <summary>
        /// Removes a specified quantity of an item from a given slot.
        /// </summary>
        /// <param name="slotIndex">The index of the inventory slot.</param>
        /// <param name="quantity">The quantity to remove.</param>
        /// <returns>True if the quantity was successfully removed, false otherwise.</returns>
        public bool RemoveItem(int slotIndex, int quantity)
        {
            if (slotIndex < 0 || slotIndex >= MaxInventorySlots || InventorySlots[slotIndex] == null || quantity <= 0)
            {
                return false;
            }

            ItemStack stack = InventorySlots[slotIndex];
            int removedAmount = stack.RemoveQuantity(quantity);

            if (removedAmount > 0)
            {
                Log.Info($"PlayerInventory: Removed {removedAmount} of {stack.Item.ItemName} from slot {slotIndex}.");
                if (stack.Quantity <= 0)
                {
                    InventorySlots[slotIndex] = null;
                    Log.Info($"PlayerInventory: Slot {slotIndex} ({stack.Item.ItemName}) is now empty.");
                }
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Consumes a specified quantity of an item by its name.
        /// Prefers ItemID for robustness, but uses ItemName as per original spec.
        /// </summary>
        /// <param name="itemName">The name of the item to consume.</param>
        /// <param name="quantity">The quantity to consume.</param>
        /// <returns>True if consumption was successful, false otherwise.</returns>
        public bool ConsumeItemByName(string itemName, int quantity)
        {
            if (string.IsNullOrEmpty(itemName) || quantity <= 0) return false;

            int quantityRemainingToConsume = quantity;

            for (int i = 0; i < MaxInventorySlots; i++)
            {
                if (InventorySlots[i] != null && InventorySlots[i].Item.ItemName == itemName)
                {
                    int canConsumeFromStack = Math.Min(quantityRemainingToConsume, InventorySlots[i].Quantity);
                    if (RemoveItem(i, canConsumeFromStack)) // RemoveItem handles OnInventoryChanged
                    {
                        quantityRemainingToConsume -= canConsumeFromStack;
                        Log.Info($"PlayerInventory: Consumed {canConsumeFromStack} of {itemName} from slot {i}. Remaining to consume: {quantityRemainingToConsume}.");
                        if (quantityRemainingToConsume <= 0) return true;
                    }
                }
            }

            if (quantityRemainingToConsume > 0)
            {
                Log.Warning($"PlayerInventory: Could not consume the full quantity of {quantity} for item {itemName}. {quantityRemainingToConsume} remaining.");
                return false; // Partial consumption might still have occurred if some items were removed.
            }
            return true; // Should be reached if quantityRemainingToConsume is 0.
        }


        public ItemStack GetItemStack(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxInventorySlots)
            {
                return null;
            }
            return InventorySlots[slotIndex];
        }

        /// <summary>
        /// Finds the first slot index containing the specified itemID.
        /// </summary>
        /// <param name="itemID">The ItemID to search for.</param>
        /// <returns>The index of the first slot, or -1 if not found.</returns>
        public int FindItemSlot(string itemID)
        {
            for (int i = 0; i < MaxInventorySlots; i++)
            {
                if (InventorySlots[i] != null && InventorySlots[i].Item.ItemID == itemID)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Finds all slot indices containing the specified itemID.
        /// </summary>
        public List<int> FindAllItemSlots(string itemID)
        {
            List<int> foundSlots = new List<int>();
            for (int i = 0; i < MaxInventorySlots; i++)
            {
                if (InventorySlots[i] != null && InventorySlots[i].Item.ItemID == itemID)
                {
                    foundSlots.Add(i);
                }
            }
            return foundSlots;
        }

        /// <summary>
        /// Placeholder for decreasing durability. Actual logic would be more complex.
        /// </summary>
        public void DecreaseDurability(int slotIndex, float amount)
        {
            if (slotIndex < 0 || slotIndex >= MaxInventorySlots || InventorySlots[slotIndex] == null) return;

            ItemStack stack = InventorySlots[slotIndex];
            if (stack.Item.Type == ItemType.Tool || stack.Item.Type == ItemType.Weapon)
            {
                stack.CurrentDurability -= amount;
                if (stack.CurrentDurability < 0) stack.CurrentDurability = 0;
                Log.Info($"PlayerInventory: Durability of {stack.Item.ItemName} in slot {slotIndex} decreased to {stack.CurrentDurability}.");
                OnInventoryChanged?.Invoke(); // Durability change might affect UI

                // Optionally, handle item breaking
                if (stack.CurrentDurability == 0)
                {
                    Log.Info($"PlayerInventory: {stack.Item.ItemName} in slot {slotIndex} broke!");
                    // Potentially remove or mark as broken. For now, just logs.
                    // RemoveItem(slotIndex, 1); // If breaking removes the item
                }
            }
        }

        /// <summary>
        /// Swaps the ItemStacks in two given slot indices.
        /// </summary>
        /// <param name="indexA">The first slot index.</param>
        /// <param name="indexB">The second slot index.</param>
        /// <returns>True if swapping was successful, false otherwise (e.g., out of bounds).</returns>
        public bool SwapSlots(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= MaxInventorySlots || indexB < 0 || indexB >= MaxInventorySlots)
            {
                Log.Warning($"PlayerInventory: SwapSlots - Index out of bounds. A: {indexA}, B: {indexB}");
                return false;
            }

            if (indexA == indexB) return true; // Nothing to swap

            Log.Info($"PlayerInventory: Swapping slot {indexA} ('{InventorySlots[indexA]?.Item?.ItemName ?? "Empty"}') with slot {indexB} ('{InventorySlots[indexB]?.Item?.ItemName ?? "Empty"}').");
            
            ItemStack temp = InventorySlots[indexA];
            InventorySlots[indexA] = InventorySlots[indexB];
            InventorySlots[indexB] = temp;

            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Clears a specific slot in the inventory, setting it to null.
        /// </summary>
        /// <param name="slotIndex">The index of the slot to clear.</param>
        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxInventorySlots)
            {
                Log.Warning($"PlayerInventory: ClearSlot - Index out of bounds: {slotIndex}");
                return;
            }

            if (InventorySlots[slotIndex] != null)
            {
                Log.Info($"PlayerInventory: Clearing slot {slotIndex} (was '{InventorySlots[slotIndex].Item.ItemName}').");
                InventorySlots[slotIndex] = null;
                OnInventoryChanged?.Invoke();
            }
        }

        /// <summary>
        /// Consumes a specified quantity of an item directly from a slot index.
        /// Checks if the item is a consumable.
        /// </summary>
        /// <param name="slotIndex">The index of the slot to consume from.</param>
        /// <param name="quantity">The quantity to consume.</param>
        /// <returns>True if consumption was successful, false otherwise.</returns>
        public bool ConsumeItemBySlot(int slotIndex, int quantity)
        {
            if (slotIndex < 0 || slotIndex >= MaxInventorySlots || quantity <= 0)
            {
                Log.Warning($"PlayerInventory: ConsumeItemBySlot - Invalid parameters. Slot: {slotIndex}, Quantity: {quantity}");
                return false;
            }

            ItemStack stack = GetItemStack(slotIndex);
            if (stack == null)
            {
                Log.Warning($"PlayerInventory: ConsumeItemBySlot - No item in slot {slotIndex}.");
                return false;
            }

            if (stack.Item.Type != ItemType.Consumable)
            {
                Log.Warning($"PlayerInventory: ConsumeItemBySlot - Item '{stack.Item.ItemName}' in slot {slotIndex} is not a consumable.");
                return false;
            }

            if (stack.Quantity < quantity)
            {
                Log.Warning($"PlayerInventory: ConsumeItemBySlot - Not enough quantity of '{stack.Item.ItemName}' in slot {slotIndex}. Has: {stack.Quantity}, Need: {quantity}.");
                return false;
            }

            // Actual consumption logic (remove from stack)
            bool removed = RemoveItem(slotIndex, quantity);
            if (removed)
            {
                Log.Info($"PlayerInventory: Consumed {quantity} of '{stack.Item.ItemName}' from slot {slotIndex}.");
                // OnInventoryChanged is called by RemoveItem
            }
            else
            {
                Log.Error($"PlayerInventory: ConsumeItemBySlot - Failed to remove item '{stack.Item.ItemName}' from slot {slotIndex} despite checks.");
            }
            return removed;
        }
    }
}
