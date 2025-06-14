// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;
using Stride.Engine.Events; 
using MySurvivalGame.Game.Weapons; 
using MySurvivalGame.Game.Player;   
using MySurvivalGame.Game.Items; 
using MySurvivalGame.Game.World; // ADDED: For ResourceNodeComponent
using Stride.Physics; // ADDED: For Raycasting
using Stride.Core.Mathematics; // ADDED: For Matrix and Vector3

namespace MySurvivalGame.Game.Player 
{
    /// <summary>
    /// Manages the equipment of a player, specifically their current weapon.
    /// Also handles relaying input actions to the equipped weapon.
    /// </summary>
    public class PlayerEquipment : ScriptComponent
    {
        /// <summary>
        /// Gets the currently equipped weapon.
        /// </summary>
        public BaseWeapon CurrentWeapon { get; private set; }
        private MySurvivalGame.Game.Items.WeaponToolData currentlyEquippedItemData; 
        private Entity currentWeaponEntity;
        private Entity weaponHoldPoint;

        // Event receivers for actions
        private EventReceiver primaryActionEventReceiver; // For initial press
        private EventReceiver primaryActionHeldReceiver;    // For continuous hold
        private EventReceiver primaryActionReleasedReceiver; // For release
        private bool isPrimaryActionHeldDown = false;       // Tracks if the primary action button is being held

        private EventReceiver secondaryActionEventReceiver;
        private EventReceiver interactReceiver; // For resource gathering, etc.

        // REMOVED: Building related event receivers
        // private EventReceiver toggleBuildModeEventReceiver;
        // private EventReceiver rotateBuildLeftEventReceiver;
        // private EventReceiver rotateBuildRightEventReceiver;
        // private EventReceiver cycleBuildableNextEventReceiver;
        // private EventReceiver cycleBuildablePrevEventReceiver;
        // private EventReceiver debugDestroyEventReceiver;

        // REMOVED: Building controller reference
        // private BuildingPlacementController buildingPlacementController; 

        public override void Start()
        {
            base.Start(); 

            // Initialize event receivers for actions
            primaryActionEventReceiver = new EventReceiver(PlayerInput.LightAttackEventKey);
            primaryActionHeldReceiver = new EventReceiver(PlayerInput.PrimaryAction_Held_EventKey);
            primaryActionReleasedReceiver = new EventReceiver(PlayerInput.PrimaryAction_Released_EventKey);

            secondaryActionEventReceiver = new EventReceiver(PlayerInput.BlockEventKey); // Assuming block maps to secondary for now
            interactReceiver = new EventReceiver(PlayerInput.InteractEventKey);
            
            weaponHoldPoint = Entity.FindChild("WeaponHoldPoint");
            if (weaponHoldPoint == null)
            {
                Log.Warning("PlayerEquipment: 'WeaponHoldPoint' child entity not found. Defaulting to player entity for weapon parenting.");
                // weaponHoldPoint = this.Entity; // No, if null, targetParent will correctly become this.Entity later
            }
        }

