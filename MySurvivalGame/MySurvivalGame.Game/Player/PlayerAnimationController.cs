// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Engine;
using Stride.Core.Mathematics; // For Vector3
using Stride.Physics;         // For CharacterComponent
using Stride.Animations;      // For AnimationComponent, PlayingAnimation, AnimationClip etc.
using System.Linq;            // For LINQ operations like FirstOrDefault

namespace MySurvivalGame.Game.Player // MODIFIED: Namespace to align with new folder structure
{
    public class PlayerAnimationController : SyncScript
    {
        // Property to be linked to the AnimationComponent on the PlayerModel entity
        public AnimationComponent AnimComponent { get; set; } 
        public CharacterComponent Character { get; set; }
        public PlayerCombatController CombatController { get; set; }

        // --- Animation Clip Names (placeholders for Mixamo animations) ---
        public string IdleAnimationName { get; set; } = "Idle";
        public string WalkAnimationName { get; set; } = "Walk";
        public string RunAnimationName { get; set; } = "Run"; 
        public string LightAttackAnimationName { get; set; } = "LightAttack";
        public string HeavyAttackAnimationName { get; set; } = "HeavyAttack";
        public string DodgeAnimationName { get; set; } = "Dodge";
        public string UpperBodyIdleAnimationName { get; set; } = "Idle_Upper"; // Example for upper body
        public string LowerBodyIdleAnimationName { get; set; } = "Idle_Lower"; // Example for lower body


        // Internal constants for current logic, might be replaced by properties above if exact match
        // These will be phased out as the new state logic is fully implemented with actual animation names.
        private const string CurrentLogicIdleAnim = "RifleIdle";   
        private const string CurrentLogicWalkAnim = "WalkForward"; 

        // Animation Layer Keys (conceptual)
        private const string BaseLayerKey = "BaseLayer";
        private const string UpperBodyAdditiveLayerKey = "UpperBodyAdditiveLayer";
        private const string ActionLayerKey = "ActionLayer"; // For one-shot attacks/dodges

        // To keep track of currently playing looped animations on layers
        private PlayingAnimation currentBaseLayerAnimation;
        private PlayingAnimation currentUpperBodyLayerAnimation;


