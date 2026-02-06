using BossRaid.Patterns;
using UnityEngine;

public class JumpState : BaseState
{
    private float _verticalVelocity;
    private bool _wasDashPressed;

    public JumpState(PlayerController controller) : base(controller)
    {
    }

    public override void Enter()
    {
        // 점프 시작: 초기 수직 속도 설정
        _verticalVelocity = Controller.JumpForce;
        _wasDashPressed = false;

        // Animation: Play Jump
        if (Controller.Animator != null)
            Controller.Animator.CrossFade(PlayerController.ANIM_STATE_JUMP, 0.1f);
    }

    public override void Update(PlayerInputPacket input)
    {
        // 수평 이동 처리 (공중에서도 조작 가능)
        UpdateAirMovement(input);

        // 중력 적용
        _verticalVelocity += Controller.Gravity * Time.deltaTime;

        // 수직 이동 적용
        Vector3 verticalMove = Vector3.up * _verticalVelocity * Time.deltaTime;
        Controller.CharController.Move(verticalMove);

        // 착지 체크: 하강 중 + 지면 접촉
        if (_verticalVelocity < 0 && Controller.CharController.isGrounded)
        {
            Controller.StateMachine.ChangeState(Controller.MoveState);
            return;
        }

        // Attack Transition
        if (input.HasFlag(InputFlag.Attack))
        {
            Controller.StateMachine.ChangeState(Controller.AttackState);
            return;
        }

        // 공중 대시 전환 (선택적: 엣지 트리거 + 쿨타임)
        bool dashPressed = input.HasFlag(InputFlag.Dash);
        if (dashPressed && !_wasDashPressed && Controller.CanDash)
        {
            Controller.StateMachine.ChangeState(Controller.DashState);
        }
        _wasDashPressed = dashPressed;
    }

    public override void Exit()
    {
        // Cleanup if needed
    }

    private void UpdateAirMovement(PlayerInputPacket input)
    {
        // 카메라 기준 이동 방향 계산
        Transform cameraRoot = Controller.CameraRoot;

        Vector3 camForward = cameraRoot.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cameraRoot.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDirection = (camForward * input.moveDir.y + camRight * input.moveDir.x).normalized;

        // 캐릭터 회전 (이동 방향으로)
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            Controller.transform.rotation = Quaternion.Slerp(
                Controller.transform.rotation,
                targetRotation,
                Controller.RotationSpeed * Time.deltaTime
            );
        }

        // 수평 이동 (공중 조작 계수 적용)
        Vector3 horizontalMove = moveDirection * (Controller.MoveSpeed * Controller.AirControl) * Time.deltaTime;
        Controller.CharController.Move(horizontalMove);
    }
}