        public override void Update()
        {
            // Interaction logic
            if (interactReceiver.TryReceive())
            {
                AttemptResourceGather();
            }

            // Handle Primary Action state updates (Held/Released)
            if (primaryActionHeldReceiver.TryReceive())
            {
                isPrimaryActionHeldDown = true;
            }
            if (primaryActionReleasedReceiver.TryReceive())
            {
                isPrimaryActionHeldDown = false;
            }

            // Handle Primary Action (Initial Press from LightAttackEventKey)
            if (primaryActionEventReceiver.TryReceive())
            {
                TriggerCurrentWeaponPrimary(); // Handles durability for currentlyEquippedItemData

                if (currentlyEquippedItemData != null && !currentlyEquippedItemData.IsBroken)
                {
                    if (CurrentWeapon != null && !CurrentWeapon.IsBroken)
                    {
                        CurrentWeapon.PrimaryAction(); // This is the first shot
                    }
                    else if (CurrentWeapon == null)
                    {
                        Log.Info($"PlayerEquipment: Primary action for item data '{currentlyEquippedItemData.Name}' (no specific CurrentWeapon script).");
                    }
                }
            }
            // Handle Held Primary Action (for automatic weapons, after the initial press)
            else if (isPrimaryActionHeldDown && CurrentWeapon is BaseRangedWeapon rangedWeapon)
            {
                // Check durability and item state here as well, as UpdateHeldAction might not be called if these fail.
                // TriggerCurrentWeaponPrimary() is NOT called here again, as durability for continuous fire
                // should ideally be handled per shot within UpdateHeldAction or a similar per-shot method.
                // For now, assuming UpdateHeldAction in BaseRangedWeapon will also check its own ToolData's IsBroken state.
                // And PlayerEquipment.TriggerCurrentWeaponPrimary reduced durability for the *first* shot.
                // Subsequent shots' durability should be handled by UpdateHeldAction, or a shared method.
                // For simplicity now: PlayerEquipment doesn't reduce durability for *held* actions, that's up to weapon script.
                // This means UpdateHeldAction in BaseRangedWeapon should probably handle its own durability if continuous use costs durability per shot.
                // Current BaseRangedWeapon.UpdateHeldAction does not consume durability. PlayerEquipment.TriggerCurrentWeaponPrimary does.
                // This is a design consideration: does holding down fire continuously drain overall item durability per shot or just the first?
                // For this subtask, we assume UpdateHeldAction will be called, and if it fires, durability was already paid for the "action".
                // A more granular system would have TryFire() in BaseWeapon that deducts durability and PlayerEquipment calls that.

                // Simplified: If primary action was already triggered by LightAttackEventKey,
                // the held logic will call UpdateHeldAction. Cooldowns in BaseRangedWeapon
                // will prevent immediate re-firing.
                if (currentlyEquippedItemData != null && !currentlyEquippedItemData.IsBroken &&
                    CurrentWeapon != null && !CurrentWeapon.IsBroken)
                {
                    rangedWeapon.UpdateHeldAction();
                }
            }


            // Handle Secondary Action (e.g., Block/Aim)
            if (secondaryActionEventReceiver.TryReceive())
            {
                Log.Info("PlayerEquipment: Secondary Action Triggered (e.g., Block/Aim).");
                // Durability for secondary actions could be handled here or within CurrentWeapon.SecondaryAction()
                // For now, let's assume secondary actions might not always consume item durability, or handle it internally.
                if (currentlyEquippedItemData != null && !currentlyEquippedItemData.IsBroken)
                {
                    if (CurrentWeapon != null && !CurrentWeapon.IsBroken)
                    {
                        CurrentWeapon.SecondaryAction();
                    }
                    else if (CurrentWeapon == null)
                    {
                         Log.Info($"PlayerEquipment: Secondary action for item data '{currentlyEquippedItemData.Name}' (no specific CurrentWeapon script).");
                    }
                }
                else if (currentlyEquippedItemData != null && currentlyEquippedItemData.IsBroken)
                {
                    Log.Info($"PlayerEquipment: Cannot use secondary action, item '{currentlyEquippedItemData.Name}' is broken.");
                }
            }
        }

        /// <summary>
        /// Equips a new weapon. If a weapon is already equipped, it will be unequipped first.
        /// </summary>
        /// <param name="newWeapon">The new weapon to equip. Can be null to unequip.</param>
        public void EquipWeapon(BaseWeapon newWeapon) 
        {
            if (CurrentWeapon != null)
            {
                CurrentWeapon.OnUnequip(this.Entity);
                // Potentially destroy the old weapon entity if it was spawned
            }

            CurrentWeapon = newWeapon;

            if (CurrentWeapon != null)
            {
                CurrentWeapon.OnEquip(this.Entity);
                // Potentially attach the new weapon model to the player
            }
        }

