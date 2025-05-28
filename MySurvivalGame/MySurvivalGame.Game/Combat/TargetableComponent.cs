using Stride.Engine;
using Stride.Core.Mathematics;

namespace MySurvivalGame.Game.Combat
{
    public class TargetableComponent : SyncScript
    {
        // Offset from the entity's origin where the camera should focus when locked on.
        public Vector3 LockOnPointOffset { get; set; } = new Vector3(0, 1.0f, 0); // Default to 1m above origin

        public Vector3 GetWorldLockOnPoint()
        {
            // Ensure entity and transform are not null before accessing
            if (Entity == null || Entity.Transform == null)
            {
                // Log an error or return a default value if appropriate
                // For now, returning a zero vector or throwing might be options
                // However, in a real scenario, this component should always be on an entity with a transform.
                Log.Error("TargetableComponent is not attached to a valid Entity with a Transform.");
                return Vector3.Zero; // Or handle more gracefully
            }
            return Entity.Transform.WorldMatrix.TranslationVector + Vector3.Transform(LockOnPointOffset, Entity.Transform.Rotation);
        }
    }
}
