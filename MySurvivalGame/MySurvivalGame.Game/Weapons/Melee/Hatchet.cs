using Stride.Engine;
using Stride.Core.Mathematics; // For Vector3, Matrix
using Stride.Physics;         // For Simulation, HitResult, SphereColliderShape
using MySurvivalGame.Game.Core; // For IDamageable, DamageType
using MySurvivalGame.Game.Player; // For PlayerInput (PlayerEquipment is not directly accessed for item data anymore)
using MySurvivalGame.Data.Items;  // For ItemData, ToolStats, WeaponStats
using System.Collections.Generic; // For List<HitResult>
using MySurvivalGame.Game.Audio; // ADDED for GameSoundManager

namespace MySurvivalGame.Game.Weapons.Melee
{
    public class Hatchet : BaseMeleeWeapon
    {
        public override void PrimaryAction()
        {
            var thisEntityName = this.Entity?.Name ?? "Hatchet";
            var thisEntityName = this.Entity?.Name ?? "Hatchet";
            
            if (ConfiguredItemData == null)
            {
                Log.Error($"{thisEntityName}: ConfiguredItemData is null. Cannot perform primary action.");
                return;
            }
            Log.Info($"{thisEntityName} ({ConfiguredItemData.ItemName}): Swung.");
            GameSoundManager.PlaySound("Hatchet_Swing", this.Entity.Transform.WorldMatrix.TranslationVector);
            
            // Get relevant data from ConfiguredItemData
            // A hatchet is primarily a tool, but might have weapon stats as a fallback or if it's a combat hatchet.
            float damage = ConfiguredItemData.ToolData?.Damage ?? ConfiguredItemData.WeaponData?.Damage ?? 1.0f;
            float meleeRange = ConfiguredItemData.ToolData?.Range ?? ConfiguredItemData.WeaponData?.Range ?? 1.5f;
            float meleeRadius = 0.3f;

            // Get PlayerInput and Camera from the parent (Player entity)
            var playerEntity = this.Entity?.GetParent(); // Assuming this script is on a child entity of the player
            if (playerEntity == null)
            {
                Log.Error($"{thisEntityName}: Could not find player entity (parent).");
                return;
            }
            var playerInput = playerEntity.Get<PlayerInput>();
            if (playerInput == null || playerInput.Camera == null)
            {
                Log.Error($"{thisEntityName}: PlayerInput or Camera not found on player entity '{playerEntity.Name}'.");
                return;
            }
            var camera = playerInput.Camera;

            var simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error($"{thisEntityName}: Physics simulation not found.");
                return;
            }

            Matrix cameraWorldMatrix = camera.Entity.Transform.WorldMatrix;
            Vector3 castStart = cameraWorldMatrix.TranslationVector;
            Vector3 castEnd = castStart + cameraWorldMatrix.Forward * meleeRange;

            var sphereShape = new SphereColliderShape(false, meleeRadius);
            
            HitResult closestHit = simulation.ShapeSweep(sphereShape, Matrix.Translation(castStart), Matrix.Translation(castEnd), 
                                                            filterGroup: CollisionFilterGroups.DefaultFilter, 
                                                            filterFlags: CollisionFilterGroupFlags.DefaultFilter);

            if (closestHit.Succeeded && closestHit.Collider != null)
            {
                var hitEntity = closestHit.Collider.Entity;
                Log.Info($"{thisEntityName}: Hit '{hitEntity.Name}'.");

                var damageable = hitEntity.Get<IDamageable>();
                if (damageable != null)
                {
                    Log.Info($"{thisEntityName}: Applying {damage} Melee damage to '{hitEntity.Name}'.");
                    damageable.TakeDamage(damage, DamageType.Melee);
                }
                else
                {
                    Log.Info($"{thisEntityName}: Hit entity '{hitEntity.Name}' is not IDamageable.");
                }
            }
            else
            {
                Log.Info($"{thisEntityName}: Swing missed.");
            }
        }

        public override void SecondaryAction()
        {
            var itemName = ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "Hatchet";
            Log.Info($"{itemName}: Secondary Action (e.g., block or stronger swing - TBD).");
        }

        public override void Reload()
        {
            var itemName = ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "Hatchet";
            Log.Info($"{itemName}: Has no reload action.");
        }

        public override void OnEquip(Entity owner)
        {
            var itemName = ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "Hatchet";
            Log.Info($"{itemName}: Equipped by {owner?.Name}.");
            // Future: Change player animations, parent model to hand, etc.
            // This is also where you might set initial state based on ConfiguredItemData if not done in Configure.
        }

        public override void OnUnequip(Entity owner)
        {
            var itemName = ConfiguredItemData?.ItemName ?? this.Entity?.Name ?? "Hatchet";
            Log.Info($"{itemName}: Unequipped by {owner?.Name}.");
            // Future: Revert player animations
        }
    }
}
