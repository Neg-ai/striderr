using Stride.Engine;
using Stride.Core.Mathematics; // For Matrix, Vector3 for camera access
using MySurvivalGame.Game.Player; 
using MySurvivalGame.Game.Data.Items;  // MODIFIED: For ItemData
using MySurvivalGame.Game.Audio; // ADDED for GameSoundManager

namespace MySurvivalGame.Game.Weapons.Ranged
{
    public class Pistol : BaseRangedWeapon
    {
        public override int ClipSize { get; protected set; } // Now set from ConfiguredItemData
        public int ActualCurrentAmmoInClip { get; protected set; }
        public int ActualReserveAmmo { get; protected set; }

        private PlayerInventoryComponent playerInventory; // Cache player inventory

        public override void PrimaryAction()
        {
            var itemName = ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "Pistol";
            if (ActualCurrentAmmoInClip > 0)
            {
                ActualCurrentAmmoInClip--;
                Log.Info($"{itemName}: Fired. Ammo: {ActualCurrentAmmoInClip}/{ClipSize}. Reserve: {ActualReserveAmmo}");
                
                var camera = GetCamera();
                if (camera != null && ConfiguredItemData?.WeaponData != null)
                {
                    Matrix cameraWorldMatrix = camera.Entity.Transform.WorldMatrix;
                    Vector3 raycastStart = cameraWorldMatrix.TranslationVector;
                    Vector3 raycastDirection = cameraWorldMatrix.Forward;
                    float range = ConfiguredItemData.WeaponData.Range;
                    
                    ShootRaycast(raycastStart, raycastDirection, range); // Assuming ShootRaycast uses ConfiguredItemData.WeaponData.Damage
                }
                GameSoundManager.PlaySound("Pistol_Shoot", this.Entity.Transform.WorldMatrix.TranslationVector);
            }
            else
            {
                Log.Info($"{itemName}: Click (empty).");
                GameSoundManager.PlaySound("Pistol_EmptyClick", this.Entity.Transform.WorldMatrix.TranslationVector);
            }
        }

        public override void Reload()
        {
            var itemName = ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "Pistol";
            if (ActualCurrentAmmoInClip >= ClipSize)
            {
                Log.Info($"{itemName}: Clip already full.");
                return;
            }

            if (ConfiguredItemData?.WeaponData?.RequiredAmmoItemID == null)
            {
                Log.Warning($"{itemName}: RequiredAmmoItemID not defined in ConfiguredItemData.WeaponData. Cannot reload.");
                return;
            }

            UpdateReserveAmmoFromInventory(this.Entity.GetParent()); // Ensure reserve ammo count is current

            int ammoNeeded = ClipSize - ActualCurrentAmmoInClip;
            int ammoToConsume = System.Math.Min(ammoNeeded, ActualReserveAmmo);

            if (ammoToConsume > 0)
            {
                if (ConsumeAmmoFromInventory(this.Entity.GetParent(), ammoToConsume))
                {
                    ActualCurrentAmmoInClip += ammoToConsume;
                    // ActualReserveAmmo is updated by ConsumeAmmoFromInventory indirectly via UpdateReserveAmmoFromInventory
                    UpdateReserveAmmoFromInventory(this.Entity.GetParent()); // Re-fetch to confirm
                    Log.Info($"{itemName}: Reloaded. Ammo: {ActualCurrentAmmoInClip}/{ClipSize}. Reserve: {ActualReserveAmmo}");
                    GameSoundManager.PlaySound("Pistol_Reload", this.Entity.Transform.WorldMatrix.TranslationVector);
                }
                else
                {
                    Log.Info($"{itemName}: Failed to consume ammo from inventory for reload.");
                }
            }
            else
            {
                Log.Info($"{itemName}: No reserve ammo to reload ({ActualReserveAmmo} available).");
            }
        }
        
