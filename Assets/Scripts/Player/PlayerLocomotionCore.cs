using Core.Multiplayer;
using UnityEngine;

namespace Core.Player
{
    /// <summary>
    /// 솔로와 멀티플레이가 함께 사용하는 이동 시뮬레이션 핵심 로직이다.
    /// </summary>
    public static class PlayerLocomotionCore
    {
        public static MultiplayerLocomotionState CaptureCurrentState(PlayerController controller, int inputSequence, int serverTick, bool allowsPrediction)
        {
            CharacterController characterController = controller.CharController;
            bool isDashActive = controller.IsDashStateActive;
            return new MultiplayerLocomotionState
            {
                InputSequence = inputSequence,
                ServerTick = serverTick,
                Position = controller.transform.position,
                Yaw = controller.transform.eulerAngles.y,
                PlanarVelocity = ResolveCurrentPlanarVelocity(characterController),
                VerticalVelocity = ResolveCurrentVerticalVelocity(characterController, controller.NetworkLocomotionGroundedGravityValue),
                JumpTimer = 0f,
                DashTimer = isDashActive ? controller.DashTimerRemaining : 0f,
                DashCooldownTimer = controller.DashCooldownRemaining,
                LastButtons = controller.CurrentInputButtons,
                AllowsPrediction = allowsPrediction,
                IsGrounded = characterController != null && characterController.isGrounded,
                IsDashActive = isDashActive
            };
        }

        public static void ApplyState(PlayerController controller, in MultiplayerLocomotionState state)
        {
            CharacterController characterController = controller.CharController;
            bool wasCharacterControllerEnabled = characterController != null && characterController.enabled;
            if (wasCharacterControllerEnabled)
            {
                characterController.enabled = false;
            }

            controller.transform.SetPositionAndRotation(state.Position, Quaternion.Euler(0f, state.Yaw, 0f));

            if (wasCharacterControllerEnabled)
            {
                characterController.enabled = true;
            }

            controller.SyncNetworkDashState(state.DashTimer, state.DashCooldownTimer);
        }

        public static MultiplayerLocomotionState SimulateTick(
            PlayerController controller,
            in MultiplayerLocomotionState currentState,
            in PlayerInputPacket input,
            float deltaTime,
            int inputSequence,
            int serverTick,
            bool allowsPrediction,
            bool updateAnimator)
        {
            CharacterController characterController = controller.CharController;
            float verticalVelocity = currentState.VerticalVelocity;
            bool isGrounded = currentState.IsGrounded;
            Vector3 moveDirection = GetMovementDirectionFromLook(input.moveDir, input.lookYaw);
            float nextYaw = currentState.Yaw;
            Vector3 nextPlanarVelocity = currentState.PlanarVelocity;
            bool wasDashActive = currentState.IsDashActive;
            float nextDashTimer = Mathf.Max(0f, currentState.DashTimer);
            float nextDashCooldownTimer = Mathf.Max(0f, currentState.DashCooldownTimer - deltaTime);
            bool dashPressed = input.HasFlag(InputFlag.Dash);
            bool previousDashPressed = (currentState.LastButtons & (byte)InputFlag.Dash) != 0;
            bool dashStartedThisTick = false;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                float targetYaw = Quaternion.LookRotation(moveDirection).eulerAngles.y;
                float rotationBlend = 1f - Mathf.Exp(-controller.RotationSpeed * deltaTime);
                nextYaw = Mathf.LerpAngle(currentState.Yaw, targetYaw, rotationBlend);
            }

            Vector3 previousPosition = controller.transform.position;
            if (wasDashActive)
            {
                controller.transform.rotation = Quaternion.Euler(0f, currentState.Yaw, 0f);
                Vector3 dashDirection = ResolveForwardFromYaw(currentState.Yaw);
                Vector3 dashVelocity = dashDirection * (controller.MoveSpeed * controller.DashSpeedMultiplier);

                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(dashVelocity * deltaTime);
                    isGrounded = characterController.isGrounded;
                    Vector3 actualDelta = controller.transform.position - previousPosition;
                    nextPlanarVelocity = new Vector3(actualDelta.x, 0f, actualDelta.z) / Mathf.Max(deltaTime, 0.0001f);
                }
                else
                {
                    nextPlanarVelocity = dashVelocity;
                }

                nextYaw = currentState.Yaw;
                nextDashTimer = Mathf.Max(0f, currentState.DashTimer - deltaTime);
            }
            else
            {
                controller.transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);

                if (isGrounded && verticalVelocity < 0f)
                {
                    verticalVelocity = controller.NetworkLocomotionGroundedGravityValue;
                }

                verticalVelocity += controller.Gravity * deltaTime;

