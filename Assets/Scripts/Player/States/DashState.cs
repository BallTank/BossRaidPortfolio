using BossRaid.Patterns;
using UnityEngine;

public class DashState : BaseState
{
    private float _timer;
    private Vector3 _dashDirection;
    private readonly IDashContext _dashContext;

    public DashState(PlayerController controller, IDashContext dashContext) : base(controller)
    {
        _dashContext = dashContext;
    }

    public override void Enter()
    {
        _timer = 0f;

        // 쿨타임 시작
        _dashContext.StartDashCooldown();

        // 진입 시 캐릭터 전방으로 대시 방향 고정
        _dashDirection = _dashContext.transform.forward;

        // Animation: Play Dash (Quickshift_F)
        if (Controller.Animator != null)
            Controller.Animator.CrossFade(PlayerController.ANIM_STATE_DASH, 0.1f);
    }

    public override void Update(PlayerInputPacket input)
    {
        _timer += Time.deltaTime;

        // 중력 무시하고 대시 속도로 이동
        Vector3 dashVelocity = _dashDirection * (_dashContext.MoveSpeed * _dashContext.DashSpeedMultiplier);
        _dashContext.CharController.Move(dashVelocity * Time.deltaTime);

        if (_timer >= _dashContext.DashDuration)
        {
            _dashContext.StateMachine.ChangeState(_dashContext.MoveState);
        }
    }

    public override void Exit()
    {
        // Reset velocity or state?
    }
}
