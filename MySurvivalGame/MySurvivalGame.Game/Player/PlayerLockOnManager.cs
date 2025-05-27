// Copyright (c) My Survival Game. All rights reserved.
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Stride.Engine;
using Stride.Engine.Events;
using Stride.Core.Mathematics;
using Stride.Physics;
using Stride.Input; // Required for PlayerInput.LockOnToggleEventKey
using MySurvivalGame.Game.Combat; // Required for TargetableComponent

namespace MySurvivalGame.Game.Player
{
    public class PlayerLockOnManager : SyncScript
    {
        public static readonly EventKey<Entity> TargetLockedEvent = new EventKey<Entity>();
        public static readonly EventKey TargetUnlockedEvent = new EventKey();

        public Entity CurrentTarget { get; private set; }
        private Simulation simulation;
        private EventReceiver lockOnToggleListener;
        private EventReceiver switchTargetLeftListener;
        private EventReceiver switchTargetRightListener;

        [Display("Player Camera")]
        public CameraComponent PlayerCamera { get; set; }

        [Display("Lock-On Sphere Radius")]
        public float LockOnSphereRadius { get; set; } = 10.0f;

        [Display("Max Lock-On Distance")]
        public float MaxLockOnDistance { get; set; } = 20.0f;

        public override void Start()
        {
            simulation = this.GetSimulation();
            if (simulation == null)
            {
                Log.Error("PlayerLockOnManager: Physics simulation not found.");
                return;
            }

            if (PlayerCamera == null)
            {
                // Attempt to find it on a child entity named "Camera" or on the same entity if setup differently
                var cameraEntity = Entity.FindChild("Camera") ?? Entity; 
                PlayerCamera = cameraEntity?.Get<CameraComponent>();
                if (PlayerCamera == null)
                {
                     Log.Error("PlayerLockOnManager: PlayerCamera not assigned or found.");
                }
            }
            
            // Initialize the event receiver for PlayerInput.LockOnToggleEventKey
            // Note: PlayerInput must be loaded and its event key accessible.
            // Ensure PlayerInput script is on the same entity or a known entity to access its static event key.
            lockOnToggleListener = new EventReceiver(PlayerInput.LockOnToggleEventKey, HandleLockOnToggle);
            switchTargetLeftListener = new EventReceiver(PlayerInput.SwitchTargetLeftEventKey, () => HandleSwitchTarget(false));
            switchTargetRightListener = new EventReceiver(PlayerInput.SwitchTargetRightEventKey, () => HandleSwitchTarget(true));
        }