        public override void OnEquip(Entity owner) // owner is the player entity
        {
            base.OnEquip(owner); // Call base to ensure logging or other base logic
            playerInventory = owner?.Get<PlayerInventoryComponent>();

            if (ConfiguredItemData?.WeaponData != null)
            {
                ClipSize = ConfiguredItemData.WeaponData.ClipSize > 0 ? ConfiguredItemData.WeaponData.ClipSize : 7; // Default to 7 if not set
                ActualCurrentAmmoInClip = ClipSize; // Start with a full clip
                UpdateReserveAmmoFromInventory(owner);
                Log.Info($"{ConfiguredItemData.ItemName}: Equipped. Clip: {ActualCurrentAmmoInClip}/{ClipSize}. Reserve: {ActualReserveAmmo}");
            }
            else
            {
                ClipSize = 7; // Default
                ActualCurrentAmmoInClip = ClipSize;
                ActualReserveAmmo = 0;
                Log.Warning($"{this.Entity?.Name ?? "Pistol"}: Equipped, but ConfiguredItemData.WeaponData is null. Using default clip size. Ammo may not function correctly.");
            }
        }

        public override void OnUnequip(Entity owner)
        {
            base.OnUnequip(owner); // Call base to ensure logging or other base logic
            Log.Info($"{ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "Pistol"}: Unequipped.");
            playerInventory = null;
            // State (ActualCurrentAmmoInClip, ActualReserveAmmo) is lost when unequipped.
            // To persist this, it would need to be saved back to an inventory system that can hold instance-specific data,
            // or the weapon entity itself would need to be persisted/pooled instead of recreated.
        }

        private void UpdateReserveAmmoFromInventory(Entity ownerEntity)
        {
            if (playerInventory == null || ConfiguredItemData?.WeaponData?.RequiredAmmoItemID == null)
            {
                ActualReserveAmmo = 0;
                return;
            }

            int count = 0;
            var slots = playerInventory.FindAllItemSlots(ConfiguredItemData.WeaponData.RequiredAmmoItemID);
            foreach (var slotIndex in slots)
            {
                var itemStack = playerInventory.GetItemStack(slotIndex);
                if (itemStack != null)
                {
                    count += itemStack.Quantity;
                }
            }
            ActualReserveAmmo = count;
            // Log.Info($"{ConfiguredItemData.ItemName}: Reserve ammo updated to {ActualReserveAmmo}");
        }

        private bool ConsumeAmmoFromInventory(Entity ownerEntity, int amountToConsume)
        {
            if (playerInventory == null || ConfiguredItemData?.WeaponData?.RequiredAmmoItemID == null || amountToConsume <= 0)
            {
                return false;
            }

            string ammoItemID = ConfiguredItemData.WeaponData.RequiredAmmoItemID;
            int remainingToConsume = amountToConsume;

            var ammoSlots = playerInventory.FindAllItemSlots(ammoItemID);
            if (ammoSlots.Count == 0) return false;

            // Sort slots to consume from earliest first (optional, but can be consistent)
            // ammoSlots.Sort();

            foreach (var slotIndex in ammoSlots)
            {
                ItemStack ammoStack = playerInventory.GetItemStack(slotIndex);
                if (ammoStack != null && ammoStack.Quantity > 0)
                {
                    int canConsumeFromThisStack = System.Math.Min(remainingToConsume, ammoStack.Quantity);
                    if (playerInventory.RemoveItem(slotIndex, canConsumeFromThisStack))
                    {
                        remainingToConsume -= canConsumeFromThisStack;
                        if (remainingToConsume <= 0) break;
                    }
                }
            }

            bool success = remainingToConsume <= 0;
            if(success)
            {
                 Log.Info($"{ConfiguredItemData.ItemName}: Consumed {amountToConsume} of {ammoItemID}.");
            }
            else
            {
                 Log.Warning($"{ConfiguredItemData.ItemName}: Could not consume {amountToConsume} of {ammoItemID}. Only {amountToConsume - remainingToConsume} consumed.");
            }
            UpdateReserveAmmoFromInventory(ownerEntity); // Refresh reserve count
            return success;
        }

        private CameraComponent GetCamera()
        {
            var playerEntity = this.Entity?.GetParent();
            return playerEntity?.Get<PlayerInput>()?.Camera;
        }
    }
}