                if (characterController != null && characterController.enabled)
                {
                    Vector3 finalVelocity = (moveDirection * controller.MoveSpeed) + Vector3.up * verticalVelocity;
                    characterController.Move(finalVelocity * deltaTime);
                    isGrounded = characterController.isGrounded;
                    Vector3 actualDelta = controller.transform.position - previousPosition;
                    nextPlanarVelocity = new Vector3(actualDelta.x, 0f, actualDelta.z) / Mathf.Max(deltaTime, 0.0001f);
                }
                else
                {
                    nextPlanarVelocity = moveDirection * controller.MoveSpeed;
                }

                if (nextDashCooldownTimer <= 0f && dashPressed && !previousDashPressed)
                {
                    Vector3 dashStartDirection = moveDirection.sqrMagnitude > 0.0001f
                        ? moveDirection
                        : ResolveForwardFromYaw(currentState.Yaw);

                    if (dashStartDirection.sqrMagnitude > 0.0001f)
                    {
                        nextYaw = Quaternion.LookRotation(dashStartDirection).eulerAngles.y;
                        controller.transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
                    }

                    nextDashTimer = controller.DashDuration;
                    nextDashCooldownTimer = Mathf.Max(nextDashCooldownTimer, controller.DashCooldown);
                    dashStartedThisTick = true;
                }
            }

            bool nextDashActive = dashStartedThisTick || nextDashTimer > 0f;
            bool dashEndedThisTick = wasDashActive && !nextDashActive;
            if (dashEndedThisTick && input.moveDir.sqrMagnitude <= 0.0001f)
            {
                // 대시 종료 직후 입력이 없으면 마지막 대시 속도를 보행 블렌드에 넘기지 않는다.
                nextPlanarVelocity = Vector3.zero;
            }

            float locomotionBlendSpeed = ResolveLocomotionBlendSpeed(controller, input.moveDir.magnitude, nextPlanarVelocity);
            bool shouldUseFrameDrivenLocomotionAnimatorSpeed = controller.ShouldUseFrameDrivenPredictedLocomotionAnimatorSpeed();

            if (updateAnimator && controller.Animator != null)
            {
                if (dashStartedThisTick)
                {
                    controller.Animator.CrossFade(PlayerController.ANIM_STATE_DASH, 0.05f);
                }
                else if (dashEndedThisTick)
                {
                    controller.Animator.CrossFade(PlayerController.ANIM_STATE_LOCOMOTION, 0.05f);
                    if (!shouldUseFrameDrivenLocomotionAnimatorSpeed)
                    {
                        controller.SetLocomotionAnimatorSpeed(locomotionBlendSpeed, deltaTime);
                    }
                }
                else if (!nextDashActive)
                {
                    if (!shouldUseFrameDrivenLocomotionAnimatorSpeed)
                    {
                        controller.SetLocomotionAnimatorSpeed(locomotionBlendSpeed, deltaTime);
                    }
                }
            }

            MultiplayerLocomotionState nextState = new MultiplayerLocomotionState
            {
                InputSequence = inputSequence,
                ServerTick = serverTick,
                Position = controller.transform.position,
                Yaw = nextYaw,
                PlanarVelocity = nextPlanarVelocity,
                VerticalVelocity = verticalVelocity,
                JumpTimer = currentState.JumpTimer,
                DashTimer = nextDashActive ? nextDashTimer : 0f,
                DashCooldownTimer = nextDashCooldownTimer,
                LastButtons = input.buttons,
                AllowsPrediction = allowsPrediction,
                IsGrounded = isGrounded,
                IsDashActive = nextDashActive
            };

            controller.SyncNetworkDashState(nextState.DashTimer, nextState.DashCooldownTimer);
            return nextState;
        }

        private static Vector3 GetMovementDirectionFromLook(Vector2 inputDir, float lookYaw)
        {
            if (inputDir.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            Quaternion lookRotation = Quaternion.Euler(0f, lookYaw, 0f);
            Vector3 camForward = lookRotation * Vector3.forward;
            Vector3 camRight = lookRotation * Vector3.right;
            return (camForward * inputDir.y + camRight * inputDir.x).normalized;
        }

        private static Vector3 ResolveForwardFromYaw(float yaw)
        {
            return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        }

        private static float ResolveLocomotionBlendSpeed(PlayerController controller, float inputMagnitude, Vector3 planarVelocity)
        {
            float normalizedPlanarSpeed = 0f;
            if (controller.MoveSpeed > 0.0001f)
            {
                normalizedPlanarSpeed = planarVelocity.magnitude / controller.MoveSpeed;
            }

            return Mathf.Clamp01(Mathf.Max(inputMagnitude, normalizedPlanarSpeed));
        }

        private static float ResolveCurrentVerticalVelocity(CharacterController characterController, float groundedGravity)
        {
            if (characterController == null)
            {
                return 0f;
            }

            if (characterController.isGrounded)
            {
                return groundedGravity;
            }

            return characterController.velocity.y;
        }

        private static Vector3 ResolveCurrentPlanarVelocity(CharacterController characterController)
        {
            if (characterController == null)
            {
                return Vector3.zero;
            }

            Vector3 controllerVelocity = characterController.velocity;
            controllerVelocity.y = 0f;
            return controllerVelocity;
        }
    }
}