        public override void Update()
        {
            // Continuously process event queues
            lockOnToggleListener?.TryReceive();
            switchTargetLeftListener?.TryReceive();
            switchTargetRightListener?.TryReceive();

            // Optional: Add logic here to check if the current target is still valid
            // (e.g., still in range, still targetable, visible, etc.)
            // If not, call ClearLockOnTarget(). This makes the lock-on more dynamic.
            if (CurrentTarget != null && PlayerCamera != null && simulation != null)
            {
                var targetableComponent = CurrentTarget.Get<TargetableComponent>();

                // 1. Target Defeated / Not Targetable Check
                //    (Using IsTargetable as a proxy for HealthComponent.CurrentHealth <= 0 for now)
                if (targetableComponent == null || !targetableComponent.IsTargetable)
                {
                    Log.Info($"Target {CurrentTarget.Name} is no longer targetable or component missing. Clearing lock.");
                    ClearLockOnTarget();
                    return; // Exit early as target is invalid
                }
                // TODO: Add check for HealthComponent once available:
                // var healthComponent = CurrentTarget.Get<HealthComponent>();
                // if (healthComponent != null && healthComponent.CurrentHealth <= 0)
                // {
                //     Log.Info($"Target {CurrentTarget.Name} defeated. Clearing lock.");
                //     ClearLockOnTarget();
                //     return;
                // }

                Vector3 targetWorldLockOnPoint = targetableComponent.GetWorldLockOnPoint();
                Vector3 cameraPosition = PlayerCamera.Entity.Transform.WorldMatrix.TranslationVector;

                // 2. Distance Check
                float distanceToTarget = Vector3.Distance(cameraPosition, targetWorldLockOnPoint);
                // Using MaxLockOnDistance directly, as sweep already uses LockOnSphereRadius for acquisition range.
                // Could add a small fixed buffer if needed: e.g., MaxLockOnDistance * 1.1f
                if (distanceToTarget > MaxLockOnDistance) 
                {
                    Log.Info($"Target {CurrentTarget.Name} out of range ({distanceToTarget} > {MaxLockOnDistance}). Clearing lock.");
                    ClearLockOnTarget();
                    return; 
                }

                // 3. Line of Sight (LOS) Check
                var raycastStart = cameraPosition;
                var raycastEnd = targetWorldLockOnPoint;
                
                // Prepare hit result list
                var losHitResults = new List<HitResult>();
                // Define what the ray should collide with. We want to hit anything that could obstruct view.
                // We assume player and target are in DefaultFilter or CharacterFilter.
                // The ray should hit DefaultFilter (environment, other objects).
                // It should ideally ignore the player casting the ray and the target itself initially to see if anything is *between* them.

                // Stride's Raycast method:
                // public bool Raycast(Vector3 from, Vector3 to, out HitResult result, CollisionFilterGroupFlags groupFlags = CollisionFilterGroupFlags.AllFilter, CollisionFilterGroupFlags P_3 = CollisionFilterGroupFlags.AllFilter, IList<Entity> P_4 = null, RaycastPenetration P_5 = RaycastPenetration.PierceFirst)
                // The overload with a list is:
                // public void Raycast(Vector3 from, Vector3 to, IList<HitResult> results, CollisionFilterGroupFlags groupFlags = CollisionFilterGroupFlags.AllFilter, CollisionFilterGroupFlags P_3 = CollisionFilterGroupFlags.AllFilter, IList<Entity> P_4 = null, RaycastPenetration P_5 = RaycastPenetration.StopAtFirst)

                // For a simple LOS, we want to see if the *first thing hit* is the target or something else.
                HitResult losHitResult;
                bool hasHit = simulation.Raycast(raycastStart, raycastEnd, out losHitResult, 
                                                CollisionFilterGroups.DefaultFilter, // What the ray collides with
                                                CollisionFilterGroupFlags.DefaultFilter); // What type the ray itself is (usually DefaultFilter)


                if (hasHit)
                {
                    // Check if the hit entity is not the CurrentTarget itself.
                    // Also need to ensure the hit entity is not part of the player (e.g. a weapon model if not setup with a specific collision group)
                    // For simplicity, if we hit something, and that something is not our target, then LOS is broken.
                    // This assumes the player's own entity (this.Entity which is the Player entity) is correctly configured for collision.
                    // Player's CharacterComponent is usually in CharacterFilter.
                    if (losHitResult.Collider.Entity != CurrentTarget && losHitResult.Collider.Entity != this.Entity) 
                    {
                        // We hit something else before the target
                        Log.Info($"Line of sight to {CurrentTarget.Name} broken by {losHitResult.Collider.Entity.Name}. Clearing lock.");
                        ClearLockOnTarget();
                        return;
                    }
                }
                // If Raycast returns false, it means nothing was hit, which implies clear LOS to the target position (if target is within ray length).
                // However, raycast only goes up to `raycastEnd`. If the target is further than `raycastEnd` but within `MaxLockOnDistance`,
                // and there's nothing in between, this check might be okay. The distance check handles targets too far.
            }
        }
        
        private void HandleLockOnToggle()
        {
            if (CurrentTarget != null)
            {
                ClearLockOnTarget();
            }
            else
            {
                FindAndLockOnTarget();
            }
        }