        public void EquipItem(MySurvivalGame.Game.Items.MockInventoryItem itemToEquip)
        {
            // --- Unequip Previous Item ---
            if (currentWeaponEntity != null)
            {
                // EquipWeapon(null) will call OnUnequip on the CurrentWeapon script instance
                this.EquipWeapon(null);

                // Remove the entity from scene
                Entity.Scene.Entities.Remove(currentWeaponEntity);
                currentWeaponEntity = null;
                // CurrentWeapon script instance is already null from EquipWeapon(null)
            }
            // Also clear data just in case, though EquipWeapon(null) should lead to this.
            currentlyEquippedItemData = null;


            // --- Equip New Item ---
            if (itemToEquip == null)
            {
                Log.Info("PlayerEquipment: No item to equip (itemToEquip was null). CurrentWeapon and data cleared.");
                // Ensure CurrentWeapon script is cleared if item is null (already handled by unequip logic if currentWeaponEntity was not null)
                if (CurrentWeapon != null) this.EquipWeapon(null);
                return;
            }

            if (itemToEquip.CurrentEquipmentType != EquipmentType.Weapon &&
                itemToEquip.CurrentEquipmentType != EquipmentType.Tool)
            {
                Log.Info($"PlayerEquipment: Item '{itemToEquip.Name}' is not a Weapon or Tool. Cannot equip as weapon. (Type: {itemToEquip.CurrentEquipmentType})");
                if (CurrentWeapon != null) this.EquipWeapon(null); // Clear any existing weapon script if the new item isn't one
                return;
            }

            if (!(itemToEquip is WeaponToolData castedItem))
            {
                Log.Error($"PlayerEquipment: Item '{itemToEquip.Name}' is of equippable type but could not be cast to WeaponToolData. Check item creation.");
                if (CurrentWeapon != null) this.EquipWeapon(null);
                return;
            }

            currentlyEquippedItemData = castedItem;
            Log.Info($"PlayerEquipment: Set item data for '{currentlyEquippedItemData.Name}'. Attempting to spawn prefab.");

            string prefabName = null;
            if (currentlyEquippedItemData.Name == "Pickaxe") prefabName = "PickaxePrefab";
            else if (currentlyEquippedItemData.Name == "Hatchet") prefabName = "HatchetPrefab";
            else if (currentlyEquippedItemData.Name == "Pistol") prefabName = "PistolPrefab";
            else if (currentlyEquippedItemData.Name == "Bow") prefabName = "BowPrefab";
            else if (currentlyEquippedItemData.Name == "Assault Rifle") prefabName = "AssaultRiflePrefab";
            // Add more mappings here for other weapons/tools

            if (string.IsNullOrEmpty(prefabName))
            {
                Log.Warning($"PlayerEquipment: No prefab defined for item name: {currentlyEquippedItemData.Name}");
                this.EquipWeapon(null); // Clear CurrentWeapon script
                return;
            }

            try
            {
                var weaponPrefab = Content.Load<Prefab>(prefabName);
                if (weaponPrefab == null)
                {
                    Log.Error($"PlayerEquipment: Could not load prefab: {prefabName}");
                    this.EquipWeapon(null);
                    return;
                }

                // Assuming prefab has one root entity. If multiple, adjust accordingly.
                currentWeaponEntity = weaponPrefab.Instantiate().FirstOrDefault();
                if (currentWeaponEntity == null)
                {
                    Log.Error($"PlayerEquipment: Prefab '{prefabName}' instantiated to null or empty list.");
                    this.EquipWeapon(null);
                    return;
                }

                Entity.Scene.Entities.Add(currentWeaponEntity);

                var weaponScript = currentWeaponEntity.Get<BaseWeapon>();
                if (weaponScript != null)
                {
                    this.EquipWeapon(weaponScript); // This sets this.CurrentWeapon and calls weaponScript.OnEquip()

                    var targetParent = weaponHoldPoint ?? this.Entity;
                    targetParent.AddChild(currentWeaponEntity);
                    currentWeaponEntity.Transform.Position = Vector3.Zero;
                    currentWeaponEntity.Transform.Rotation = Quaternion.Identity;

                    Log.Info($"PlayerEquipment: Successfully spawned and equipped '{prefabName}' with script '{weaponScript.GetType().Name}'. Parented to '{targetParent.Name}'.");
                }
                else
                {
                    Log.Error($"PlayerEquipment: No BaseWeapon-derived script found on entity from prefab '{prefabName}'. Destroying instance.");
                    Entity.Scene.Entities.Remove(currentWeaponEntity);
                    currentWeaponEntity = null;
                    this.EquipWeapon(null); // Ensure CurrentWeapon script is also cleared
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"PlayerEquipment: Exception loading or instantiating prefab '{prefabName}'. Exception: {e.Message}");
                if (currentWeaponEntity != null && currentWeaponEntity.Scene != null) Entity.Scene.Entities.Remove(currentWeaponEntity);
                currentWeaponEntity = null;
                this.EquipWeapon(null);
            }
        }
        
