// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Engine;
using MySurvivalGame.Game.Items; // For WeaponToolData
using MySurvivalGame.Game.Player; // For PlayerEquipment
using System; // For Math.Min

namespace MySurvivalGame.Game.Weapons
{
    public abstract class BaseRangedWeapon : BaseWeapon
    {
        public int MaxAmmoInClip { get; protected set; }
        public int CurrentAmmoInClip { get; protected set; }
        public int ReserveAmmo { get; protected set; }
        public bool IsReloading { get; protected set; }
        public float ReloadTime { get; protected set; } = 2.0f; // Default reload time
        public float FireRateHz { get; protected set; } = 1.0f; // Shots per second

        private float currentReloadTimer = 0.0f;
        private float fireCooldownTimer = 0.0f;
        protected WeaponToolData ToolData { get; private set; } // Stores the specific data for this weapon

        public override void OnEquip(Entity owner)
        {
            base.OnEquip(owner); // Calls BaseWeapon.OnEquip if it has any logic

            var playerEquipment = owner?.Get<PlayerEquipment>();
            if (playerEquipment != null && playerEquipment.currentlyEquippedItemData is WeaponToolData rangedToolData)
            {
                ToolData = rangedToolData;
                MaxAmmoInClip = ToolData.ClipSize;
                ReserveAmmo = ToolData.ReserveAmmo_Persisted;
                CurrentAmmoInClip = ToolData.CurrentAmmoInClip_Persisted;

                // Special handling for bow-like weapons on equip: auto-nock if possible
                if (MaxAmmoInClip == 1 && CurrentAmmoInClip == 0 && ReserveAmmo > 0)
                {
                    // CurrentAmmoInClip = 1; // Nock one from reserve. Reserve is decremented by Reload().
                    // Let Reload() handle the nocking and reserve deduction if called by PrimaryAction
                }

                // Ensure CurrentAmmoInClip does not exceed MaxAmmoInClip and is not negative
                CurrentAmmoInClip = Math.Max(0, Math.Min(CurrentAmmoInClip, MaxAmmoInClip));

                FireRateHz = ToolData.FireRate > 0 ? ToolData.FireRate : 1.0f;
                fireCooldownTimer = 0.0f; // Ensure cooldown is reset

                Log.Info($"'{ToolData.Name}' equipped. Ammo: {CurrentAmmoInClip}/{ReserveAmmo}. Max Clip: {MaxAmmoInClip}. Reload Time: {ReloadTime}s. FireRate: {FireRateHz}Hz.");
            }
            else
            {
                Log.Error("BaseRangedWeapon: Equipped, but currentlyEquippedItemData is not WeaponToolData or PlayerEquipment not found.");
                ToolData = null; // Ensure ToolData is null if setup fails
                MaxAmmoInClip = 0;
                CurrentAmmoInClip = 0;
                ReserveAmmo = 0;
            }
            IsReloading = false;
            currentReloadTimer = 0f;
        }

        public override void OnUnequip(Entity owner)
        {
            base.OnUnequip(owner); // Calls BaseWeapon.OnUnequip

            if (ToolData != null)
            {
                ToolData.CurrentAmmoInClip_Persisted = CurrentAmmoInClip;
                ToolData.ReserveAmmo_Persisted = ReserveAmmo;
                Log.Info($"'{ToolData.Name}' unequipped. Ammo state persisted: Clip: {ToolData.CurrentAmmoInClip_Persisted}, Reserve: {ToolData.ReserveAmmo_Persisted}.");
            }
            ToolData = null; // Clear reference
        }

        public override void Update() // Assuming BaseWeapon or its parent (ScriptComponent) calls Update
        {
            base.Update(); // Call if BaseWeapon has an Update
            var deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;

            if (IsReloading)
            {
                currentReloadTimer -= deltaTime;
                if (currentReloadTimer <= 0f)
                {
                    int ammoNeeded = MaxAmmoInClip - CurrentAmmoInClip;
                    int ammoToMove = Math.Min(ammoNeeded, ReserveAmmo);

                    CurrentAmmoInClip += ammoToMove;
                    ReserveAmmo -= ammoToMove;
                    IsReloading = false;
                    currentReloadTimer = 0f;
                    Log.Info($"'{ToolData?.Name ?? "Weapon"}' reload complete. Ammo: {CurrentAmmoInClip}/{ReserveAmmo}");
                }
            }

            if (fireCooldownTimer > 0f)
            {
                fireCooldownTimer -= deltaTime;
            }
        }

        public override void PrimaryAction()
        {
            if (ToolData == null)
            {
                Log.Error("BaseRangedWeapon: PrimaryAction called but ToolData is null.");
                return;
            }

            if (IsBroken)
            {
                Log.Info($"'{ToolData.Name}' is broken. Cannot fire.");
                return;
            }

            if (IsReloading)
            {
                Log.Info($"'{ToolData.Name}' is busy reloading.");
                return;
            }

            if (CurrentAmmoInClip <= 0)
            {
                Log.Info($"'{ToolData.Name}' out of ammo in clip.");
                Reload();
                if (!IsReloading && MaxAmmoInClip > 1) // For magazine weapons, if reload didn't start (e.g. no reserve)
                {
                    Log.Info($"'{ToolData.Name}' click empty (no reserve ammo or clip already full).");
                }
                // For bows (MaxAmmoInClip == 1), Reload() is immediate nocking, so no "click empty" here.
                return;
            }

            if (fireCooldownTimer > 0f)
            {
                // Log.Info($"'{ToolData.Name}' still cooling down from previous shot."); // Optional debug
                return; // Don't fire if still cooling down
            }

            Fire();
            CurrentAmmoInClip--;
            ConsumeDurabilityPerShot(); // Consume durability per shot
            fireCooldownTimer = 1.0f / FireRateHz; // Reset cooldown
            Log.Info($"Fired (PrimaryAction) '{ToolData.Name}'. Ammo: {CurrentAmmoInClip}/{ReserveAmmo}. Cooldown set to {fireCooldownTimer:F3}s");

            if (CurrentAmmoInClip <= 0)
            {
                Reload(); // Auto-reload if clip empty after firing
            }
        }