        public void ClearLockOnTarget()
        {
            if (CurrentTarget != null)
            {
                Log.Info($"Unlocked target: {CurrentTarget.Name}");
                CurrentTarget = null;
                TargetUnlockedEvent.Broadcast();
                // Future: Add logic to return camera to default behavior (handled by PlayerCamera script)
            }
        }

        public void FindAndLockOnTarget()
        {
            if (PlayerCamera == null || simulation == null)
            {
                Log.Warning("PlayerLockOnManager: PlayerCamera or Simulation is not available. Cannot find target.");
                return;
            }

            var cameraMatrix = PlayerCamera.Entity.Transform.WorldMatrix;
            var sweepStart = cameraMatrix.TranslationVector;
            var sweepEnd = sweepStart + cameraMatrix.Forward * MaxLockOnDistance;

            var sphereShape = new SphereColliderShape(LockOnSphereRadius);
            var hitResults = new List<HitResult>(); // Use List<HitResult>

            // Perform the sweep
            // Note: Stride's ShapeSweep takes the shape, start transform, end transform, list, group, filter flags.
            // We'll use identity rotation for the sphere for simplicity in sweep.
            simulation.ShapeSweep(sphereShape, Matrix.Translation(sweepStart), Matrix.Translation(sweepEnd), hitResults, CollisionFilterGroups.DefaultFilter, CollisionFilterGroupFlags.DefaultFilter);
            
            Entity bestTarget = null;
            float bestTargetScore = float.MaxValue; // Lower is better (e.g., distance to screen center)

            if (hitResults.Count > 0)
            {
                Log.Info($"ShapeSweep found {hitResults.Count} potential hits.");
            }

            foreach (var hitResult in hitResults)
            {
                if (hitResult.Collider == null || hitResult.Collider.Entity == Entity) // Don't target self
                    continue;

                var targetEntity = hitResult.Collider.Entity;
                var targetableComponent = targetEntity.Get<TargetableComponent>();

                if (targetableComponent != null && targetableComponent.IsTargetable)
                {
                    // Basic selection: first valid target
                    // Future: Implement better selection logic (e.g., closest to screen center or player)
                    // For now, we'll take the first valid one.
                    
                    // Example: Calculate distance from camera to target's lock-on point
                    Vector3 targetWorldPos = targetableComponent.GetWorldLockOnPoint();
                    float distance = Vector3.Distance(sweepStart, targetWorldPos);

                    // Simple scoring: closer is better
                    if (distance < bestTargetScore)
                    {
                        bestTargetScore = distance;
                        bestTarget = targetEntity;
                    }
                }
            }

            if (bestTarget != null)
            {
                CurrentTarget = bestTarget;
                Log.Info($"Locked on target: {CurrentTarget.Name}");
                TargetLockedEvent.Broadcast(CurrentTarget);
            }
            else
            {
                Log.Info("No valid target found.");
                // Ensure CurrentTarget remains null if no suitable target is found
                if (CurrentTarget != null) // Should not happen if logic is correct
                {
                     ClearLockOnTarget();   
                }
            }
        }
        
        public override void Cancel()
        {
            // Clean up event listeners
            lockOnToggleListener?.Dispose();
            switchTargetLeftListener?.Dispose();
            switchTargetRightListener?.Dispose();
            base.Cancel();
        }

