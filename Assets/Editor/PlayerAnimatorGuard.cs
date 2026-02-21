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

        static PlayerAnimatorGuard()
        {
            // 에디터 리로드 직후 한 번 자동 검증.
            EditorApplication.delayCall += ValidateOnEditorLoad;
        }

        [MenuItem("Tools/Validation/Validate Player Animator")]
        private static void ValidateFromMenu()
        {
            ValidateAndReport(logSuccess: true);
        }

        internal static void ValidateAfterImport()
        {
            ValidateAndReport(logSuccess: false);
        }

        private static void ValidateOnEditorLoad()
        {
            ValidateAndReport(logSuccess: false);
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
            if (ContainsTargetPath(importedAssets) || ContainsTargetPath(movedAssets))
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