        public virtual void UpdateHeldAction()
        {
            if (ToolData == null) return; // Should not happen if equipped

            if (IsBroken || IsReloading || fireCooldownTimer > 0f)
            {
                // Optional: Log why held action isn't firing, e.g. Log.Info("Held action skipped: broken, reloading, or cooling down.");
                return;
            }

            if (CurrentAmmoInClip <= 0)
            {
                Reload(); // Attempt to reload if out of ammo
                // If reload is immediate (like bow nocking), next UpdateHeldAction might fire.
                // If reload is timed, IsReloading will be true and block next shot.
                return;
            }

            Fire();
            CurrentAmmoInClip--;
            ConsumeDurabilityPerShot(); // Consume durability per shot
            fireCooldownTimer = 1.0f / FireRateHz;
            Log.Info($"Fired (UpdateHeldAction) '{ToolData.Name}'. Ammo: {CurrentAmmoInClip}/{ReserveAmmo}. Cooldown set to {fireCooldownTimer:F3}s");

            if (CurrentAmmoInClip <= 0)
            {
                Reload(); // Auto-reload if clip empty after firing
            }
        }

        public override void Reload()
        {
            if (ToolData == null)
            {
                Log.Error("BaseRangedWeapon: Reload called but ToolData is null.");
                return;
            }

            // Special handling for bow-like single "clip" weapons (nocking)
            if (MaxAmmoInClip == 1)
            {
                if (ReserveAmmo > 0 && CurrentAmmoInClip == 0)
                {
                    CurrentAmmoInClip = 1;
                    ReserveAmmo--; // Consume arrow from reserve when nocking
                    IsReloading = false; // Nocking is immediate
                    Log.Info($"'{ToolData.Name}' nocked an arrow. Reserve: {ReserveAmmo}");
                }
                else if (ReserveAmmo <= 0 && CurrentAmmoInClip == 0)
                {
                    Log.Info($"'{ToolData.Name}' has no arrows to nock.");
                }
                // else: clip is already full (arrow nocked) or no reserve ammo.
                return; // Exit Reload for bow-like weapons after this logic
            }

            // Original reload logic for magazine-based weapons:
            if (IsReloading)
            {
                // Log.Info($"'{ToolData.Name}' is already reloading.");
                return;
            }

            if (ReserveAmmo <= 0)
            {
                Log.Info($"'{ToolData.Name}' has no reserve ammo to reload.");
                return;
            }

            if (CurrentAmmoInClip == MaxAmmoInClip)
            {
                // Log.Info($"'{ToolData.Name}' clip is already full.");
                return;
            }

            IsReloading = true;
            currentReloadTimer = ReloadTime; // Use ReloadTime property
            Log.Info($"'{ToolData.Name}' reloading... ({ReloadTime}s)");
        }

        // To be implemented by derived classes (e.g., hitscan, projectile spawning)
        public virtual void Fire()
        {
            Log.Info($"BaseRangedWeapon.Fire() called for '{ToolData?.Name ?? "Unknown Weapon"}'. Derived class should implement actual firing logic.");
        }

        protected virtual void ConsumeDurabilityPerShot(float cost = 1.0f)
        {
            if (ToolData == null)
            {
                Log.Error("ConsumeDurabilityPerShot: ToolData is null!");
                return;
            }

            if (ToolData.MaxDurabilityPoints <= 0) // Non-durable item
            {
                return;
            }

            // Don't consume if already at 0 (IsBroken should be true)
            // ToolData.UpdateDurability handles setting IsBroken if it reaches 0.
            if (ToolData.DurabilityPoints <= 0f)
            {
                // Log.Info($"ConsumeDurabilityPerShot: Tool '{ToolData.Name}' is already fully broken. No durability consumed.");
                // Ensure BaseWeapon's IsBroken flag is also set if ToolData says it's broken
                if (ToolData.IsBroken) this.IsBroken = true;
                return;
            }

            ToolData.DurabilityPoints -= cost;
            ToolData.UpdateDurability(ToolData.DurabilityPoints); // This updates ToolData.IsBroken and MockInventoryItem.Durability

            // Log.Info($"BaseRangedWeapon: Consumed {cost} durability from '{ToolData.Name}'. Remaining: {ToolData.DurabilityPoints}/{ToolData.MaxDurabilityPoints}. Broken: {ToolData.IsBroken}");

            this.IsBroken = ToolData.IsBroken; // Sync BaseWeapon's IsBroken flag

            if (this.IsBroken && ToolData.DurabilityPoints == 0) // Check if it JUST broke to 0 this shot
            {
                Log.Warning($"BaseRangedWeapon: Item '{ToolData.Name}' just broke from firing!");
            }
        }
    }
}
