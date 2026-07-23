using Client.Data.Model;
using Client.Main.Content;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Objects.Wings;
using Microsoft.Xna.Framework;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Client.Main.Objects
{
    public abstract partial class ModelObject
    {
        // Local animation optimization - per object only
        private struct LocalAnimationState : IEquatable<LocalAnimationState>
        {
            public int ActionIndex;
            public int Frame0;
            public int Frame1;
            public float InterpolationFactor;

            public bool Equals(LocalAnimationState other)
            {
                return ActionIndex == other.ActionIndex &&
                       Frame0 == other.Frame0 &&
                       Frame1 == other.Frame1 &&
                       MathF.Abs(InterpolationFactor - other.InterpolationFactor) < 0.001f;
            }

            public override bool Equals(object obj) => obj is LocalAnimationState other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(ActionIndex, Frame0, Frame1, InterpolationFactor);
        }

        private void Animation(GameTime gameTime)
        {
            if (LinkParentAnimation || Model?.Actions == null || Model.Actions.Length == 0) return;

            int currentActionIndex = Math.Clamp(ResolveActionIndex(CurrentAction), 0, Model.Actions.Length - 1);
            var action = Model.Actions[currentActionIndex];
            if (action == null) return; // Skip animation if action is null

            int totalFrames = Math.Max(action.LockPositions ? action.NumAnimationKeys - 1 : action.NumAnimationKeys, 1);
            float frameDelta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            bool actionChanged = _priorActionIndex != currentActionIndex;

            // Detect death action for walkers to clamp on second-to-last key
            bool isDeathAction = false;
            if (this is WalkerObject)
            {
                if (this is PlayerObject)
                {
                    // Detecção usa o CurrentAction ORIGINAL (enum compacto), não o remapeado.
                    var pa = (PlayerAction)CurrentAction;
                    isDeathAction = pa == PlayerAction.PlayerDie1 || pa == PlayerAction.PlayerDie2;
                }
                else if (this is MonsterObject)
                {
                    isDeathAction = currentActionIndex == (int)Client.Main.Models.MonsterActionType.Die;
                }
                else if (this is NPCObject)
                {
                    var pa = (PlayerAction)CurrentAction;
                    isDeathAction = pa == PlayerAction.PlayerDie1 || pa == PlayerAction.PlayerDie2;
                }
            }

            if (totalFrames == 1 && !ContinuousAnimation)
            {
                if (actionChanged)
                {
                    GenerateBoneMatrix(currentActionIndex, 0, 0, 0);
                    _priorActionIndex = currentActionIndex;
                    InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                }
                CurrentFrame = 0;
                return;
            }

            if (actionChanged)
            {
                _blendFromAction = _priorActionIndex;
                _blendFromTime = _animTime;
                _blendElapsed = 0f;
                _isBlending = true;
                _animTime = 0.0;

                // _blendFromBones is pre-allocated in LoadContent - no need to allocate here
            }

            // Objetos de per-frame animation (o player/hero, walkers) avançam a animação TODO
            // frame com o delta real — sem o throttle de 45fps que dava passos quantizados
            // (aspecto "30fps" na animação mesmo com render a 60). Estáticos/distantes seguem
            // throttled pra performance.
            if (!TryConsumeAnimationDelta(frameDelta, actionChanged || RequiresPerFrameAnimation, out float delta))
            {
                _priorActionIndex = currentActionIndex;
                return;
            }

            // Advance the cycle. Locomotion (walk/run) and everything else use different clocks —
            // see AdvanceKeysPerSecond. This mirrors the mobile client: the walk/run clip plays at
            // its NATIVE glTF duration scaled by (realSpeed / referenceSpeed), so the stride tracks
            // ground travel and the feet plant; idle/attack/etc. play at the BMD-anchored duration.
            float keysPerSecond = AdvanceKeysPerSecond(action, currentActionIndex, frameDelta);
            _animTime += delta * (action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed) * keysPerSecond;
            double framePos;

            if (isDeathAction || HoldOnLastFrame)
            {
                int endIdx = Math.Max(0, totalFrames - 2);
                _animTime = Math.Min(_animTime, endIdx + 0.0001f);
                framePos = _animTime;
            }
            else if (this is WalkerObject walker && walker.IsOneShotPlaying)
            {
                int endIdx = Math.Max(0, totalFrames - 1);
                if (_animTime >= endIdx)
                {
                    _animTime = endIdx;
                    framePos = _animTime;
                    walker.NotifyOneShotAnimationCompleted();
                }
                else
                {
                    framePos = _animTime;
                }
            }
            else
            {
                framePos = _animTime % totalFrames;
            }

            int f0 = (int)framePos;
            int f1 = (f0 + 1) % totalFrames;
            float t = (float)(framePos - f0);
            CurrentFrame = f0;

            GenerateBoneMatrix(currentActionIndex, f0, f1, t);

            if (_isBlending)
            {
                _blendElapsed += delta;
                float blendFactor = MathHelper.Clamp(_blendElapsed / _blendDuration, 0f, 1f);

                if (_blendFromAction >= 0 && _blendFromBones != null)
                {
                    var prevAction = Model.Actions[_blendFromAction];
                    // Blend-from is the outgoing pose during a short crossfade; advance it at the
                    // BMD-anchored/base rate (no locomotion speed-sync needed for a fading pose).
                    float prevRate = (prevAction.BmdKeys > 0 && prevAction.NumAnimationKeys > 0)
                        ? AnimationSpeed * prevAction.NumAnimationKeys / prevAction.BmdKeys
                        : AnimationSpeed;
                    _blendFromTime += delta * (prevAction.PlaySpeed == 0 ? 1.0f : prevAction.PlaySpeed) * prevRate;
                    int prevTotal = Math.Max(prevAction.NumAnimationKeys, 1);
                    double pf = _blendFromTime % prevTotal;
                    int pf0 = (int)pf;
                    int pf1 = (pf0 + 1) % prevTotal;
                    float pt = (float)(pf - pf0);
                    ComputeBoneMatrixTo(_blendFromAction, pf0, pf1, pt, _blendFromBones);

                    // CRITICAL: put the blend-from pose in the SAME space as BoneTransform before
                    // mixing. ComputeBoneMatrixTo returns raw boneWorld (local*parent), but at this
                    // point BoneTransform already holds the FINAL skin matrices (InverseBind *
                    // boneWorld * RootTransform) from GenerateBoneMatrix -> ApplySkinMatrices. For
                    // legacy BMD models InverseBind and RootTransform are identity, so the two
                    // spaces coincided and the old code got away with it. glTF monsters, however,
                    // carry a non-identity InverseBind and a ~4000x RootTransform: blending a
                    // 4000-scale skin matrix against a 0.08-scale raw boneWorld produced garbage
                    // for the blend's duration — the monster collapsed to a speck and sprang back
                    // every time it switched into its attack animation. Apply the skin transform
                    // to the blend-from bones so both sides are comparable.
                    ApplySkinMatrices(Model.Bones, _blendFromBones, _blendFromBones);

                    // Blend by decompose/Slerp/recompose (NOT a component-wise Matrix.Lerp, which
                    // shrinks the rotation columns — invisible on tiny BMD models, very visible
                    // once the RootTransform scale amplifies it).
                    for (int i = 0; i < BoneTransform.Length; i++)
                    {
                        BlendBoneMatrix(ref _blendFromBones[i], ref BoneTransform[i], blendFactor, out BoneTransform[i]);
                    }

                    if (Constants.ANIM_BLEND_PROBE && GetType().Name == "Hunter")
                    {
                        // Measure the whole skeleton's extent (max |translation|) on both sides and
                        // in the result, to catch where/how much it collapses during the blend.
                        float ext(Matrix[] a){ float m=0; for(int k=0;k<a.Length;k++){ var p=a[k].Translation; float d=p.Length(); if(d>m)m=d; } return m; }
                        float minS=9e9f,maxS=0f;
                        for(int k=0;k<BoneTransform.Length;k++){ BoneTransform[k].Decompose(out var s,out _,out _); float a=(MathF.Abs(s.X)+MathF.Abs(s.Y)+MathF.Abs(s.Z))/3f; if(a<minS)minS=a; if(a>maxS)maxS=a; }
                        System.Console.WriteLine($"[BLENDPROBE] Hunter f={blendFactor:F2} from={_blendFromAction} to={currentActionIndex} extFrom={ext(_blendFromBones):F0} extResult={ext(BoneTransform):F0} scaleMin={minS:F2} scaleMax={maxS:F2}");
                    }
                }

                if (blendFactor >= 1.0f)
                {
                    _isBlending = false;
                    _blendFromAction = -1;
                }

                InvalidateBuffers(BUFFER_FLAG_ANIMATION);
            }

            _priorActionIndex = currentActionIndex;
        }

        /// <summary>
        /// How many keyframes/second the given action should advance at. Two clocks, matching
        /// what the original mobile client does (verified from its decompiled Role animation code
        /// in mobile-assets/reverse):
        ///
        /// 1) LOCOMOTION (walk/run) while actually moving: the glTF clip plays at
        ///    (nativeRate * baseSpeed) scaled by realGroundSpeed / MOVE_SPEED. The mobile client
        ///    computes animator.speed = moveSpeed / BasicMoveSpeed[walk=2.85|run=4], and its
        ///    AnimationConfig gives each clip a base playback speed (walk=0.27, run=0.7) — the
        ///    conversion bakes the walk clip ~3.7x too fast, so without that 0.27 base the legs
        ///    race. baseSpeed folds that in; the speed term keeps the stride planted on the
        ///    ground (no slide) at any travel speed. Our reference is MOVE_SPEED (a baseline
        ///    monster's travel speed), matching the mobile reference of 2.85.
        ///
        /// 2) ATTACK: nativeRate × AttackCadence, same shape as locomotion. In the mobile
        ///    client a monster swing is (clip length / stateBaseSpeed) / attackSpeedStat:
        ///    AnimationConfig gives the monster attack states base speed 0.7 (attack/attack2/
        ///    attack3 — same value as run), and animator.speed is the attackSpeed factor,
        ///    which is 1.0 for virtually every monster (cfg_actionLogic duration = 1.0 for
        ///    453 of 463 basic attacks; server default attackSpeed = 1). Like walk, the DH
        ///    conversion bakes attack clips time-compressed (~0.2–0.44s), so playing them at
        ///    native rate would be a blur, and stretching to the .bmd cycle (clock 3, what we
        ///    did before) made them slow motion. AttackCadence starts at the mobile's 0.7 and
        ///    is the visual-calibration knob.
        ///
        /// 3) EVERYTHING ELSE (idle/die/...): anchored to the sibling .bmd's real-time
        ///    duration (BmdKeys). Falls back to the raw AnimationSpeed knob when there is no
        ///    glTF native-duration / .bmd reference.
        /// </summary>
        private float AdvanceKeysPerSecond(ModelAction action, int currentActionIndex, float frameDelta)
        {
            // (1) Locomotion (walk/run) while moving. The glTF walk clip is IN-PLACE (no root
            // translation — measured: the root only bobs vertically), so there is no geometric
            // stride to sync to; the planted-looking cadence is a tuned constant, exactly as the
            // mobile client does it. We express it as: cycle advances with real ground speed
            // (keeps the phase coupled so it can't drift), times a single calibration factor per
            // locomotion type. LOCOMOTION_CADENCE is the knob dialed in visually.
            if (action.NativeDurationSeconds > 1e-4f && action.NumAnimationKeys > 0
                && this is WalkerObject walker && frameDelta > 0f
                && IsLocomotionAction(currentActionIndex) && walker.IsMoving)
            {
                float nativeRate = action.NumAnimationKeys / action.NativeDurationSeconds; // keys/sec at 1.0x
                float cadence = LocomotionCadence(currentActionIndex);
                float realSpeed = walker.LastGroundDistance / frameDelta;
                float speedScale = MathHelper.Clamp(realSpeed / Constants.MOVE_SPEED, 0.15f, 3f);
                float rate = nativeRate * cadence * speedScale;

                if (Constants.ANIM_LOCO_PROBE && this is MonsterObject)
                {
                    float cycleSec = action.NumAnimationKeys / MathF.Max(0.001f, rate);
                    System.Console.WriteLine($"[LOCOPROBE] {GetType().Name} realSpd={realSpeed:F0} moveSpd={walker.MoveSpeed:F0} scale={speedScale:F2} cadence={cadence:F3} cycle={cycleSec:F2}s");
                }
                return rate;
            }

            // (2) Attack on glTF models: native clip duration × AttackCadence (see summary).
            // Only glTF actions carry NativeDurationSeconds, so legacy BMD attacks are
            // untouched (they never had the slow-motion stretch problem).
            if (action.NativeDurationSeconds > 1e-4f && action.NumAnimationKeys > 0
                && IsAttackAction(currentActionIndex))
            {
                float rate = action.NumAnimationKeys / action.NativeDurationSeconds
                             * AttackCadence(currentActionIndex);

                // Log once per swing (at the action switch), not per frame.
                if (Constants.ANIM_ATTACK_PROBE && this is MonsterObject && _priorActionIndex != currentActionIndex)
                {
                    float cycleSec = action.NumAnimationKeys / MathF.Max(0.001f, rate);
                    System.Console.WriteLine($"[ATKPROBE] {GetType().Name} action={currentActionIndex} native={action.NativeDurationSeconds:F2}s cadence={AttackCadence(currentActionIndex):F2} cycle={cycleSec:F2}s (bmdAnchor={(action.BmdKeys > 0 ? action.BmdKeys / MathF.Max(0.001f, AnimationSpeed) : -1f):F2}s)");
                }
                return rate;
            }

            // (2.5) LOCOMOÇÃO em modelo BMD (ex.: PLAYER) sincronizada à velocidade real.
            // O player é BMD (NativeDurationSeconds=0), então caía no rate FIXO (AnimationSpeed)
            // — as pernas andavam sempre no mesmo ritmo independente da velocidade real, dando
            // deslize de pé e o aspecto "travado/desconectado". Aqui a cadência acompanha a
            // distância percorrida por frame (foot-plant), como o cliente mobile: anda devagar
            // = passos lentos, corre = passos rápidos, sem deslizar.
            if (action.NativeDurationSeconds <= 1e-4f && action.BmdKeys == 0
                && this is WalkerObject bmdWalker && frameDelta > 0f && bmdWalker.IsMoving
                && IsLocomotionByCompactAction(CurrentAction))
            {
                float realSpeed = bmdWalker.LastGroundDistance / frameDelta;
                float speedScale = MathHelper.Clamp(realSpeed / Constants.MOVE_SPEED, 0.2f, 3f);
                return AnimationSpeed * speedScale;
            }

            // (3) BMD-anchored real-time duration for non-locomotion glTF actions.
            if (action.BmdKeys > 0 && action.NumAnimationKeys > 0)
                return AnimationSpeed * action.NumAnimationKeys / action.BmdKeys;

            // (4) glTF SEM .bmd irmão pra ancorar (BmdKeys=0): toca na duração NATIVA do
            // próprio clip. Sem isto cai no AnimationSpeed=4 keys/s e um clip de 37 quadros
            // leva 9s em vez de 1,2s — câmera lenta.
            if (action.NativeDurationSeconds > 1e-4f && action.NumAnimationKeys > 0)
                return action.NumAnimationKeys / action.NativeDurationSeconds;

            // Legacy BMD / no reference.
            return AnimationSpeed;
        }

        // Calibration for the in-place locomotion clips. Starting point comes from the mobile
        // client's AnimationConfig base speeds (walk=0.27, run=0.7); dialed in visually because
        // the clip has no root motion to sync to geometrically. Tune these two numbers only.
        protected const float WalkCadence = 0.40f;
        protected const float RunCadence = 0.7f;

        /// <summary>
        /// Cadence multiplier for a locomotion clip (walk vs run). Overridden per object type to
        /// tell walk from run in its action-index space. Default treats everything as walk.
        /// </summary>
        protected virtual float LocomotionCadence(int resolvedActionIndex) => WalkCadence;

        /// <summary>
        /// True when the given resolved action index is a ground-locomotion cycle (walk/run)
        /// that should be speed-synced to travel. Overridden per object type because the action
        /// index space differs (monster MonsterActionType vs player PlayerAction). Default: none.
        /// </summary>
        protected virtual bool IsLocomotionAction(int resolvedActionIndex) => false;

        /// <summary>
        /// True se a AÇÃO COMPACTA (enum, antes do remap p/ o BMD) é de locomoção (walk/run).
        /// Usada pra forçar o lock do root ao andar — robusto a deslocamento de índices do BMD
        /// entre versões (Season 21 tem ~12 ações a mais e o LockPositions veio errado).
        /// </summary>
        protected virtual bool IsLocomotionByCompactAction(int compactAction) => false;

        /// <summary>
        /// True when the given resolved action index is an attack swing that should play at
        /// the glTF clip's native duration × <see cref="AttackCadence"/> instead of being
        /// stretched to the sibling .bmd cycle. Default: none (players stay on BMD timing).
        /// </summary>
        protected virtual bool IsAttackAction(int resolvedActionIndex) => false;

        /// <summary>
        /// Cadence multiplier for an attack clip: effective swing = clip native duration ÷ this.
        /// Mirrors the mobile client's animation-state base speed for monster attacks (0.7,
        /// same value as run) × the attackSpeed stat (1.0 for a baseline monster). Single
        /// visual-calibration knob, exactly like WalkCadence/RunCadence.
        /// </summary>
        protected virtual float AttackCadence(int resolvedActionIndex) => MonsterAttackCadence;

        // Calibration for glTF attack clips, dialed in visually like WalkCadence/RunCadence.
        // Starting point = the mobile AnimationConfig base speed for monster attack states (0.7).
        protected const float MonsterAttackCadence = 0.7f;

        private bool TryConsumeAnimationDelta(float frameDelta, bool forceStep, out float animationDelta)
        {
            animationDelta = 0f;

            if (!float.IsFinite(frameDelta) || frameDelta <= 0f)
                return false;

            if (forceStep)
            {
                _animationStepAccumulatorSeconds = 0f;
                animationDelta = frameDelta;
                return true;
            }

            int animationUpdateFps = Constants.ClampPerformanceFps(Constants.ANIMATION_UPDATE_FPS);
            float animationStepInterval = 1f / animationUpdateFps;

            _animationStepAccumulatorSeconds = MathF.Min(_animationStepAccumulatorSeconds + frameDelta, 0.5f);
            int availableSteps = (int)(_animationStepAccumulatorSeconds / animationStepInterval);
            if (availableSteps <= 0)
                return false;

            animationDelta = availableSteps * animationStepInterval;
            _animationStepAccumulatorSeconds -= animationDelta;
            return animationDelta > 0f;
        }

        protected void GenerateBoneMatrix(int actionIdx, int frame0, int frame1, float t)
        {
            var bones = Model?.Bones;

            if (bones == null || bones.Length == 0)
            {
                // Reset animation cache for invalid models
                _animationStateValid = false;
                _animationSampleValid = false;
                return;
            }

            // Armor items use the player's idle pose so they match equipped visuals
            if (TryApplyPlayerIdlePose(bones))
            {
                _animationStateValid = true;
                _lastAnimationState = default;
                _animationSampleActionIndex = actionIdx;
                _animationSampleFrame0 = frame0;
                _animationSampleFrame1 = frame1;
                _animationSampleInterpolationBucket = QuantizeAnimationInterpolation(t);
                _animationSampleValid = true;
                return;
            }

            if (Model.Actions == null || Model.Actions.Length == 0)
            {
                _animationStateValid = false;
                _animationSampleValid = false;
                return;
            }

            actionIdx = Math.Clamp(actionIdx, 0, Model.Actions.Length - 1);
            var action = Model.Actions[actionIdx];
            _animationSampleActionIndex = actionIdx;
            _animationSampleFrame0 = frame0;
            _animationSampleFrame1 = frame1;
            _animationSampleInterpolationBucket = QuantizeAnimationInterpolation(t);
            _animationSampleValid = true;

            // Create animation state for comparison - only for animated objects
            LocalAnimationState currentAnimState = default;
            bool shouldCheckCache = !RequiresPerFrameAnimation &&
                                   !LinkParentAnimation &&
                                   ParentBoneLink < 0 &&
                                   action.NumAnimationKeys > 1; // Only cache non-critical animated objects

            if (shouldCheckCache)
            {
                currentAnimState = new LocalAnimationState
                {
                    ActionIndex = actionIdx,
                    Frame0 = frame0,
                    Frame1 = frame1,
                    InterpolationFactor = t
                };

                // Check if we can skip expensive calculation using local cache
                // But be more conservative - only skip if frames and interpolation are identical
                if (_animationStateValid && currentAnimState.Equals(_lastAnimationState) &&
                    BoneTransform != null && BoneTransform.Length == bones.Length)
                {
                    // Animation state hasn't changed - no need to recalculate
                    return;
                }
            }

            // Initialize or resize bone transform array if needed
            if (BoneTransform == null || BoneTransform.Length != bones.Length)
                BoneTransform = new Matrix[bones.Length];

            // Rent temp array from pool for safer hierarchical calculations
            // ArrayPool may return larger array, so we use bones.Length for actual operations
            Matrix[] tempBoneTransforms = _matrixArrayPool.Rent(bones.Length);

            try
            {
                bool lockPositions = action.LockPositions;
                float bodyHeight = BodyHeight;
                bool anyBoneChanged = false;

                // Pre-clamp frame indices to valid ranges
                int maxFrameIndex = action.NumAnimationKeys - 1;
                frame0 = Math.Clamp(frame0, 0, maxFrameIndex);
                frame1 = Math.Clamp(frame1, 0, maxFrameIndex);

                // If frames are the same, no interpolation needed
                if (frame0 == frame1) t = 0f;

                // Process bones in order (parents before children)
                for (int i = 0; i < bones.Length; i++)
                {
                    var bone = bones[i];

                    // Skip invalid bones
                    if (bone == ModelBone.Dummy || bone.Matrixes == null || actionIdx >= bone.Matrixes.Length)
                    {
                        tempBoneTransforms[i] = Matrix.Identity;
                        if (BoneTransform[i] != Matrix.Identity)
                            anyBoneChanged = true;
                        continue;
                    }

                    var bm = bone.Matrixes[actionIdx];
                    int numPosKeys = bm.Position?.Length ?? 0;
                    int numQuatKeys = bm.Quaternion?.Length ?? 0;

                    if (numPosKeys == 0 || numQuatKeys == 0)
                    {
                        tempBoneTransforms[i] = Matrix.Identity;
                        if (BoneTransform[i] != Matrix.Identity)
                            anyBoneChanged = true;
                        continue;
                    }

                    // Ensure frame indices are valid for this specific bone
                    int boneMaxFrame = Math.Min(numPosKeys, numQuatKeys) - 1;
                    int boneFrame0 = Math.Min(frame0, boneMaxFrame);
                    int boneFrame1 = Math.Min(frame1, boneMaxFrame);
                    float boneT = (boneFrame0 == boneFrame1) ? 0f : t;

                    Matrix localTransform;

                    // glTF joints can carry non-unit SCALE in their local TRS. Compose it
                    // when present (BMD always had scale=1, so the array may be empty).
                    var scaleArr = bm.Scale;
                    bool hasScale = scaleArr != null && scaleArr.Length > 0;

                    // Optimize for common case: no interpolation needed
                    if (boneT == 0f)
                    {
                        // Direct keyframe - no interpolation
                        localTransform = Matrix.CreateFromQuaternion(bm.Quaternion[boneFrame0]);
                        if (hasScale)
                        {
                            var s = scaleArr[Math.Min(boneFrame0, scaleArr.Length - 1)];
                            localTransform = Matrix.CreateScale(s) * localTransform;
                        }
                        localTransform.Translation = bm.Position[boneFrame0];
                    }
                    else
                    {
                        // Interpolated keyframe - use fast normalized lerp instead of costly Slerp
                        Quaternion q = Nlerp(bm.Quaternion[boneFrame0], bm.Quaternion[boneFrame1], boneT);
                        Vector3 p0 = bm.Position[boneFrame0];
                        Vector3 p1 = bm.Position[boneFrame1];

                        localTransform = Matrix.CreateFromQuaternion(q);
                        if (hasScale)
                        {
                            var s0 = scaleArr[Math.Min(boneFrame0, scaleArr.Length - 1)];
                            var s1 = scaleArr[Math.Min(boneFrame1, scaleArr.Length - 1)];
                            var s = Vector3.Lerp(s0, s1, boneT);
                            localTransform = Matrix.CreateScale(s) * localTransform;
                        }
                        localTransform.M41 = p0.X + (p1.X - p0.X) * boneT;
                        localTransform.M42 = p0.Y + (p1.Y - p0.Y) * boneT;
                        localTransform.M43 = p0.Z + (p1.Z - p0.Z) * boneT;
                    }

                    // Apply position locking for root bone
                    if (i == 0)
                    {
                        if (lockPositions && bm.Position.Length > 0)
                        {
                            var rootPos = bm.Position[0];
                            localTransform.Translation = new Vector3(rootPos.X, rootPos.Y, localTransform.M43 + bodyHeight);
                        }
                    }

                    // Apply parent transformation with safety checks
                    Matrix worldTransform;
                    if (bone.Parent >= 0 && bone.Parent < bones.Length)
                    {
                        worldTransform = localTransform * tempBoneTransforms[bone.Parent];
                    }
                    else
                    {
                        worldTransform = localTransform;
                    }

                    // Store in temp array
                    tempBoneTransforms[i] = worldTransform;

                    // Check if this bone actually changed (simple comparison for performance)
                    if (BoneTransform[i] != worldTransform)
                    {
                        anyBoneChanged = true;
                    }
                }

                // For static objects (single frame) or first-time setup, always update
                bool forceUpdate = action.NumAnimationKeys <= 1 || !_animationStateValid;

                // Allow derived objects to apply procedural bone post-processing (e.g., head look-at).
                // Must run on the temp array so the result also propagates to children using LinkParentAnimation.
                if (PostProcessBoneTransforms(bones, tempBoneTransforms))
                {
                    anyBoneChanged = true;
                }

                // Diagnóstico dev-only (MU_POSE_DIAG=1): compara o palette do RUNTIME com o do
                // validador offline (ModelSkinning) no MESMO action/frame/t, medindo os CENTROS
                // de carne por osso (bbox é invariante a rolamento e mascarava o defeito).
                if (Model?.IsGltf == true && Environment.GetEnvironmentVariable("MU_POSE_DIAG") == "1")
                {
                    _poseDiagTimer += 1;
                    if (_poseDiagTimer % 120 == 1)
                        try
                        {
                            var tmpPal = new Matrix[bones.Length];
                            ApplySkinMatrices(bones, tempBoneTransforms, tmpPal);

                            // audit: mesmos índices via ModelSkinning (com interpolação)
                            var world2 = new System.Numerics.Matrix4x4[Model.Bones.Length];
                            Client.Data.Model.ModelSkinning.BuildBoneWorlds(Model, actionIdx, frame0, frame1, t, world2);
                            var pal2 = new System.Numerics.Matrix4x4[Model.Bones.Length];
                            Client.Data.Model.ModelSkinning.BuildSkinPalette(Model, world2, pal2);

                            var sb = new System.Text.StringBuilder();
                            sb.Append($"[POSEDIAG] {Model.Name} act={actionIdx} f={frame0}/{frame1}/{t:F2}");
                            float worst = 0;
                            for (int bi = 0; bi < Model.Bones.Length; bi++)
                            {
                                // centro da carne do osso bi nos dois palettes
                                System.Numerics.Vector3 sum1 = default, sum2 = default; int cnt = 0;
                                foreach (var mesh in Model.Meshes)
                                    foreach (var v in mesh.Vertices)
                                    {
                                        if (v.Node != bi) continue;
                                        var p1 = Vector3.Transform(new Vector3(v.Position.X, v.Position.Y, v.Position.Z), tmpPal[bi]);
                                        sum1 += new System.Numerics.Vector3(p1.X, p1.Y, p1.Z);
                                        sum2 += Client.Data.Model.ModelSkinning.SkinPosition(v, pal2);
                                        cnt++;
                                    }
                                if (cnt < 4) continue;
                                var d = (sum1 - sum2) / cnt;
                                float e = d.Length();
                                if (e > worst) worst = e;
                                if (e > 3f)
                                    sb.Append($" | {Model.Bones[bi].Name}: d={e:F0} rt=({sum1.X / cnt:F0},{sum1.Y / cnt:F0},{sum1.Z / cnt:F0}) au=({sum2.X / cnt:F0},{sum2.Y / cnt:F0},{sum2.Z / cnt:F0})");
                            }
                            sb.Append($" | worst={worst:F1}");
                            Console.WriteLine(sb.ToString());
                        }
                        catch (Exception dex) { Console.WriteLine($"[POSEDIAG] {Model?.Name} FAIL {dex.Message}"); }
                }

                // Only update final transforms and invalidate if something actually changed OR force update
                if (anyBoneChanged || forceUpdate)
                {
                    // tempBoneTransforms hold boneWorld (local*parent). glTF skinning needs
                    // the full skin matrix per bone: InverseBind * boneWorld * RootTransform.
                    // (InverseBind maps mesh-local verts to joint space; RootTransform is the
                    // MU convention: +90X Y-up->Z-up and uniform scale.) BMD had identity for
                    // both, so this is a no-op for any legacy identity-bind model.
                    ApplySkinMatrices(bones, tempBoneTransforms, BoneTransform);
                    _animationPoseVersion++;
                    InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                }

                // Always update cache for objects that should use it
                if (shouldCheckCache)
                {
                    _lastAnimationState = currentAnimState;
                    _animationStateValid = true;
                }
                else if (action.NumAnimationKeys <= 1)
                {
                    // Mark static objects as having valid animation state
                    _animationStateValid = true;
                }
            }
            finally
            {
                // CRITICAL: Always return rented array to pool to prevent memory leaks
                // clearArray: false because we don't need to zero out Matrix structs (performance)
                _matrixArrayPool.Return(tempBoneTransforms, clearArray: false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int QuantizeAnimationInterpolation(float t)
        {
            return (int)MathHelper.Clamp(MathF.Round(t * 255f), 0f, 255f);
        }

        /// <summary>
        /// Turn per-bone world transforms (boneWorld = local*parent) into the final glTF
        /// skin palette: skin[i] = InverseBind[i] * boneWorld[i] * RootTransform.
        /// InverseBind + RootTransform are identity for legacy identity-bind models, so
        /// this reduces to a plain copy there.
        /// </summary>
        private bool _poseDiagDone;
        private int _poseDiagTimer;

        private void ApplySkinMatrices(ModelBone[] bones, Matrix[] boneWorld, Matrix[] dst)
        {
            Matrix root = ToXnaMatrix(Model.RootTransform);
            bool rootIsIdentity = Model.RootTransform.IsIdentity;

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                Matrix skin = boneWorld[i];

                // Prepend inverse bind (maps mesh-local vertex into this joint's space).
                if (!bone.InverseBind.IsIdentity)
                    skin = ToXnaMatrix(bone.InverseBind) * skin;

                // Append the MU convention.
                if (!rootIsIdentity)
                    skin = skin * root;

                dst[i] = skin;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Matrix ToXnaMatrix(in System.Numerics.Matrix4x4 m) => new(
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44);

        /// <summary>
        /// Allows derived objects to procedurally adjust the computed bone transforms (in-place).
        /// Return true if any bone was modified.
        /// </summary>
        protected virtual bool PostProcessBoneTransforms(ModelBone[] bones, Matrix[] boneTransforms)
        {
            return false;
        }

        private static Quaternion Nlerp(in Quaternion q1, in Quaternion q2, float t)
        {
            var target = q2;
            if (Quaternion.Dot(q1, q2) < 0f)
            {
                target.X = -target.X;
                target.Y = -target.Y;
                target.Z = -target.Z;
                target.W = -target.W;
            }

            var blended = new Quaternion(
                q1.X + (target.X - q1.X) * t,
                q1.Y + (target.Y - q1.Y) * t,
                q1.Z + (target.Z - q1.Z) * t,
                q1.W + (target.W - q1.W) * t);

            return Quaternion.Normalize(blended);
        }

        /// <summary>
        /// Blend two bone world matrices by interpolating their translation/rotation/scale
        /// components separately (Slerp keeps rotation unit-length), instead of a component-wise
        /// Matrix.Lerp which shrinks the rotation part. Falls back to a plain lerp only if a
        /// matrix can't be decomposed (degenerate), which never makes it worse.
        /// </summary>
        private static void BlendBoneMatrix(ref Matrix from, ref Matrix to, float t, out Matrix result)
        {
            if (from.Decompose(out Vector3 sFrom, out Quaternion rFrom, out Vector3 tFrom) &&
                to.Decompose(out Vector3 sTo, out Quaternion rTo, out Vector3 tTo))
            {
                Vector3 s = Vector3.Lerp(sFrom, sTo, t);
                Quaternion r = Quaternion.Slerp(rFrom, rTo, t);
                Vector3 tr = Vector3.Lerp(tFrom, tTo, t);
                result = Matrix.CreateScale(s) * Matrix.CreateFromQuaternion(r) * Matrix.CreateTranslation(tr);
            }
            else
            {
                Matrix.Lerp(ref from, ref to, t, out result);
            }
        }

        private void ComputeBoneMatrixTo(int actionIdx, int frame0, int frame1, float t, Matrix[] output)
        {
            if (Model?.Bones == null || output == null)
                return;

            var bones = Model.Bones;
            if (actionIdx < 0 || actionIdx >= Model.Actions.Length)
                actionIdx = 0;

            var action = Model.Actions[actionIdx];

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];

                // A skipped bone MUST still write output[i] — leaving it stale (from a previous
                // call / different action) corrupts this bone AND every child that multiplies by
                // output[parent] below, which showed up as the model folding/shrinking during the
                // 0.25s action-change blend (e.g. Hunter on attack). Fall back to the parent's
                // world (or identity for a root) so the chain stays coherent.
                if (bone == ModelBone.Dummy || bone.Matrixes == null || actionIdx >= bone.Matrixes.Length)
                {
                    output[i] = (bone != ModelBone.Dummy && bone.Parent >= 0 && bone.Parent < output.Length)
                        ? output[bone.Parent] : Matrix.Identity;
                    continue;
                }

                var bm = bone.Matrixes[actionIdx];

                int numPosKeys = bm.Position?.Length ?? 0;
                int numQuatKeys = bm.Quaternion?.Length ?? 0;
                if (numPosKeys == 0 || numQuatKeys == 0)
                {
                    output[i] = (bone.Parent >= 0 && bone.Parent < output.Length)
                        ? output[bone.Parent] : Matrix.Identity;
                    continue;
                }

                // Per-bone LOCAL copies — clamping must NOT leak the mutated frame/t into the next
                // bone's iteration (that silently corrupted later bones in the chain).
                int bf0 = frame0, bf1 = frame1;
                float bt = t;
                if (bf0 < 0 || bf1 < 0 || bf0 >= numPosKeys || bf1 >= numPosKeys || bf0 >= numQuatKeys || bf1 >= numQuatKeys)
                {
                    int maxValidIndex = Math.Min(numPosKeys, numQuatKeys) - 1;
                    if (maxValidIndex < 0) maxValidIndex = 0;
                    bf0 = Math.Clamp(bf0, 0, maxValidIndex);
                    bf1 = Math.Clamp(bf1, 0, maxValidIndex);
                    if (bf0 == bf1) bt = 0f;
                }

                Quaternion q = Nlerp(bm.Quaternion[bf0], bm.Quaternion[bf1], bt);

                // Compose local = Scale * Rotation * Translation, matching ModelSkinning.SampleLocal
                // (the shared/validator path). The 233 rotation-only rigs and legacy BMD have unit
                // scale, so this is a no-op for them; kept for parity in case a converted rig ever
                // carries a per-bone scale curve on an actual skin joint.
                int numScaleKeys = bm.Scale?.Length ?? 0;
                Matrix m;
                if (numScaleKeys > 0)
                {
                    int sf0 = bf0 < numScaleKeys ? bf0 : numScaleKeys - 1;
                    int sf1 = bf1 < numScaleKeys ? bf1 : numScaleKeys - 1;
                    Vector3 s0 = bm.Scale[sf0];
                    Vector3 s1 = bm.Scale[sf1];
                    Vector3 s = new(
                        s0.X + (s1.X - s0.X) * bt,
                        s0.Y + (s1.Y - s0.Y) * bt,
                        s0.Z + (s1.Z - s0.Z) * bt);
                    m = Matrix.CreateScale(s) * Matrix.CreateFromQuaternion(q);
                }
                else
                {
                    m = Matrix.CreateFromQuaternion(q);
                }

                Vector3 p0 = bm.Position[bf0];
                Vector3 p1 = bm.Position[bf1];

                m.M41 = p0.X + (p1.X - p0.X) * bt;
                m.M42 = p0.Y + (p1.Y - p0.Y) * bt;
                m.M43 = p0.Z + (p1.Z - p0.Z) * bt;

                if (i == 0 && action.LockPositions)
                    m.Translation = new Vector3(bm.Position[0].X, bm.Position[0].Y, m.M43 + BodyHeight);

                Matrix world = bone.Parent != -1 && bone.Parent < output.Length
                    ? m * output[bone.Parent]
                    : m;

                output[i] = world;
            }
        }

        /// <summary>
        /// Allows derived objects to provide modified bone transforms for rendering.
        /// Default returns the input bones unchanged.
        /// Useful for lightweight procedural deformations (e.g., cape flutter).
        /// </summary>
        protected virtual Matrix[] GetRenderBoneTransforms(Matrix[] bones)
        {
            return bones;
        }

        private bool TryApplyPlayerIdlePose(ModelBone[] bones)
        {
            var def = ItemDefinition;
            int group = def?.Group ?? -1;
            bool isArmor = group >= 7 && group <= 11;
            if (!isArmor)
                return false;

            var playerBones = PlayerIdlePoseProvider.GetIdleBoneMatrices();
            if (playerBones == null || playerBones.Length == 0)
                return false;

            if (BoneTransform == null || BoneTransform.Length != bones.Length)
                BoneTransform = new Matrix[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                BoneTransform[i] = (i < playerBones.Length)
                    ? playerBones[i]
                    : BuildBoneFromBmd(bones[i], BoneTransform);
            }

            InvalidateBuffers(BUFFER_FLAG_ANIMATION);
            return true;
        }

        private static Matrix BuildBoneFromBmd(ModelBone bone, Matrix[] parentResults)
        {
            Matrix local = Matrix.Identity;

            if (bone?.Matrixes != null && bone.Matrixes.Length > 0)
            {
                var bm = bone.Matrixes[0];
                if (bm.Position?.Length > 0 && bm.Quaternion?.Length > 0)
                {
                    var q = bm.Quaternion[0];
                    local = Matrix.CreateFromQuaternion(new Quaternion(q.X, q.Y, q.Z, q.W));
                    var p = bm.Position[0];
                    local.Translation = new Vector3(p.X, p.Y, p.Z);
                }
            }

            if (bone != null && bone.Parent >= 0 && bone.Parent < parentResults.Length)
                return local * parentResults[bone.Parent];

            return local;
        }
    }
}
