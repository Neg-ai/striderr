// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Core.Mathematics;
using Stride.Engine;
using MySurvivalGame.Game.Items; // For WeaponToolData
using MySurvivalGame.Game.Player; // For PlayerInput

namespace MySurvivalGame.Game.Weapons
{
    public class BowWeapon : BaseRangedWeapon
    {
        public float ArrowSpeed { get; set; } = 50f; // Default speed, can be overridden by ToolData

        public override void OnEquip(Entity owner)
        {
            base.OnEquip(owner); // This now handles setting up ToolData, MaxAmmoInClip, CurrentAmmoInClip, ReserveAmmo

            if (ToolData != null)
            {
                if (ToolData.ProjectileSpeed > 0) // Check if a specific speed is set in data
                {
                    ArrowSpeed = ToolData.ProjectileSpeed;
                }
                // Ensure bow-specific ammo logic (MaxClip = 1, auto-nock if possible)
                // BaseRangedWeapon.OnEquip and Reload now handle this better.
                // Specifically, Reload() will nock an arrow if MaxAmmoInClip is 1.
                // And PrimaryAction() calls Reload() if CurrentAmmoInClip is 0.
                // We might want to ensure an arrow is nocked on equip if reserve allows and clip was persisted as 0.
                if (MaxAmmoInClip == 1 && CurrentAmmoInClip == 0 && ReserveAmmo > 0)
                {
                    // Call Reload to nock an arrow. Reload now handles decrementing ReserveAmmo for bows.
                    Reload();
                }
            }
            else
            {
                Log.Error("BowWeapon.OnEquip: ToolData is null after base.OnEquip. Cannot set ArrowSpeed or verify ammo state.");
            }
        }

        public override void Fire()
        {
            // base.Fire(); // Calls the log in BaseRangedWeapon, not strictly needed here unless for layered debugging.
            Log.Info($"BowWeapon attempting to fire '{ToolData?.Name ?? "Bow"}'. ArrowSpeed: {ArrowSpeed}");

            if (OwnerEntity == null)
            {
                Log.Error("BowWeapon.Fire: OwnerEntity is null.");
                return;
            }

            var playerInput = OwnerEntity.Get<PlayerInput>();
            var camera = playerInput?.Camera;

            if (camera == null)
            {
                Log.Error("BowWeapon.Fire: Camera not found on Player via PlayerInput script!");
                return;
            }

            if (ToolData == null)
            {
                Log.Error("BowWeapon.Fire: ToolData is null. Cannot determine damage.");
                return;
            }

            var cameraMatrix = camera.Entity.Transform.WorldMatrix;
            // Spawn projectile slightly in front of the camera/player to avoid immediate self-collision
            Vector3 spawnPosition = cameraMatrix.TranslationVector + cameraMatrix.Forward * 1.0f;
            Vector3 fireDirection = cameraMatrix.Forward;

            var projectilePrefab = Content.Load<Prefab>("ArrowProjectilePrefab");
            if (projectilePrefab == null)
            {
                Log.Error("BowWeapon.Fire: Could not load ArrowProjectilePrefab.");
                return;
            }

            var projectileEntityInstance = projectilePrefab.Instantiate().FirstOrDefault();
            if (projectileEntityInstance == null)
            {
                 Log.Error("BowWeapon.Fire: Failed to instantiate ArrowProjectilePrefab.");
                return;
            }

            var projectileScript = projectileEntityInstance.Get<Projectile>();
            if (projectileScript != null)
            {
                projectileScript.InitialVelocity = fireDirection * ArrowSpeed;
                projectileScript.Damage = ToolData.Damage; // Damage comes from Bow's data
                projectileScript.ShooterEntity = this.OwnerEntity; // The Player entity
                projectileScript.LifespanSeconds = 5.0f; // Could also be part of ToolData for different arrow types
                projectileScript.GravityFactor = 1.0f;   // Standard gravity
                // HitDetectionRadius could also be on ToolData if different arrow types have different hitboxes

                projectileEntityInstance.Transform.Position = spawnPosition;
                // Projectile's Start() method will handle initial rotation based on InitialVelocity

                Entity.Scene.Entities.Add(projectileEntityInstance);
                Log.Info($"BowWeapon fired. Projectile '{projectileEntityInstance.Name}' spawned and launched. InitialSpeed: {ArrowSpeed}");
            }
            else
            {
                Log.Error("BowWeapon.Fire: Projectile script not found on ArrowProjectilePrefab's instantiated entity.");
                // Clean up unconfigured projectile entity
                Entity.Scene.Entities.Remove(projectileEntityInstance);
            }

            // CurrentAmmoInClip is decremented by BaseRangedWeapon.PrimaryAction() after this Fire() call.
            // If CurrentAmmoInClip becomes 0, BaseRangedWeapon.PrimaryAction() will call Reload(),
            // which for the Bow (MaxAmmoInClip == 1) should nock another arrow if ReserveAmmo > 0.
        }

        // PrimaryAction is inherited from BaseRangedWeapon.
        // Reload is inherited but overridden in BaseRangedWeapon for bow-specific (MaxClip==1) nocking.
    }
}
