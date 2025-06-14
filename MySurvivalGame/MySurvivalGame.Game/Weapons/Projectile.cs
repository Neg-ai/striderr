// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Physics;
using MySurvivalGame.Game.Combat; // For HealthComponent
using System.Collections.Generic;
using System.Linq;

namespace MySurvivalGame.Game.Weapons
{
    public class Projectile : SyncScript
    {
        // Public Properties
        public Vector3 InitialVelocity { get; set; }
        public float Damage { get; set; } = 10f;
        public float LifespanSeconds { get; set; } = 5.0f;
        public Entity ShooterEntity { get; set; }
        public float GravityFactor { get; set; } = 1.0f;
        public float HitDetectionRadius { get; set; } = 0.1f;
        public bool IsExplosive { get; set; } = false;
        public float ExplosionRadius { get; set; } = 3.0f;
        public bool DamageFalloff { get; set; } = true; // Optional: to enable/disable damage falloff

        // Private Fields
        private float currentLifespanTimer;
        private Vector3 currentVelocity;

        public override void Start()
        {
            currentLifespanTimer = LifespanSeconds;
            currentVelocity = InitialVelocity;

            // Orient projectile to face its initial velocity direction, only if velocity is not zero.
            if (InitialVelocity.LengthSquared() > 0.001f)
            {
                Entity.Transform.Rotation = Quaternion.LookRotation(InitialVelocity, Vector3.UnitY);
            }
        }

        public override void Update()
        {
            var deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;

            // Lifespan
            currentLifespanTimer -= deltaTime;
            if (currentLifespanTimer <= 0f)
            {
                // Log.Info("Projectile lifespan expired."); // Optional: for debugging
                if (IsExplosive)
                {
                    Explode();
                }
                else
                {
                    Entity.Scene?.Entities.Remove(Entity);
                }
                return;
            }

            // Gravity
            // Ensure there's a physics simulation before applying gravity if using global physics settings.
            // For simplicity, direct application:
            currentVelocity.Y -= 9.81f * GravityFactor * deltaTime;

            // Movement
            var previousPosition = Entity.Transform.Position;
            Entity.Transform.Position += currentVelocity * deltaTime;

            // Keep projectile oriented towards its velocity vector
            if (currentVelocity.LengthSquared() > 0.001f)
            {
                Entity.Transform.Rotation = Quaternion.LookRotation(currentVelocity, Vector3.UnitY);
            }

            // Collision Detection
            var simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error("Projectile: Physics simulation not found.");
                return;
            }

            var hitSphere = new SphereColliderShape(is2D: false, radius: HitDetectionRadius); // Ensure is2D is false for 3D

            List<HitResult> hitResults = new List<HitResult>();

            // Prepare ignored colliders list
            List<EntityComponent> ignoredColliders = new List<EntityComponent>();
            if (ShooterEntity != null)
            {
                var shooterCharacter = ShooterEntity.Get<CharacterComponent>();
                if (shooterCharacter != null)
                {
                    ignoredColliders.Add(shooterCharacter);
                }
                // Potentially ignore other colliders on the shooter entity if needed
            }
            // Also ignore the projectile's own collider if it has one (though for a sweep, it's the shape being moved)

            // Perform the sweep
            // Note: ShapeSweep's 'to' is a matrix. We are sweeping from previous to current position.
            simulation.ShapeSweep(hitSphere,
                                  Matrix.Translation(previousPosition),
                                  Matrix.Translation(Entity.Transform.Position),
                                  hitResults,
                                  CollisionFilterGroups.Default, // Or specific projectile collision group
                                  CollisionFilterGroupFlags.Default, // What it collides with
                                  ignoredColliders.Any() ? ignoredColliders : null);


            var actualHit = hitResults.FirstOrDefault(h => h.Collider?.Entity != null && h.Collider.Entity != ShooterEntity && h.Collider.Entity != this.Entity);

            if (actualHit.Collider != null) // Check Collider directly from actualHit, not actualHit.Succeeded as FirstOrDefault might return default HitResult
            {
                if (IsExplosive)
                {
                    Explode();
                }
                else // Non-explosive direct hit logic
                {
                    var targetEntity = actualHit.Collider.Entity;
                    Log.Info($"Projectile direct hit: {targetEntity.Name} with tag {actualHit.Collider.CollisionGroup} at distance {actualHit.Distance}");

                    var healthComponent = targetEntity.Get<HealthComponent>();
                    if (healthComponent != null)
                    {
                        Log.Info($"Applying {Damage} damage to {targetEntity.Name}.");
                        healthComponent.TakeDamage(Damage, ShooterEntity);
                    }

                    // Conceptual: Trigger impact VFX/sound at actualHit.Point
                    // Example: SpawnImpactEffect(actualHit.Point, actualHit.Normal);

                    Entity.Scene?.Entities.Remove(Entity); // Destroy projectile on direct hit
                }
                return; // Stop further processing for this projectile this frame
            }
        }

        private void Explode()
        {
            Log.Info($"Projectile '{Entity.Name}' exploding at {Entity.Transform.Position} with radius {ExplosionRadius}. Base Damage: {Damage}");

            var simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error("Explode: Simulation not found!");
                Entity.Scene?.Entities.Remove(Entity); // Still remove projectile
                return;
            }

            List<HitResult> hitResults = new List<HitResult>();
            // Note: OverlapSphere doesn't provide normals or exact hit points for each entity,
            // just that their colliders overlap the sphere.
            simulation.OverlapSphere(Entity.Transform.Position, ExplosionRadius, hitResults, CollisionFilterGroups.Default, CollisionFilterGroupFlags.Default);

            // Conceptual: Spawn explosion VFX/sound at Entity.Transform.Position
            // Example: EffectsManager.SpawnExplosionVFX(Entity.Transform.Position, ExplosionRadius);

            HashSet<Entity> damagedEntities = new HashSet<Entity>(); // To ensure each entity is damaged only once per explosion

            foreach (var hit in hitResults) // Renamed hitResult to hit for clarity in this loop
            {
                var targetEntity = hit.Collider?.Entity;

                // Basic checks to avoid issues
                if (targetEntity == null || targetEntity == this.Entity || targetEntity == ShooterEntity || damagedEntities.Contains(targetEntity))
                {
                    continue;
                }

                var healthComponent = targetEntity.Get<HealthComponent>();
                if (healthComponent != null)
                {
                    float actualDamage = Damage;
                    if (DamageFalloff)
                    {
                        // Using targetEntity.Transform.Position for distance. For more accuracy, use hit.Point if available from a more detailed query,
                        // or the closest point on the collider to the explosion center. OverlapSphere doesn't give a specific hit point per entity.
                        float distance = Vector3.Distance(Entity.Transform.Position, targetEntity.Transform.Position); // Or hit.Collider.ClosestPoint(Entity.Transform.Position)
                        float falloffFactor = MathUtil.Clamp(1.0f - (distance / ExplosionRadius), 0f, 1.0f); // Linear falloff
                        actualDamage *= falloffFactor;
                    }

                    if (actualDamage > 0) // Only apply if damage is still positive after falloff
                    {
                        healthComponent.TakeDamage(actualDamage, ShooterEntity);
                        damagedEntities.Add(targetEntity);
                        Log.Info($"Explosion damaged '{targetEntity.Name}' for {actualDamage:F2} HP (Distance: {Vector3.Distance(Entity.Transform.Position, targetEntity.Transform.Position):F2}).");
                    }
                }
            }
            Entity.Scene?.Entities.Remove(Entity); // Destroy projectile after explosion logic
        }
    }
}
