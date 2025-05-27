// Copyright (c) My Survival Game. All rights reserved.
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;
using Stride.Core.Mathematics;
// Potentially add Stride.Physics if CollisionFilterGroupFlags is used later.

namespace MySurvivalGame.Game.Combat
{
    /// <summary>
    /// Represents an entity that can be targeted by other entities (e.g., by a lock-on camera system).
    /// </summary>
    public class TargetableComponent : SyncScript
    {
        /// <summary>
        /// The local offset from the entity's origin where the camera or targeting system should focus.
        /// </summary>
        /// <remarks>
        /// For example, this could be set to aim at the center of mass or head of a character.
        /// </remarks>
        public Vector3 LockOnPointOffset { get; set; } = Vector3.Zero;

        /// <summary>
        /// Gets or sets a value indicating whether this entity is currently targetable.
        /// </summary>
        /// <remarks>
        /// This allows dynamically enabling or disabling targeting for an entity (e.g., if it's defeated or in a non-combat state).
        /// </remarks>
        public bool IsTargetable { get; set; } = true;

        // Optional: For more advanced filtering.
        // public Stride.Physics.CollisionFilterGroupFlags TargetType { get; set; } = Stride.Physics.CollisionFilterGroupFlags.DefaultFilter;

        public override void Start()
        {
            // Initialization logic for the component, if any, could go here.
            // For example, you might want to register this component with a global targeting manager.
        }

        public override void Update()
        {
            // Update logic for the component, if any, could go here.
            // For instance, if IsTargetable depends on other conditions (like health), it could be updated here.
        }

        /// <summary>
        /// Calculates the world-space position of the lock-on point.
        /// </summary>
        /// <returns>The world-space Vector3 position of the lock-on point.</returns>
        public Vector3 GetWorldLockOnPoint()
        {
            // Transform the local offset to world space.
            // Entity.Transform.WorldMatrix.TranslationVector gives the entity's world position (origin).
            // We then need to add the offset, rotated by the entity's world rotation.
            return Entity.Transform.WorldMatrix.TranslationVector + Vector3.Transform(LockOnPointOffset, Entity.Transform.WorldMatrix.Rotation);
        }
    }
}
