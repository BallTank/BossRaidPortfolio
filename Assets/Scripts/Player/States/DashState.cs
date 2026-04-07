using Core.Common.Interfaces;
using Core.Common.Patterns;
using Core.Player;
using UnityEngine;

namespace Core.Player.States
{
    public class DashState : PlayerBaseState
    {
        private float _timer;
        private Vector3 _dashDirection;
        private readonly IDashContext _dashContext;
        private float _pendingAuthoritativeElapsedTime;

        public float RemainingTime => Mathf.Max(0f, _dashContext.DashDuration - _timer);
        public Vector3 DashDirection => _dashDirection;

        public DashState(PlayerController controller, IDashContext dashContext) : base(controller)
        {
            _dashContext = dashContext;
        }

        public void SetAuthoritativeElapsedTime(float elapsedTime)
        {
            _pendingAuthoritativeElapsedTime = Mathf.Max(0f, elapsedTime);
        }

        public override void Enter()
        {
            _timer = Mathf.Min(_pendingAuthoritativeElapsedTime, _dashContext.DashDuration);
            _pendingAuthoritativeElapsedTime = 0f;

            // 쿨타임 시작
            _dashContext.StartDashCooldown();

            if (Controller.TryConsumePendingAuthoritativeDashDirection(out Vector3 authoritativeDashDirection))
            {
                _dashDirection = authoritativeDashDirection;
                Controller.transform.rotation = Quaternion.LookRotation(_dashDirection);
            }
            else
            {
                // 진입 시 입력 방향으로 대시 (없으면 전방)
                Vector3 inputDir = Vector3.zero;
                if (Controller.InputProvider != null)
                {
                    PlayerInputPacket input = Controller.InputProvider.GetInput();
                    inputDir = Controller.GetMovementDirection(input.moveDir);
                }

                if (inputDir != Vector3.zero)
                {
                    _dashDirection = inputDir;
                    Controller.transform.rotation = Quaternion.LookRotation(_dashDirection);
                }
                else
                {
                    _dashDirection = Controller.transform.forward;
                }
            }

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
            Controller.ClearPendingAuthoritativeDashFacingYaw();
        }
    }
}
