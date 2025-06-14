// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Physics;
using MySurvivalGame.Game.Combat; // For HealthComponent
using MySurvivalGame.Game.Player; // For PlayerInput to get Camera
using System.Collections.Generic; // For List in Raycast ignoredColliders

namespace MySurvivalGame.Game.Weapons
{
    public class AssaultRifle : BaseRangedWeapon
    {
        public override void Fire()
        {
            // base.Fire(); // Calls the log in BaseRangedWeapon

            if (OwnerEntity == null)
            {
                Log.Error("AssaultRifle.Fire: OwnerEntity is null. Cannot determine firing direction.");
                return;
            }

            var playerInput = OwnerEntity.Get<PlayerInput>();
            // PlayerInput script should have a public CameraComponent Camera {get; set;}
            // This Camera property is assigned in Stride editor or by another script that manages camera setup.
            var camera = playerInput?.Camera;

            if (camera == null)
            {
                Log.Error("AssaultRifle.Fire: Camera not found on Player via PlayerInput script! Ensure Camera property is set on PlayerInput.");
                return;
            }

            if (ToolData == null)
            {
                Log.Error("AssaultRifle.Fire: ToolData is null. Cannot determine range or damage.");
                return;
            }

            var cameraMatrix = camera.Entity.Transform.WorldMatrix; // Use the camera's entity transform
            Vector3 raycastStart = cameraMatrix.TranslationVector;
            Vector3 raycastDirection = cameraMatrix.Forward;

            // Use ToolData for range and damage, with sensible fallbacks if data is missing (though ToolData should always be present for an equipped weapon)
            float range = ToolData.Range > 0 ? ToolData.Range : 70f;
            float damage = ToolData.Damage > 0 ? ToolData.Damage : 15f;

            var simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error("AssaultRifle.Fire: Physics simulation not found.");
                return;
            }

            var characterCollider = OwnerEntity.Get<CharacterComponent>();
            // Raycast normally ignores the collider it starts inside of, but explicitly ignoring CharacterComponent is safer.
            var ignoredCollidersList = characterCollider != null ? new List<EntityComponent> { characterCollider } : null;

            var hitResult = simulation.Raycast(raycastStart, raycastStart + raycastDirection * range, ignoredColliders: ignoredCollidersList);

            if (hitResult.Succeeded)
            {
                var targetEntity = hitResult.Collider.Entity;
                Log.Info($"Assault Rifle fired. Hit: {targetEntity?.Name ?? "Unnamed Entity"} at distance {hitResult.Distance}. Applying {damage} damage.");

                var healthComponent = targetEntity?.Get<HealthComponent>();
                if (healthComponent != null)
                {
                    healthComponent.TakeDamage(damage, OwnerEntity); // Pass OwnerEntity as damager
                }
                // Conceptual: Trigger impact VFX/sound at hitResult.Point
                // Example: EffectsManager.SpawnImpactEffect(hitResult.Point, hitResult.Normal);
            }
            else
            {
                Log.Info($"Assault Rifle fired. Miss. (Range: {range})");
            }
            // Conceptual: Trigger muzzle flash VFX/sound from weapon's location/muzzle point
            // Example: EffectsManager.SpawnMuzzleFlash(this.Entity); // Assuming this script is on the weapon's entity
        }

        // PrimaryAction() and UpdateHeldAction() are inherited from BaseRangedWeapon.
        // BaseRangedWeapon will call this overridden Fire() method.
        // Reload() is also inherited and should work as expected for a magazine-based weapon.
    }
}
