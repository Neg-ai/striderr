// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Physics;
using MySurvivalGame.Game.Items; // For WeaponToolData
using MySurvivalGame.Game.Player; // For PlayerEquipment
using MySurvivalGame.Game.Combat; // For HealthComponent
using MySurvivalGame.Game.World;  // For ResourceNodeComponent

namespace MySurvivalGame.Game.Weapons
{
    public abstract class BaseMeleeWeapon : BaseWeapon
    {
        protected Entity OwnerEntity { get; private set; }
        protected WeaponToolData CurrentToolData { get; private set; }

        public override void OnEquip(Entity owner)
        {
            OwnerEntity = owner;
            var playerEquipment = OwnerEntity?.Get<PlayerEquipment>();
            if (playerEquipment != null)
            {
                CurrentToolData = playerEquipment.currentlyEquippedItemData; // Get the specific data
                if (CurrentToolData != null)
                {
                    Log.Info($"BaseMeleeWeapon '{CurrentToolData.Name}' equipped by {owner.Name}. Damage: {CurrentToolData.Damage}, Range: {CurrentToolData.Range}");
                }
                else
                {
                    Log.Error("BaseMeleeWeapon: Equipped, but currentlyEquippedItemData is null in PlayerEquipment.");
                }
            }
            else
            {
                Log.Error("BaseMeleeWeapon: PlayerEquipment component not found on owner entity.");
            }
            // Future: Attach model to hand, etc.
        }

        public override void OnUnequip(Entity owner)
        {
            if (CurrentToolData != null)
            {
                Log.Info($"BaseMeleeWeapon '{CurrentToolData.Name}' unequipped by {owner.Name}.");
            }
            OwnerEntity = null;
            CurrentToolData = null;
            // Future: Detach model, etc.
        }

        public override void PrimaryAction()
        {
            if (OwnerEntity == null || CurrentToolData == null)
            {
                Log.Warning("BaseMeleeWeapon: PrimaryAction called but OwnerEntity or CurrentToolData is null.");
                return;
            }

            if (CurrentToolData.IsBroken)
            {
                Log.Info($"BaseMeleeWeapon: Cannot use primary action, {CurrentToolData.Name} is broken.");
                return;
            }

            Log.Info($"BaseMeleeWeapon: {CurrentToolData.Name} PrimaryAction triggered. Range: {CurrentToolData.Range}, Damage: {CurrentToolData.Damage}");

            var simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error("BaseMeleeWeapon: Physics simulation not found.");
                return;
            }

            // Determine raycast start and end points
            // Using camera's forward vector for aiming direction.
            // A more accurate melee might use specific hand bone + weapon model direction.
            var camera = OwnerEntity.Get<PlayerInput>()?.Camera; // Assuming PlayerInput has CameraComponent reference
            if (camera == null)
            {
                Log.Error("BaseMeleeWeapon: Camera not found on Player for raycasting direction.");
                return;
            }

            Matrix cameraWorldMatrix = camera.Entity.Transform.WorldMatrix;
            Vector3 raycastStart = cameraWorldMatrix.TranslationVector;
            Vector3 raycastForward = cameraWorldMatrix.Forward;
            Vector3 raycastEnd = raycastStart + raycastForward * CurrentToolData.Range;

            // Perform a raycast. A short sphere sweep might be better for melee hit detection.
            // For simplicity, using Raycast for now.
            var hitResult = simulation.Raycast(raycastStart, raycastEnd, ignoredColliders: new List<EntityComponent> { OwnerEntity.Get<CharacterComponent>() });

            if (hitResult.Succeeded)
            {
                var hitEntity = hitResult.Collider.Entity;
                Log.Info($"BaseMeleeWeapon: Hit entity '{hitEntity.Name}' at distance {hitResult.Distance}.");

                // Check for ResourceNodeComponent
                var resourceNode = hitEntity.Get<ResourceNodeComponent>();
                if (resourceNode != null)
                {
                    var playerInventory = OwnerEntity.Get<PlayerInventoryComponent>();
                    if (playerInventory != null)
                    {
                        Log.Info($"BaseMeleeWeapon: Hitting ResourceNode '{hitEntity.Name}' with '{CurrentToolData.Name}'.");
                        // PlayerEquipment handles durability consumption for tool use during gathering.
                        // Here, we just inform the node it was hit by this tool.
                        // The HitNode method in ResourceNodeComponent will determine if this tool is effective.
                        resourceNode.HitNode(CurrentToolData, playerInventory);
                    }
                }

                // Check for HealthComponent (for combat)
                var healthComponent = hitEntity.Get<HealthComponent>();
                if (healthComponent != null)
                {
                    Log.Info($"BaseMeleeWeapon: Attacking entity '{hitEntity.Name}' with '{CurrentToolData.Name}' for {CurrentToolData.Damage} damage.");
                    // In a real system, you'd pass damage type, source entity, etc.
                    healthComponent.TakeDamage(CurrentToolData.Damage);
                }
            }
            else
            {
                Log.Info($"BaseMeleeWeapon: {CurrentToolData.Name} swing missed.");
            }
        }

        public override void SecondaryAction()
        {
            if (CurrentToolData == null) return;
            Log.Info($"BaseMeleeWeapon: {CurrentToolData.Name} SecondaryAction (e.g., block, heavy attack).");
            // Placeholder for specific secondary actions
        }

        public override void Reload()
        {
            // Melee weapons typically don't reload.
            if (CurrentToolData == null) return;
            Log.Info($"BaseMeleeWeapon: {CurrentToolData.Name} - No reload action.");
        }
    }
}