        public override void Start()
        {
            // This script is now on the "Player" entity.
            Character = Entity.Get<CharacterComponent>();
            if (Character == null)
            {
                Log.Error("PlayerAnimationController: CharacterComponent not found on this entity.");
            }

            CombatController = Entity.Get<PlayerCombatController>();
            if (CombatController == null)
            {
                Log.Error("PlayerAnimationController: PlayerCombatController not found on this entity.");
            }

            // AnimComponent is on a child entity named "PlayerModel".
            // This can be linked via the editor, or found dynamically.
            if (AnimComponent == null)
            {
                var modelEntity = Entity.FindChild("PlayerModel");
                if (modelEntity != null)
                {
                    AnimComponent = modelEntity.Get<AnimationComponent>();
                }
                
                if (AnimComponent == null)
                {
                    Log.Error("PlayerAnimationController: AnimComponent not found on child 'PlayerModel' or not assigned.");
                }
            }
            
            // --- Initial Animation State & Investigation into Layering/Masking ---
            // Stride's AnimationComponent plays AnimationClips. A PlayingAnimation is an instance of an AnimationClip being played.
            // The AnimationComponent has a list of PlayingAnimations.
            //
            // **Primary Question for Investigation:** How to make one PlayingAnimation affect only upper body bones,
            // and another PlayingAnimation affect only lower body bones simultaneously?
            //
            // **1. Bone Masking per PlayingAnimation:**
            //    - Ideal Scenario: `AnimComponent.Play("Walk_Forward", new AnimationPlayParameters { BoneMask = lowerBodyBoneNamesList });`
            //    - Stride's `PlayingAnimation` class does NOT seem to have a direct `BoneMask` property or equivalent
            //      that takes a list of bone names to include/exclude for that specific animation instance *at runtime* when playing.
            //    - `AnimationClip` itself doesn't store a bone mask that `PlayingAnimation` would inherit for a specific playback.
            //
            // **2. Animation Layers (Unity-style):**
            //    - Unity has a concept of layers in its Animator, where each layer can have a weight and an Avatar Mask.
            //    - Stride's `AnimationComponent` has a `BlendOperation` (Additive, Linear) but this applies globally to how
            //      multiple currently playing animations are combined, not to layering with distinct masks per layer.
            //    - There isn't an obvious "Layers" collection on `AnimationComponent` where each layer could have its own mask.
            //
            // **3. Additive Animations & `AnimationBlendOperation.Additive`:**
            //    - This is supported. If "Rifle_Idle_Upper" is a true additive animation (designed to only contain rotations relative
            //      to a base pose, typically the T-pose or another idle), then playing it additively might work IF the lower body
            //      animation ("Walk_Forward") correctly provides the base pose for all bones, and "Rifle_Idle_Upper" only has
            //      meaningful animation data for upper body bones.
            //    - If "Rifle_Idle_Upper" is a standard animation (not additive), playing it additively might lead to undesirable
            //      results (e.g., doubled rotations).
            //
            // **4. `IAnimationBlender` & Custom Blenders:**
            //    - `AnimationComponent` uses an `IAnimationBlender` (default is `AnimationBlender`).
            //    - It might be possible to create a custom `IAnimationBlender` that, during its `Blend` method,
            //      selectively applies bone transformations based on some criteria (e.g., animation name conventions
            //      or custom properties attached to `PlayingAnimation.StateObject` if that's usable).
            //    - This is an advanced approach and would require deep understanding of Stride's animation evaluation pipeline.
            //
            // **5. Pre-Processing Animations:**
            //    - The most straightforward way, if runtime masking is not directly available, is to ensure animations are
            //      authored/exported correctly:
            //        a) "Walk_Forward" should ideally only contain keyframes for lower body bones.
            //        b) "Rifle_Idle_Upper" should ideally only contain keyframes for upper body bones.
            //    - If animations are full-body, they would need to be split in a DCC tool (e.g., Blender) into separate
            //      clips for upper and lower body.
            //
            // **6. Stride `SkeletonUpdater` and `BoneMask` (Low-level internal type):**
            //    - Digging into Stride's source, there's `Stride.Animations.SkeletonUpdater` which uses a `BoneMask`.
            //    - However, this `BoneMask` (a struct of bitfields) seems to be computed internally based on which bones an
            //      `AnimationClip` actually has animation data for. It's not something a user typically sets per `PlayingAnimation`
            //      to restrict a full-body animation to a subset of bones at runtime.
            //
            // **Conclusion for Initial Setup (based on public API) & Investigation Summary:**
            // - Stride's `AnimationComponent` plays `AnimationClip` assets.
            // - For distinct upper/lower body animations from full-body clips, animations generally need to be pre-split
            //   in a DCC tool (e.g., Blender) or authored specifically as additive layers.
            // - Stride does not appear to offer a high-level, built-in runtime bone masking feature on `PlayingAnimation`
            //   to restrict a full-body clip to certain bones for a specific playback instance without custom `IAnimationBlender` development.
            // - Current blending will rely on `AnimationBlendOperation.Additive` for upper body layers,
            //   assuming the animation clips are authored appropriately (either as true additive clips or pre-split).
            // - The system seems to rely more on animations being authored for the parts they should affect.
            // - For this script, we will assume animations *would be* correctly authored (e.g., "Idle_Upper_Additive" only affects upper bones).
            //   Then, `BlendOperation.Additive` for the upper body animation is the most promising approach.

            // Example: Try to play a base idle and an upper body additive idle if available
            // This assumes "Idle_Upper_Additive" is a true additive animation or only affects upper bones.
            // And "Idle_Lower" (or a full body idle) provides the base.
            
            // Ensure animations are present before trying to play.
            // Initialize with Idle animation on base and upper body layers.
            // This simplifies the initial state, Update() will then adjust based on PlayerState.
            PlayAnimation(IdleAnimationName, BaseLayerKey, true, AnimationBlendOperation.LinearBlend, 0.0f);
            PlayAnimation(UpperBodyIdleAnimationName, UpperBodyAdditiveLayerKey, true, AnimationBlendOperation.Additive, 0.0f);
            
            // Subscribe to combat action events (example)
            // Ensure PlayerInput static event keys are accessible.
            // lightAttackListener = new EventReceiver(PlayerInput.LightAttackEventKey, HandleLightAttack);
        }

