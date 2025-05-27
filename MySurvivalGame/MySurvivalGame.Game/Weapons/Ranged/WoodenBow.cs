using Stride.Engine;
using Stride.Core.Mathematics;
using MySurvivalGame.Game.Player;       // For PlayerInventoryComponent, PlayerInput
using MySurvivalGame.Data.Items;        // MODIFIED: For ItemData
using MySurvivalGame.Game.Core;         // For DamageType
using MySurvivalGame.Game.Audio;        // For GameSoundManager

namespace MySurvivalGame.Game.Weapons.Ranged
{
    public class WoodenBow : BaseBowWeapon // BaseBowWeapon inherits from BaseWeapon
    {
        private int currentAmmoInInventory = 0;
        private PlayerInventoryComponent playerInventory;

        public override void OnEquip(Entity owner) // owner is the player entity
        {
            base.OnEquip(owner); 
            playerInventory = owner?.Get<PlayerInventoryComponent>();

            if (ConfiguredItemData?.WeaponData != null)
            {
                UpdateAmmoCountFromInventory(owner);
                Log.Info($"{ConfiguredItemData.ItemName}: Equipped. Required Ammo ID: '{ConfiguredItemData.WeaponData.RequiredAmmoItemID}'. Found in Inv: {currentAmmoInInventory}");
            }
            else
            {
                Log.Warning($"{this.Entity?.Name ?? "WoodenBow"}: Equipped, but ConfiguredItemData.WeaponData is null. Ammo functionality will be impaired.");
                currentAmmoInInventory = 0;
            }
        }

        public override void OnUnequip(Entity owner)
        {
            base.OnUnequip(owner);
            playerInventory = null;
            Log.Info($"{ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "WoodenBow"}: Unequipped.");
        }
        
        protected override void ReleaseArrow(float chargeTime) // This method is called by BaseBowWeapon's PrimaryActionReleased
        {
            var itemName = ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "WoodenBow";
            if (ConfiguredItemData?.WeaponData == null)
            {
                Log.Error($"{itemName}: WeaponData not configured. Cannot fire.");
                return;
            }

            if (playerInventory == null)
            {
                Log.Error($"{itemName}: PlayerInventoryComponent not found. Cannot check/consume ammo.");
                return;
            }
            
            UpdateAmmoCountFromInventory(this.Entity.GetParent()); // Refresh ammo count before trying to consume

            string requiredAmmoId = ConfiguredItemData.WeaponData.RequiredAmmoItemID;
            if (string.IsNullOrEmpty(requiredAmmoId))
            {
                 Log.Warning($"{itemName}: No RequiredAmmoItemID specified in WeaponData.");
                 // Optionally, allow firing a default "no-ammo" arrow or prevent firing.
                 // For now, let's prevent firing if ammo ID is not set.
                 return;
            }

            if (ConsumeArrowFromInventory(requiredAmmoId))
            {
                Log.Info($"{itemName}: Arrow fired with charge {chargeTime:F2}s.");
                GameSoundManager.PlaySound("Bow_Shoot", Entity.Transform.WorldMatrix.TranslationVector);

                var camera = GetCamera();
                if (camera != null)
                {
                    Matrix cameraWorldMatrix = camera.Entity.Transform.WorldMatrix;
                    Vector3 raycastStart = cameraWorldMatrix.TranslationVector;
                    Vector3 raycastDirection = cameraWorldMatrix.Forward;
                    
                    // Stats from ConfiguredItemData
                    float range = ConfiguredItemData.WeaponData.Range; 
                    float damage = ConfiguredItemData.WeaponData.Damage * CalculateChargeBonus(chargeTime); // Example charge bonus

                    var simulation = this.GetSimulation();
                    var hitResult = simulation.Raycast(raycastStart, raycastStart + raycastDirection * range);
                    if (hitResult.Succeeded && hitResult.Collider?.Entity.Get<IDamageable>() != null)
                    {
                        Log.Info($"{itemName}: Hit {hitResult.Collider.Entity.Name}. Applying {damage} damage.");
                        hitResult.Collider.Entity.Get<IDamageable>().TakeDamage(damage, DamageType.Ranged);
                    } else if (hitResult.Succeeded) {
                        Log.Info($"{itemName}: Hit {hitResult.Collider.Entity.Name}, but it's not Damageable.");
                    } else {
                        Log.Info($"{itemName}: Arrow missed.");
                    }
                }
                UpdateAmmoCountFromInventory(this.Entity.GetParent()); // Update count after firing
            }
            else
            {
                Log.Info($"{itemName}: No '{requiredAmmoId}' in inventory!");
                GameSoundManager.PlaySound("Bow_Empty", Entity.Transform.WorldMatrix.TranslationVector);
            }
        }

        private float CalculateChargeBonus(float chargeTime)
        {
            // Example: Simple charge bonus, max 2x damage at 1s charge
            return 1.0f + MathUtil.Clamp(chargeTime, 0, 1.0f);
        }

        private void UpdateAmmoCountFromInventory(Entity ownerEntity)
        {
            if (playerInventory == null || ConfiguredItemData?.WeaponData?.RequiredAmmoItemID == null)
            {
                currentAmmoInInventory = 0;
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
            currentAmmoInInventory = count;
        }

        private bool ConsumeArrowFromInventory(string ammoItemId)
        {
            if (playerInventory == null) return false;

            var ammoSlots = playerInventory.FindAllItemSlots(ammoItemId);
            if (ammoSlots.Count > 0)
            {
                // Consume one arrow from the first available stack
                if (playerInventory.RemoveItem(ammoSlots[0], 1))
                {
                    return true;
                }
            }
            return false;
        }

        private CameraComponent GetCamera()
        {
            // Assumes this script is on a weapon entity child of the player entity
            var playerEntity = this.Entity?.GetParent(); 
            return playerEntity?.Get<PlayerInput>()?.Camera;
        }
        
        // BaseBowWeapon handles PrimaryAction to start charging, and OnPrimaryActionReleased to call ReleaseArrow.
        // So, PrimaryAction in WoodenBow itself might not be needed if charge mechanic is in BaseBowWeapon.
        // public override void PrimaryAction() { /* Potentially start drawing the bow */ }

        public override void SecondaryAction() { Log.Info($"{ConfiguredItemData?.ItemName ?? Entity.Name}: Secondary Action (e.g. Aim TBD)."); }
        public override void Reload() { Log.Info($"{ConfiguredItemData?.ItemName ?? Entity.Name}: Does not reload traditionally. Ammo count: {currentAmmoInInventory}"); }
    }
}
