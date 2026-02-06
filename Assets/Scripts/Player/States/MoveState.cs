using BossRaid.Patterns;
using UnityEngine;

public class MoveState : BaseState
{
    private float _verticalVelocity;
    private bool _wasDashPressed;
    private bool _wasJumpPressed;

    // 설정값 (PlayerController 값 참조 예정)

    public MoveState(PlayerController controller) : base(controller)
    {
    }

    public override void Enter()
    {
        // 수직 속도 초기화
        _verticalVelocity = 0;
        _wasDashPressed = false;
        _wasJumpPressed = false;

        // Animation: Locomotion Blend Tree (Speed = 0 for Idle)
        if (Controller.Animator != null)
        {
            Controller.Animator.CrossFade(PlayerController.ANIM_STATE_LOCOMOTION, 0.1f);
            Controller.Animator.SetFloat(PlayerController.ANIM_PARAM_SPEED, 0f);
        }
    }

    public override void Update(PlayerInputPacket input)
    {
        // 회전 및 이동 처리
        UpdateRotation(input);
        UpdateMovement(input);

        // Animation: Update locomotion blend
        if (Controller.Animator != null)
            Controller.Animator.SetFloat(PlayerController.ANIM_PARAM_SPEED, input.moveDir.magnitude);

        // Attack Transition (지면/공중 모두 가능)
        if (input.HasFlag(InputFlag.Attack))
        {
            Controller.StateMachine.ChangeState(Controller.AttackState);
            return;
        }

        // Jump Transition (엣지 트리거 + 지면 체크)
        bool jumpPressed = input.HasFlag(InputFlag.Jump);
        if (jumpPressed && !_wasJumpPressed && Controller.CharController.isGrounded)
        {
            Controller.StateMachine.ChangeState(Controller.JumpState);
            return;
        }
        _wasJumpPressed = jumpPressed;

        // Dash Transition (엣지 트리거 + 쿨타임 체크)
        bool dashPressed = input.HasFlag(InputFlag.Dash);
        if (dashPressed && !_wasDashPressed && Controller.CanDash)
        {
            Controller.StateMachine.ChangeState(Controller.DashState);
        }
        _wasDashPressed = dashPressed;
    }

    public override void Exit()
    {
        // Cleanup
    }

    private void UpdateRotation(PlayerInputPacket input)
    {
        // 카메라 방향 기준 캐릭터 회전
        Transform cameraRoot = Controller.CameraRoot;
        Transform playerTransform = Controller.transform;

        Vector3 camForward = cameraRoot.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cameraRoot.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDirection = (camForward * input.moveDir.y + camRight * input.moveDir.x).normalized;

        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, targetRotation, Controller.RotationSpeed * Time.deltaTime);
        }

        // Calc Velocity
        if (Controller.CharController.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f;
        }
        _verticalVelocity += Controller.Gravity * Time.deltaTime;

        Vector3 finalVelocity = (moveDirection * Controller.MoveSpeed) + Vector3.up * _verticalVelocity;
        Controller.CharController.Move(finalVelocity * Time.deltaTime);
    }

    private void UpdateMovement(PlayerInputPacket input)
    {
        // Merged into UpdateRotation for consistency with original code flow
    }
}