        public override void Update()
        {
            if (AnimComponent == null || Character == null || CombatController == null)
                return;

            // lightAttackListener?.TryReceive(); // Process action events

            PlayerState currentState = CombatController.CurrentState;
            
            // Base layer animations (locomotion)
            switch (currentState)
            {
                case PlayerState.Idle:
                case PlayerState.LockedOnIdle:
                    PlayAnimation(IdleAnimationName, BaseLayerKey, true, AnimationBlendOperation.LinearBlend);
                    break;
                case PlayerState.Moving:
                case PlayerState.LockedOnMoving:
                    // TODO: Add differentiation for RunAnimationName based on speed or sprint state
                    PlayAnimation(WalkAnimationName, BaseLayerKey, true, AnimationBlendOperation.LinearBlend);
                    break;
                    // Other states like Attacking, Dodging might stop or override base layer
                    // or they might be primarily upper body / action layer.
            }

            // Upper body additive layer (e.g., idle pose, aiming)
            // This layer should generally persist unless an action overrides it.
            // For now, it just plays an upper body idle additively.
            // In a more complex system, this could be aiming animations, etc.
            // If an action like an attack is full-body, it might temporarily "own" all layers or stop this one.
            if (currentState == PlayerState.Idle || currentState == PlayerState.Moving ||
                currentState == PlayerState.LockedOnIdle || currentState == PlayerState.LockedOnMoving)
            {
                 PlayAnimation(UpperBodyIdleAnimationName, UpperBodyAdditiveLayerKey, true, AnimationBlendOperation.Additive);
            }


            // --- Example of how new animation properties might be used (commented out) ---
            // This section is for illustrative purposes and would replace/integrate with the above logic
            // once actual Mixamo animations are in place and named according to the properties.

            // PlayerState currentState = Entity.Get<PlayerCombatController>()?.CurrentState ?? PlayerState.Idle;
            //
            // switch (currentState)
            // {
            //     case PlayerState.Idle:
            //         if (!AnimComponent.IsPlaying(IdleAnimationName)) AnimComponent.Crossfade(IdleAnimationName, TimeSpan.FromSeconds(0.25));
            //         break;
            //     case PlayerState.Moving:
            //         // Could further differentiate between Walk and Run based on speed or a 'IsSprinting' flag
            //         if (!AnimComponent.IsPlaying(WalkAnimationName)) AnimComponent.Crossfade(WalkAnimationName, TimeSpan.FromSeconds(0.25));
            //         break;
            //     case PlayerState.LockedOnIdle:
            //         // May use a different Idle animation or blend poses for locked-on state
            //         if (!AnimComponent.IsPlaying(IdleAnimationName)) AnimComponent.Crossfade(IdleAnimationName, TimeSpan.FromSeconds(0.25));
            //         break;
            //     case PlayerState.LockedOnMoving:
            //         // May use specific strafing/locked movement animations
            //         if (!AnimComponent.IsPlaying(WalkAnimationName)) AnimComponent.Crossfade(WalkAnimationName, TimeSpan.FromSeconds(0.25)); // Placeholder
            //         break;
            // }
            //
            // // For one-shot animations like attacks or dodges, you'd play them when an event occurs:
            // // Example: If a 'LightAttackEvent' is received from PlayerInput:
            // // if (AnimComponent.Animations.ContainsKey(LightAttackAnimationName))
            // // {
            // //    AnimComponent.Play(LightAttackAnimationName, new AnimationPlayParameters { BlendOperation = AnimationBlendOperation.Additive, TimeFactor = Character.AttackSpeed }); 
            // //    // Additive might be used if it's an upper-body attack, or Linear if full-body.
            // // }
            //
            // // if (AnimComponent != null && !AnimComponent.IsPlaying(IdleAnimationName))
            // // {
            // //     AnimComponent.Play(IdleAnimationName);
            // // }
        }

