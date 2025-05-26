// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Engine;
// Potentially using MySurvivalGame.Game.Core for ITargetable if needed later
// Potentially using MySurvivalGame.Game.Items for MaterialType if needed later
using MySurvivalGame.Data.Items; // Required for ItemData

namespace MySurvivalGame.Game.Weapons
{
    public abstract class BaseWeapon : ScriptComponent 
    {
        public ItemData ConfiguredItemData { get; protected set; }

        // IsBroken and Durability are now primarily managed by PlayerEquipment via ItemStack.
        // These properties can be removed from BaseWeapon if the weapon script itself
        // doesn't need to react to its own broken state independently of PlayerEquipment's checks.
        // For now, they can remain but might be unused or reflect initial state.
        public virtual bool IsBrokenProxy => ConfiguredItemData == null || (Entity?.Get<Player.PlayerEquipment>()?.GetItemStack(Entity.Get<Player.PlayerEquipment>().EquippedSlotIndex)?.CurrentDurability ?? 0) <= 0;
        public virtual float DurabilityProxy => Entity?.Get<Player.PlayerEquipment>()?.GetItemStack(Entity.Get<Player.PlayerEquipment>().EquippedSlotIndex)?.CurrentDurability ?? 0;


        public virtual void Configure(ItemData data)
        {
            ConfiguredItemData = data;
            Log.Info($"BaseWeapon {this.GetType().Name} configured with ItemID: {data?.ItemID ?? "NULL"}");
        }

        public abstract void PrimaryAction();
        public abstract void SecondaryAction();
        public abstract void Reload();
        public abstract void OnEquip(Entity owner); // Owner is the player entity
        public abstract void OnUnequip(Entity owner); // Owner is the player entity

        // Placeholder for potential bow functionality
        public virtual void OnPrimaryActionReleased() { } 
    }

    // BaseBowWeapon remains as it was, inheriting the new Configure method and ConfiguredItemData.
    public abstract class BaseBowWeapon : BaseWeapon 
    {
        // Specific bow logic would go here
    }
}
