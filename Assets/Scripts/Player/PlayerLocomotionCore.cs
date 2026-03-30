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
            return new MultiplayerLocomotionState
            {
                InputSequence = inputSequence,
                ServerTick = serverTick,
                Position = controller.transform.position,
                Yaw = controller.transform.eulerAngles.y,
                PlanarVelocity = ResolveCurrentPlanarVelocity(characterController),
                VerticalVelocity = ResolveCurrentVerticalVelocity(characterController, controller.NetworkLocomotionGroundedGravityValue),
                JumpTimer = 0f,
                AllowsPrediction = allowsPrediction,
                IsGrounded = characterController != null && characterController.isGrounded
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

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                float targetYaw = Quaternion.LookRotation(moveDirection).eulerAngles.y;
                float rotationBlend = 1f - Mathf.Exp(-controller.RotationSpeed * deltaTime);
                nextYaw = Mathf.LerpAngle(currentState.Yaw, targetYaw, rotationBlend);
            }

            Vector3 previousPosition = controller.transform.position;
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

            if (updateAnimator && controller.Animator != null)
            {
                controller.Animator.SetFloat(PlayerController.ANIM_PARAM_SPEED, input.moveDir.magnitude);
            }

            return new MultiplayerLocomotionState
            {
                InputSequence = inputSequence,
                ServerTick = serverTick,
                Position = controller.transform.position,
                Yaw = nextYaw,
                PlanarVelocity = nextPlanarVelocity,
                VerticalVelocity = verticalVelocity,
                JumpTimer = currentState.JumpTimer,
                AllowsPrediction = allowsPrediction,
                IsGrounded = isGrounded
            };
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