        /// <summary>
        /// Triggers the primary action of the currently equipped weapon.
        /// </summary>
        public void TriggerCurrentWeaponPrimary()
        {
            if (currentlyEquippedItemData == null)
            {
                // Log.Info("PlayerEquipment: No item equipped to use."); // Optional, can be verbose
                return;
            }

            if (currentlyEquippedItemData.IsBroken)
            {
                Log.Info($"PlayerEquipment: Item '{currentlyEquippedItemData.Name}' is broken. Cannot use.");
                return;
            }

            // --- Durability Consumption ---
            if (CurrentWeapon is BaseRangedWeapon)
            {
                // Durability for ranged weapons is handled per shot by BaseRangedWeapon itself.
            }
            else if (currentlyEquippedItemData != null) // Keep existing durability logic for non-ranged (e.g., melee) weapons/tools
            {
                // For now, consume a fixed amount. This could vary by item or action later.
                float durabilityCost = 1.0f;
                currentlyEquippedItemData.DurabilityPoints -= durabilityCost;

                // Ensure durability doesn't go below zero before updating IsBroken & base Durability
                if (currentlyEquippedItemData.DurabilityPoints < 0)
                {
                    currentlyEquippedItemData.DurabilityPoints = 0;
                }

                // Call the UpdateDurability method in WeaponToolData to correctly update IsBroken and sync base.Durability
                currentlyEquippedItemData.UpdateDurability(currentlyEquippedItemData.DurabilityPoints);

                Log.Info($"PlayerEquipment: Used (non-ranged) '{currentlyEquippedItemData.Name}'. Durability: {currentlyEquippedItemData.DurabilityPoints}/{currentlyEquippedItemData.MaxDurabilityPoints}. Broken: {currentlyEquippedItemData.IsBroken}");

                if (currentlyEquippedItemData.IsBroken) // Check if it just broke
                {
                    Log.Warning($"PlayerEquipment: Item '{currentlyEquippedItemData.Name}' just broke!");
                    // Future: Play a 'broken item' sound or visual effect.
                }
            }
            // --- End Durability Consumption ---

            // Call the actual weapon's action (if a BaseWeapon script instance is equipped)
            // This part remains for future integration with actual weapon scripts.
            // Note: Actual call to CurrentWeapon.PrimaryAction() is now handled in Update() after this method.
            // This method now focuses on item data state.
        }

        /// <summary>
        /// Triggers the secondary action of the currently equipped weapon.
        /// </summary>
        public void TriggerCurrentWeaponSecondary()
        {
            if (CurrentWeapon == null)
            {
                return;
            }

            if (CurrentWeapon.IsBroken)
            {
                Log.Info($"PlayerEquipment: Cannot use secondary action, {CurrentWeapon.Entity?.Name ?? "Current weapon"} is broken.");
                return;
            }

            CurrentWeapon.SecondaryAction();
        }

        private void AttemptResourceGather()
        {
            // The condition for hand gathering "!(this.Entity.Get<PlayerInput>()?.Camera?.Get<PlayerCamera>()?.IsFPSCrouched ?? false)"
            // was an example and is removed for clarity. We will rely on the ResourceNodeComponent's ToolCategory.
            // If currentlyEquippedItemData is null, ResourceNodeComponent.HitNode will handle it (e.g. if ToolCategory is Hand).

            var playerInput = this.Entity.Get<PlayerInput>();
            if (playerInput == null || playerInput.Camera == null)
            {
                Log.Error("PlayerEquipment.AttemptResourceGather: PlayerInput or Camera not found.");
                return;
            }

            var camera = playerInput.Camera; // This is the CameraComponent
            var simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error("PlayerEquipment.AttemptResourceGather: Physics simulation not found.");
                return;
            }

            Matrix cameraWorldMatrix = camera.Entity.Transform.WorldMatrix; // Camera's entity transform
            Vector3 raycastStart = cameraWorldMatrix.TranslationVector;
            Vector3 raycastForward = cameraWorldMatrix.Forward;
            float gatherRange = 2.0f; // Max distance for gathering

            // Perform raycast
            var hitResult = simulation.Raycast(raycastStart, raycastStart + raycastForward * gatherRange);

