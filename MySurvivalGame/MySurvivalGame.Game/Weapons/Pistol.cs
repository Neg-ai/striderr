// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Physics;
using MySurvivalGame.Game.Combat; // For HealthComponent
using MySurvivalGame.Game.Player; // For PlayerInput to get Camera

namespace MySurvivalGame.Game.Weapons
{
    public class Pistol : BaseRangedWeapon
    {
        public override void Fire()
        {
            // base.Fire(); // Calls the log in BaseRangedWeapon, can be kept for debugging if needed.
            // Log.Info($"Pistol specific Fire() method called for '{ToolData?.Name ?? "Pistol"}'.");

            if (OwnerEntity == null)
            {
                Log.Error("Pistol.Fire: OwnerEntity is null. Cannot determine firing direction.");
                return;
            }

            var playerInput = OwnerEntity.Get<PlayerInput>();
            var camera = playerInput?.Camera; // PlayerInput script should have a public CameraComponent Camera {get; set;}

            if (camera == null)
            {
                Log.Error("Pistol.Fire: Camera not found on Player! Make sure PlayerInput script has Camera property set.");
                return;
            }

            if (ToolData == null)
            {
                Log.Error("Pistol.Fire: ToolData is null. Cannot determine range or damage.");
                return;
            }

            var cameraMatrix = camera.Entity.Transform.WorldMatrix; // Use the camera's entity transform
            Vector3 raycastStart = cameraMatrix.TranslationVector;
            Vector3 raycastDirection = cameraMatrix.Forward;
            float range = ToolData.Range > 0 ? ToolData.Range : 50f; // Use ToolData.Range, fallback to 50f
            float damage = ToolData.Damage > 0 ? ToolData.Damage : 10f; // Use ToolData.Damage, fallback to 10f

            var simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error("Pistol.Fire: Physics simulation not found.");
                return;
            }

            // Perform raycast, ignore owner's character collider
            var characterCollider = OwnerEntity.Get<CharacterComponent>();
            var ignoredColliders = characterCollider != null ? new System.Collections.Generic.List<EntityComponent> { characterCollider } : null;
            var hitResult = simulation.Raycast(raycastStart, raycastStart + raycastDirection * range, ignoredColliders: ignoredColliders);

            if (hitResult.Succeeded)
            {
                var targetEntity = hitResult.Collider.Entity;
                Log.Info($"Pistol fired. Hit: {targetEntity?.Name ?? "Unnamed Entity"} at distance {hitResult.Distance}. Applying {damage} damage.");

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
                Log.Info("Pistol fired. Miss.");
            }
            // Conceptual: Trigger muzzle flash VFX/sound from pistol's location/muzzle point
            // Example: EffectsManager.SpawnMuzzleFlash(this.Entity); // Assuming this script is on the Pistol's entity
        }
    }
}
