using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// PlayerAnimator 필수 상태/모션 누락을 자동 점검하는 에디터 가드.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayerAnimatorGuard
    {
        internal const string PlayerAnimatorControllerPath = "Assets/Animations/PlayerAnimator.controller";

        private const string AnimParamSpeed = "Speed";
        private const string AnimParamHit = "Hit";
        private const string AnimEventHitStart = "OnHitStart";
        private const string AnimEventHitEnd = "OnHitEnd";

        private const float EventTimeTolerance = 0.001f;

        private struct AttackClipEventPreset
        {
            public readonly string AssetPath;
            public readonly string ClipName;
            public readonly float HitStartTime;
            public readonly float HitEndTime;

            public AttackClipEventPreset(string assetPath, string clipName, float hitStartTime, float hitEndTime)
            {
                AssetPath = assetPath;
                ClipName = clipName;
                HitStartTime = hitStartTime;
                HitEndTime = hitEndTime;
            }
        }

        private static readonly string[] RequiredStates =
        {
            PlayerController.ANIM_STATE_LOCOMOTION,
            PlayerController.ANIM_STATE_DASH,
            PlayerController.ANIM_STATE_ATTACK1,
            PlayerController.ANIM_STATE_ATTACK2,
            PlayerController.ANIM_STATE_ATTACK3,
            PlayerController.ANIM_STATE_JUMP,
            PlayerController.ANIM_STATE_HIT,
            PlayerController.ANIM_STATE_DIE
        };

        private static readonly string[] AttackStates =
        {
            PlayerController.ANIM_STATE_ATTACK1,
            PlayerController.ANIM_STATE_ATTACK2,
            PlayerController.ANIM_STATE_ATTACK3
        };

        private static readonly AttackClipEventPreset[] AttackClipPresets =
        {
            new AttackClipEventPreset(
                "Assets/CombatGirlsCharacterPack/School_Katana_Girl/Animations/Normal/Attack1.fbx",
                PlayerController.ANIM_STATE_ATTACK1,
                0.12f,
                0.34f),
            new AttackClipEventPreset(
                "Assets/CombatGirlsCharacterPack/School_Katana_Girl/Animations/Normal/Attack2.fbx",
                PlayerController.ANIM_STATE_ATTACK2,
                0.16f,
                0.42f),
            new AttackClipEventPreset(
                "Assets/CombatGirlsCharacterPack/School_Katana_Girl/Animations/Normal/Attack3.fbx",
                PlayerController.ANIM_STATE_ATTACK3,
                0.22f,
                0.62f)
        };

        static PlayerAnimatorGuard()
        {
            // 공격 이벤트 자동 보정.
            EditorApplication.delayCall += EnsureAttackEventsOnEditorLoad;
            // 에디터 리로드 직후 한 번 자동 검증.
            EditorApplication.delayCall += ValidateOnEditorLoad;
        }

        [MenuItem("Tools/Validation/Validate Player Animator")]
        private static void ValidateFromMenu()
        {
            ValidateAndReport(logSuccess: true);
        }

        [MenuItem("Tools/Validation/Fix Player Attack Events")]
        private static void FixPlayerAttackEventsFromMenu()
        {
            EnsureAttackAnimationEvents(logSuccess: true);
            ValidateAndReport(logSuccess: true);
        }

        internal static void ValidateAfterImport()
        {
            ValidateAndReport(logSuccess: false);
        }

        internal static void EnsureAttackEventsAfterImport()
        {
            EnsureAttackAnimationEvents(logSuccess: false);
        }

        internal static bool ContainsAttackClipPath(string[] assetPaths)
        {
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                for (int presetIndex = 0; presetIndex < AttackClipPresets.Length; presetIndex++)
                {
                    if (string.Equals(assetPath, AttackClipPresets[presetIndex].AssetPath, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ValidateOnEditorLoad()
        {
            ValidateAndReport(logSuccess: false);
        }

        private static void EnsureAttackEventsOnEditorLoad()
        {
            EnsureAttackAnimationEvents(logSuccess: false);
        }

        private static void EnsureAttackAnimationEvents(bool logSuccess)
        {
            bool hasChanged = false;

            for (int presetIndex = 0; presetIndex < AttackClipPresets.Length; presetIndex++)
            {
                AttackClipEventPreset preset = AttackClipPresets[presetIndex];
                if (TryApplyAttackClipPreset(preset, out bool changed))
                {
                    hasChanged |= changed;
                }
            }

            if (logSuccess)
            {
                if (hasChanged)
                {
                    Debug.Log("[PlayerAnimatorGuard] 공격 이벤트 자동 보정 완료");
                }
                else
                {
                    Debug.Log("[PlayerAnimatorGuard] 공격 이벤트가 이미 정상 상태입니다.");
                }
            }
        }

        private static bool TryApplyAttackClipPreset(AttackClipEventPreset preset, out bool changed)
        {
            changed = false;

            ModelImporter importer = AssetImporter.GetAtPath(preset.AssetPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[PlayerAnimatorGuard] 애니메이션 클립을 찾을 수 없습니다: {preset.AssetPath}");
                return false;
            }

            int clipIndex = FindClipIndex(clips, preset.ClipName);
            if (clipIndex < 0)
            {
                Debug.LogWarning($"[PlayerAnimatorGuard] 클립 이름을 찾을 수 없습니다: {preset.ClipName} ({preset.AssetPath})");
                return false;
            }

            ModelImporterClipAnimation clip = clips[clipIndex];
            if (HasExpectedHitEventPair(clip.events, preset.HitStartTime, preset.HitEndTime))
            {
                return true;
            }

            clip.events = MergeAttackHitEvents(clip.events, preset.HitStartTime, preset.HitEndTime);
            clips[clipIndex] = clip;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            changed = true;
            return true;
        }

        private static int FindClipIndex(ModelImporterClipAnimation[] clips, string clipName)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (string.Equals(clips[i].name, clipName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasExpectedHitEventPair(AnimationEvent[] events, float expectedStartTime, float expectedEndTime)
        {
            if (events == null || events.Length == 0)
            {
                return false;
            }

            bool hasStart = false;
            bool hasEnd = false;

            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent animationEvent = events[i];
                if (string.Equals(animationEvent.functionName, AnimEventHitStart, StringComparison.Ordinal) &&
                    Mathf.Abs(animationEvent.time - expectedStartTime) <= EventTimeTolerance)
                {
                    hasStart = true;
                }
                else if (string.Equals(animationEvent.functionName, AnimEventHitEnd, StringComparison.Ordinal) &&
                         Mathf.Abs(animationEvent.time - expectedEndTime) <= EventTimeTolerance)
                {
                    hasEnd = true;
                }
            }

            return hasStart && hasEnd;
        }

        private static AnimationEvent[] MergeAttackHitEvents(AnimationEvent[] existingEvents, float hitStartTime, float hitEndTime)
        {
            var mergedEvents = new List<AnimationEvent>();

            if (existingEvents != null)
            {
                for (int i = 0; i < existingEvents.Length; i++)
                {
                    AnimationEvent animationEvent = existingEvents[i];
                    if (string.Equals(animationEvent.functionName, AnimEventHitStart, StringComparison.Ordinal) ||
                        string.Equals(animationEvent.functionName, AnimEventHitEnd, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    mergedEvents.Add(animationEvent);
                }
            }

            mergedEvents.Add(new AnimationEvent { time = hitStartTime, functionName = AnimEventHitStart });
            mergedEvents.Add(new AnimationEvent { time = hitEndTime, functionName = AnimEventHitEnd });
            mergedEvents.Sort((left, right) => left.time.CompareTo(right.time));
            return mergedEvents.ToArray();
        }

        private static void ValidateAndReport(bool logSuccess)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[PlayerAnimatorGuard] PlayerAnimator를 찾을 수 없습니다: {PlayerAnimatorControllerPath}");
                return;
            }

            if (controller.layers == null || controller.layers.Length == 0)
            {
                Debug.LogError("[PlayerAnimatorGuard] PlayerAnimator에 Animator Layer가 없습니다.", controller);
                return;
            }

            Dictionary<string, AnimatorState> stateMap = BuildStateMapRecursive(controller);
            bool hasIssue = false;

            ValidateRequiredStates(stateMap, controller, ref hasIssue);
            ValidateRequiredParameters(controller, ref hasIssue);
            ValidateAttackHitEvents(stateMap, ref hasIssue);

            if (stateMap.TryGetValue(PlayerController.ANIM_STATE_LOCOMOTION, out AnimatorState locomotionState))
            {
                ValidateLocomotionBlendTree(locomotionState, ref hasIssue);
            }

            if (!hasIssue && logSuccess)
            {
                Debug.Log("[PlayerAnimatorGuard] PlayerAnimator 검증 통과", controller);
            }
        }

        private static void ValidateRequiredStates(
            Dictionary<string, AnimatorState> stateMap,
            AnimatorController controller,
            ref bool hasIssue)
        {
            foreach (string stateName in RequiredStates)
            {
                if (!stateMap.TryGetValue(stateName, out AnimatorState state))
                {
                    Debug.LogError($"[PlayerAnimatorGuard] 필수 상태 누락: {stateName}", controller);
                    hasIssue = true;
                    continue;
                }

                if (state.motion == null)
                {
                    Debug.LogError($"[PlayerAnimatorGuard] 모션 누락 상태: {stateName}", state);
                    hasIssue = true;
                }
            }
        }

        private static void ValidateRequiredParameters(AnimatorController controller, ref bool hasIssue)
        {
            ValidateParameter(controller, AnimParamSpeed, AnimatorControllerParameterType.Float, ref hasIssue);
            ValidateParameter(controller, AnimParamHit, AnimatorControllerParameterType.Trigger, ref hasIssue);
        }

        private static void ValidateParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType expectedType,
            ref bool hasIssue)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (!string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (parameter.type != expectedType)
                {
                    Debug.LogError(
                        $"[PlayerAnimatorGuard] 파라미터 타입 불일치: {parameterName} (Expected: {expectedType}, Actual: {parameter.type})",
                        controller);
                    hasIssue = true;
                }

                return;
            }

            Debug.LogError($"[PlayerAnimatorGuard] 필수 파라미터 누락: {parameterName}", controller);
            hasIssue = true;
        }

        private static Dictionary<string, AnimatorState> BuildStateMapRecursive(AnimatorController controller)
        {
            var stateMap = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                AnimatorStateMachine rootStateMachine = layers[i].stateMachine;
                if (rootStateMachine == null)
                {
                    continue;
                }

                CollectStatesRecursive(rootStateMachine, stateMap);
            }

            return stateMap;
        }

        private static void CollectStatesRecursive(
            AnimatorStateMachine stateMachine,
            Dictionary<string, AnimatorState> stateMap)
        {
            ChildAnimatorState[] childStates = stateMachine.states;
            for (int i = 0; i < childStates.Length; i++)
            {
                AnimatorState state = childStates[i].state;
                if (state == null)
                {
                    continue;
                }

                if (!stateMap.TryAdd(state.name, state))
                {
                    Debug.LogWarning($"[PlayerAnimatorGuard] 중복 상태명 감지: {state.name}", state);
                }
            }

            ChildAnimatorStateMachine[] childStateMachines = stateMachine.stateMachines;
            for (int i = 0; i < childStateMachines.Length; i++)
            {
                AnimatorStateMachine child = childStateMachines[i].stateMachine;
                if (child == null)
                {
                    continue;
                }

                CollectStatesRecursive(child, stateMap);
            }
        }

        private static void ValidateLocomotionBlendTree(AnimatorState locomotionState, ref bool hasIssue)
        {
            if (!(locomotionState.motion is BlendTree blendTree))
            {
                Debug.LogError("[PlayerAnimatorGuard] Locomotion 상태 모션은 BlendTree여야 합니다.", locomotionState);
                hasIssue = true;
                return;
            }

            ChildMotion[] children = blendTree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion != null)
                {
                    continue;
                }

                Debug.LogError($"[PlayerAnimatorGuard] Locomotion BlendTree 자식 모션 누락 (index: {i})", blendTree);
                hasIssue = true;
            }
        }

        private static void ValidateAttackHitEvents(
            Dictionary<string, AnimatorState> stateMap,
            ref bool hasIssue)
        {
            for (int i = 0; i < AttackStates.Length; i++)
            {
                string attackStateName = AttackStates[i];
                if (!stateMap.TryGetValue(attackStateName, out AnimatorState attackState) || attackState.motion == null)
                {
                    continue;
                }

                if (!(attackState.motion is AnimationClip clip))
                {
                    Debug.LogError($"[PlayerAnimatorGuard] {attackStateName} 모션은 AnimationClip이어야 합니다.", attackState);
                    hasIssue = true;
                    continue;
                }

                AnimationEvent[] animationEvents = AnimationUtility.GetAnimationEvents(clip);
                bool hasHitStart = false;
                bool hasHitEnd = false;
                float hitStartTime = 0f;
                float hitEndTime = 0f;

                for (int eventIndex = 0; eventIndex < animationEvents.Length; eventIndex++)
                {
                    AnimationEvent animationEvent = animationEvents[eventIndex];
                    if (string.Equals(animationEvent.functionName, AnimEventHitStart, StringComparison.Ordinal))
                    {
                        if (!hasHitStart || animationEvent.time < hitStartTime)
                        {
                            hitStartTime = animationEvent.time;
                        }

                        hasHitStart = true;
                    }
                    else if (string.Equals(animationEvent.functionName, AnimEventHitEnd, StringComparison.Ordinal))
                    {
                        if (!hasHitEnd || animationEvent.time < hitEndTime)
                        {
                            hitEndTime = animationEvent.time;
                        }

                        hasHitEnd = true;
                    }
                }

                if (!hasHitStart || !hasHitEnd)
                {
                    Debug.LogError(
                        $"[PlayerAnimatorGuard] {attackStateName} 이벤트 누락: {AnimEventHitStart}/{AnimEventHitEnd} 둘 다 필요합니다.",
                        clip);
                    hasIssue = true;
                    continue;
                }

                if (hitStartTime >= hitEndTime)
                {
                    Debug.LogError(
                        $"[PlayerAnimatorGuard] {attackStateName} 이벤트 순서 오류: {AnimEventHitStart}({hitStartTime:0.###}) >= {AnimEventHitEnd}({hitEndTime:0.###})",
                        clip);
                    hasIssue = true;
                }

                if (hitEndTime > clip.length + 0.001f)
                {
                    Debug.LogError(
                        $"[PlayerAnimatorGuard] {attackStateName} 이벤트 시간이 클립 길이를 초과했습니다. end={hitEndTime:0.###}, clip={clip.length:0.###}",
                        clip);
                    hasIssue = true;
                }
            }
        }
    }

    /// <summary>
    /// PlayerAnimator.controller가 저장/재임포트될 때 자동 검증을 실행한다.
    /// </summary>
    internal sealed class PlayerAnimatorImportPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool hasControllerChange = ContainsTargetPath(importedAssets) || ContainsTargetPath(movedAssets);
            bool hasAttackClipChange = PlayerAnimatorGuard.ContainsAttackClipPath(importedAssets) ||
                                       PlayerAnimatorGuard.ContainsAttackClipPath(movedAssets);

            if (hasAttackClipChange)
            {
                // 수동 패키지 임포트 직후 공격 이벤트 누락을 자동 보정.
                PlayerAnimatorGuard.EnsureAttackEventsAfterImport();
            }

            if (hasControllerChange || hasAttackClipChange)
            {
                // 상태/모션 누락을 즉시 드러내기 위한 자동 검증.
                PlayerAnimatorGuard.ValidateAfterImport();
            }
        }

        private static bool ContainsTargetPath(string[] assetPaths)
        {
            for (int i = 0; i < assetPaths.Length; i++)
            {
                if (string.Equals(assetPaths[i], PlayerAnimatorGuard.PlayerAnimatorControllerPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