            if (hitResult.Succeeded && hitResult.Collider != null)
            {
                var hitEntity = hitResult.Collider.Entity;
                var resourceNode = hitEntity?.Get<MySurvivalGame.Game.World.ResourceNodeComponent>();
                var playerInventory = this.Entity.Get<PlayerInventoryComponent>();

                if (resourceNode != null && playerInventory != null)
                {
                    Log.Info($"PlayerEquipment: Interacted with '{hitEntity.Name}' which has a ResourceNodeComponent.");
                    
                    // Try to hit the node with the currently equipped tool (which might be null)
                    var harvestedItem = resourceNode.HitNode(currentlyEquippedItemData, playerInventory);

                    if (harvestedItem != null) // HitNode returns an item if harvest was successful
                    {
                        Log.Info($"PlayerEquipment: Successfully harvested '{harvestedItem.Name}' using '{currentlyEquippedItemData?.Name ?? "Hands (conceptual)"}'.");
                        
                        // If a tool was used (not null) and harvest was successful, consume durability
                        if (currentlyEquippedItemData != null)
                        {
                            if (currentlyEquippedItemData.IsBroken) // Check if tool was already broken
                            {
                                Log.Info($"PlayerEquipment: Tool '{currentlyEquippedItemData.Name}' is broken. Cannot use further for gathering.");
                                // Note: HitNode might still allow harvest if tool isn't strictly required or if it just broke.
                                // If HitNode returned an item, it means harvest occurred. We just log tool status here.
                                return; 
                            }

                            float durabilityCost = 1.0f; // Specific to gathering action
                            currentlyEquippedItemData.DurabilityPoints -= durabilityCost;
                            // No need to clamp here as UpdateDurability will handle it.
                            
                            currentlyEquippedItemData.UpdateDurability(currentlyEquippedItemData.DurabilityPoints); // This updates IsBroken and base.Durability

                            Log.Info($"PlayerEquipment: Tool '{currentlyEquippedItemData.Name}' durability: {currentlyEquippedItemData.DurabilityPoints}/{currentlyEquippedItemData.MaxDurabilityPoints}. Broken: {currentlyEquippedItemData.IsBroken}");
                            if (currentlyEquippedItemData.IsBroken)
                            {
                                Log.Warning($"PlayerEquipment: Tool '{currentlyEquippedItemData.Name}' just broke from gathering!");
                                // Future: Player notification, potentially unequip, etc.
                            }
                        }
                    }
                    // If harvestedItem is null, HitNode already logged why (e.g. wrong tool, depleted, inventory full)
                }
                else
                {
                    // Log.Info($"PlayerEquipment: Interacted with '{hitEntity.Name}', but it's not a resource node or player inventory is missing.");
                }
            }
            else
            {
                // Log.Info("PlayerEquipment: Interaction raycast hit nothing in range.");
            }
        }

        public bool AttemptThrowItem(WeaponToolData throwableData)
        {
            if (throwableData == null || !throwableData.IsThrowable)
            {
                Log.Error("AttemptThrowItem: Invalid item data or item is not throwable.");
                return false;
            }

            // TODO: Implement throw cooldown if necessary, similar to fireCooldownTimer in BaseRangedWeapon
            // For now, allow throwing as fast as input is received.

            Log.Info($"PlayerEquipment: Attempting to throw {throwableData.Name}");

            var playerInput = this.Entity.Get<PlayerInput>(); // Assuming PlayerEquipment is on the Player entity
            var camera = playerInput?.Camera;

            if (camera == null) {
                Log.Error("AttemptThrowItem: Camera not found on PlayerInput component!");
                return false;
            }

            var cameraMatrix = camera.Entity.Transform.WorldMatrix;
            // Spawn slightly in front of the camera to avoid instant self-collision
            Vector3 spawnPosition = cameraMatrix.TranslationVector + cameraMatrix.Forward * 1.0f;
            Vector3 throwDirection = cameraMatrix.Forward;

            // Assuming a generic "ExplosiveGrenadeProjectilePrefab" for all throwable explosives for now
            // This could be made more specific if throwableData had a ProjectilePrefabName property.
            var projectilePrefab = Content.Load<Prefab>("ExplosiveGrenadeProjectilePrefab");

            if (projectilePrefab != null)
            {
                var projectileEntity = projectilePrefab.Instantiate().FirstOrDefault();
                if (projectileEntity == null)
                {
                    Log.Error("AttemptThrowItem: Failed to instantiate ExplosiveGrenadeProjectilePrefab.");
                    return false;
                }

                var projectileScript = projectileEntity.Get<Projectile>();
                if (projectileScript != null)
                {
                    projectileScript.InitialVelocity = throwDirection * throwableData.ThrowForce;
                    projectileScript.Damage = throwableData.ExplosionDamage; // Use specific explosion damage
                    projectileScript.ExplosionRadius = throwableData.AoeRadius;
                    projectileScript.LifespanSeconds = throwableData.FuseTime;
                    projectileScript.IsExplosive = true; // Grenades are explosive
                    projectileScript.DamageFalloff = true; // Typically true for explosives, could be from data
                    projectileScript.ShooterEntity = this.Entity; // Player entity is the shooter

                    projectileEntity.Transform.Position = spawnPosition;

                    Entity.Scene.Entities.Add(projectileEntity);
                    Log.Info($"{throwableData.Name} thrown. Projectile: {projectileEntity.Name}, Force: {throwableData.ThrowForce}, Fuse: {throwableData.FuseTime}s");
                    return true; // Signify successful throw initiation
                }
                else
                {
                    Log.Error("AttemptThrowItem: Projectile script not found on ExplosiveGrenadeProjectilePrefab.");
                    Entity.Scene.Entities.Remove(projectileEntity); // Clean up unconfigured entity
                }
            }
            else
            {
                Log.Error("AttemptThrowItem: Could not load ExplosiveGrenadeProjectilePrefab.");
            }
            return false;
        }
    }
}
