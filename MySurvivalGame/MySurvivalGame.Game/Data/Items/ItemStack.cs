using Stride.Core; // For DataContract and DataMember

namespace MySurvivalGame.Game.Data.Items
{
    [DataContract]
    public class ItemStack
    {
        // Default maximum durability for items that have durability but don't define a specific max.
        public const float DefaultMaxDurability = 100f;

        [DataMember]
        public ItemData Item { get; private set; } // Reference to the item's definition

        [DataMember]
        public int Quantity { get; private set; }

        [DataMember]
        public float CurrentDurability { get; set; }

        // Parameterless constructor for serialization
        public ItemStack() { }

        public ItemStack(ItemData item, int quantity)
        {
            Item = item;
            Quantity = quantity; // Initial quantity, validation should be done by inventory manager

            // Initialize durability for items that should have it
            if (item.Type == ItemType.Tool || item.Type == ItemType.Weapon)
            {
                // Ideally, MaxDurability would come from ItemData.ToolData.MaxDurability or ItemData.WeaponData.MaxDurability
                // Since those fields don't exist from the previous task, we'll use a default.
                // This can be refined later if MaxDurability is added to ToolStats/WeaponStats.
                CurrentDurability = DefaultMaxDurability;
            }
            else
            {
                CurrentDurability = 0; // No durability for other item types
            }
        }

        /// <summary>
        /// Checks how many of a quantity can be added to this stack.
        /// </summary>
        /// <param name="quantityToAdd">The desired quantity to add.</param>
        /// <param name="canAdd">Output parameter for the quantity that can actually be added.</param>
        /// <returns>True if any quantity can be added, false otherwise.</returns>
        public bool CanAddItem(int quantityToAdd, out int canAdd)
        {
            canAdd = 0;
            if (Item == null || quantityToAdd <= 0)
            {
                return false;
            }

            if (Quantity < Item.MaxStackSize)
            {
                canAdd = System.Math.Min(quantityToAdd, Item.MaxStackSize - Quantity);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a quantity to the stack. Assumes CanAddItem check has been performed.
        /// </summary>
        /// <param name="quantityToAdd">The quantity to add.</param>
        public void AddQuantity(int quantityToAdd)
        {
            if (quantityToAdd <= 0) return;
            Quantity += quantityToAdd;
            // Ensure quantity doesn't exceed max stack size (should be pre-validated by caller using CanAddItem)
            if (Quantity > Item.MaxStackSize)
            {
                Quantity = Item.MaxStackSize;
            }
        }

        /// <summary>
        /// Removes a quantity from the stack.
        /// </summary>
        /// <param name="quantityToRemove">The quantity to remove.</param>
        /// <returns>The actual quantity removed (might be less if not enough items in stack).</returns>
        public int RemoveQuantity(int quantityToRemove)
        {
            if (quantityToRemove <= 0) return 0;

            int actualRemoved = System.Math.Min(quantityToRemove, Quantity);
            Quantity -= actualRemoved;
            return actualRemoved;
        }
    }
}