        /// <summary>
        /// Plays or crossfades an animation on a specified layer, ensuring it's not unnecessarily restarted.
        /// </summary>
        /// <param name="clipName">Name of the animation clip.</param>
        /// <param name="layerKey">A unique key for the layer/slot this animation occupies.</param>
        /// <param name="loop">Whether the animation should loop.</param>
        /// <param name="blendOperation">How this animation should blend with others.</param>
        /// <param name="transitionDuration">Duration of the crossfade. 0 for immediate play.</param>
        /// <param name="timeScale">Speed factor for the animation.</param>
        private void PlayAnimation(string clipName, string layerKey, bool loop, AnimationBlendOperation blendOperation, float transitionDuration = 0.2f, float timeScale = 1.0f)
        {
            if (AnimComponent == null || string.IsNullOrEmpty(clipName) || !AnimComponent.Animations.ContainsKey(clipName))
            {
                // Log.WarningOnce($"PlayAnimation: Clip '{clipName}' not found or AnimComponent is null.");
                return;
            }

            // Check if the desired animation is already playing on this layer with the same parameters
            var currentPlayingAnimation = AnimComponent.PlayingAnimations.FirstOrDefault(pa => pa.Key == layerKey);
            if (currentPlayingAnimation != null && currentPlayingAnimation.Name == clipName && currentPlayingAnimation.Enabled)
            {
                // Already playing this clip on this layer, ensure loop and timescale are current
                currentPlayingAnimation.Loop = loop; 
                currentPlayingAnimation.TimeFactor = timeScale;
                return; 
            }

            // Stop any currently playing animation on this specific layer before starting a new one
            if (currentPlayingAnimation != null)
            {
                currentPlayingAnimation.Stop();
            }
            
            var playParams = new AnimationPlayParameters
            {
                Loop = loop,
                BlendOperation = blendOperation,
                Key = layerKey, // Use the key to identify this playing instance
                TimeFactor = timeScale
            };

            if (transitionDuration > 0.0f)
            {
                AnimComponent.Crossfade(clipName, playParams, TimeSpan.FromSeconds(transitionDuration));
            }
            else
            {
                AnimComponent.Play(clipName, playParams);
            }

            // Store reference to the new playing animation for the layer
            if (layerKey == BaseLayerKey) currentBaseLayerAnimation = AnimComponent.PlayingAnimations.FirstOrDefault(pa => pa.Key == layerKey);
            else if (layerKey == UpperBodyAdditiveLayerKey) currentUpperBodyLayerAnimation = AnimComponent.PlayingAnimations.FirstOrDefault(pa => pa.Key == layerKey);
        }

        // Placeholder for action event handlers
        // private void HandleLightAttack()
        // {
        //     PlayAnimation(LightAttackAnimationName, ActionLayerKey, false, AnimationBlendOperation.Additive); // Or Linear if full body
        // }
        // Similarly for HandleHeavyAttack, HandleDodge etc.

        // --- Further Investigation Notes (already present in Start(), reiterated here for clarity in report) ---
            // Stride's `AnimationComponent` plays `AnimationClip` assets.
            // For distinct upper/lower body animations from full-body clips, animations generally need to be pre-split
            // in a DCC tool (e.g., Blender) or authored specifically as additive layers.
            // Stride does not appear to offer a high-level, built-in runtime bone masking feature on `PlayingAnimation`
            // to restrict a full-body clip to certain bones for a specific playback instance without custom `IAnimationBlender` development.
            // Current blending will rely on `AnimationBlendOperation.Additive` for upper body layers,
            // assuming the animation clips are authored appropriately (either as true additive clips or pre-split).
        }
    }
}