        private void HandleSwitchTarget(bool switchToRight)
        {
            if (CurrentTarget == null || PlayerCamera == null || simulation == null)
            {
                Log.Info("PlayerLockOnManager: Cannot switch target, no current target or camera/simulation missing.");
                return;
            }

            var cameraMatrix = PlayerCamera.Entity.Transform.WorldMatrix;
            var sweepStart = cameraMatrix.TranslationVector;
            // Use a wider sweep or different parameters if desired for switching, for now, reuse existing
            var sweepEnd = sweepStart + cameraMatrix.Forward * MaxLockOnDistance; 
            var sphereShape = new SphereColliderShape(LockOnSphereRadius);
            var hitResults = new List<HitResult>();

            simulation.ShapeSweep(sphereShape, Matrix.Translation(sweepStart), Matrix.Translation(sweepEnd), hitResults, CollisionFilterGroups.DefaultFilter, CollisionFilterGroupFlags.DefaultFilter);

            var potentialTargets = new List<TargetInfo>();
            var currentTargetScreenPos = PlayerCamera.WorldToScreenPoint(CurrentTarget.Get<TargetableComponent>()?.GetWorldLockOnPoint() ?? CurrentTarget.Transform.WorldMatrix.TranslationVector);

            foreach (var hitResult in hitResults)
            {
                if (hitResult.Collider == null || hitResult.Collider.Entity == Entity || hitResult.Collider.Entity == CurrentTarget)
                    continue;

                var targetEntity = hitResult.Collider.Entity;
                var targetableComponent = targetEntity.Get<TargetableComponent>();

                if (targetableComponent != null && targetableComponent.IsTargetable)
                {
                    Vector3 targetWorldPos = targetableComponent.GetWorldLockOnPoint();
                    Vector3 screenPos = PlayerCamera.WorldToScreenPoint(targetWorldPos);

                    // Only consider targets in front of the camera and within viewport (roughly) for X-sorting
                    if (screenPos.Z > 0 && screenPos.X >= 0 && screenPos.X <= PlayerCamera.Viewport.Width) 
                    {
                        // Check if target is within MaxLockOnDistance
                        float distanceToTarget = Vector3.Distance(sweepStart, targetWorldPos);
                        if (distanceToTarget <= MaxLockOnDistance)
                        {
                            potentialTargets.Add(new TargetInfo(targetEntity, screenPos, targetWorldPos));
                        }
                    }
                }
            }

            if (potentialTargets.Count == 0)
            {
                Log.Info("No other valid targets found to switch to.");
                return;
            }

            // Sort targets by their screen X-coordinate
            potentialTargets.Sort((a, b) => a.ScreenPosition.X.CompareTo(b.ScreenPosition.X));

            Entity newSelectedTarget = null;

            if (switchToRight)
            {
                // Find the first target to the right of the current target's screen X
                newSelectedTarget = potentialTargets.FirstOrDefault(t => t.ScreenPosition.X > currentTargetScreenPos.X)?.Entity;
                if (newSelectedTarget == null && potentialTargets.Count > 0) // Wrap around to the leftmost
                {
                    newSelectedTarget = potentialTargets[0].Entity;
                }
            }
            else // Switch to left
            {
                // Find the last target to the left of the current target's screen X
                newSelectedTarget = potentialTargets.LastOrDefault(t => t.ScreenPosition.X < currentTargetScreenPos.X)?.Entity;
                if (newSelectedTarget == null && potentialTargets.Count > 0) // Wrap around to the rightmost
                {
                    newSelectedTarget = potentialTargets[potentialTargets.Count - 1].Entity;
                }
            }

            if (newSelectedTarget != null && newSelectedTarget != CurrentTarget)
            {
                CurrentTarget = newSelectedTarget;
                Log.Info($"Switched target to: {CurrentTarget.Name}");
                TargetLockedEvent.Broadcast(CurrentTarget);
            }
            else if (newSelectedTarget == CurrentTarget)
            {
                 Log.Info("Only one valid target in range (current), not switching.");
            }
            else
            {
                Log.Info("Could not find a suitable target to switch to in the specified direction.");
            }
        }

        // Helper class for sorting targets
        private class TargetInfo
        {
            public Entity Entity { get; }
            public Vector3 ScreenPosition { get; }
            public Vector3 WorldPosition { get; }

            public TargetInfo(Entity entity, Vector3 screenPos, Vector3 worldPos)
            {
                Entity = entity;
                ScreenPosition = screenPos;
                WorldPosition = worldPos;
            }
        }
    }
}
