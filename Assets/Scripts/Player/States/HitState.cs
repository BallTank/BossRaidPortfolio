using UnityEngine;

namespace Core.Player.States
{
    public class HitState : PlayerBaseState
    {
        private float _timer;
        private float _verticalVelocity;
        private const float StunDuration = 0.5f;
        private const float GroundedGravity = -2.0f;

        public HitState(PlayerController controller) : base(controller) { }

        public override void Enter()
        {
            // 비주얼 담당 클래스에 위임
            Controller.Visual?.TriggerHit();

            _timer = StunDuration;

            // 초기 수직 속도 설정
            if (Controller.CharController.isGrounded)
            {
                _verticalVelocity = GroundedGravity;
            }
            else
            {
                _verticalVelocity = 0f;
            }
        }

        public override void Update(PlayerInputPacket input)
        {
            _timer -= Time.deltaTime;

            // 중력 적용 (공중 체공 방지)
            if (Controller.CharController.isGrounded && _verticalVelocity < 0)
            {
                _verticalVelocity = GroundedGravity;
            }
            _verticalVelocity += Controller.Gravity * Time.deltaTime;

            Controller.CharController.Move(Vector3.up * _verticalVelocity * Time.deltaTime);

            if (_timer <= 0)
            {
                Controller.StateMachine.ChangeState(Controller.MoveState);
            }
        }

        public override void Exit() { }
    }

    public class StunState : PlayerBaseState
    {
        private const float GroundedGravity = -2.0f;

        private float _stunTimer;
        private float _verticalVelocity;
        private Vector3 _pushDirection;
        private float _pushbackDuration;
        private float _pushbackTimer;
        private float _pushbackSpeed;

        public StunState(PlayerController controller) : base(controller) { }

        public void Configure(float duration, Vector3 pushDirection, float pushDistance, float pushDuration)
        {
            _stunTimer = Mathf.Max(0f, duration);
            _pushDirection = pushDirection;
            _pushDirection.y = 0f;
            if (_pushDirection.sqrMagnitude > 0.0001f)
            {
                _pushDirection.Normalize();
            }
            else
            {
                _pushDirection = -Controller.transform.forward;
            }

            _pushbackDuration = Mathf.Max(0f, pushDuration);
            _pushbackTimer = _pushbackDuration;
            _pushbackSpeed = _pushbackDuration > 0.0001f
                ? Mathf.Max(0f, pushDistance) / _pushbackDuration
                : 0f;
        }

        public override void Enter()
        {
            if (Controller.Animator != null)
            {
                // Animator 상태 해시 경로 차이로 미검출되는 경우를 피하기 위해
                // 상태명 직접 CrossFade를 우선 적용한다.
                Controller.Animator.CrossFade(PlayerController.ANIM_STATE_STUN, 0.05f);
            }

            if (Controller.CharController.isGrounded)
            {
                _verticalVelocity = GroundedGravity;
            }
            else
            {
                _verticalVelocity = Controller.CharController.velocity.y;
            }
        }

        public override void Update(PlayerInputPacket input)
        {
            _stunTimer -= Time.deltaTime;

            Vector3 movement = Vector3.zero;
            if (_pushbackTimer > 0f && _pushbackSpeed > 0f)
            {
                movement += _pushDirection * (_pushbackSpeed * Time.deltaTime);
                _pushbackTimer -= Time.deltaTime;
            }

            if (Controller.CharController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = GroundedGravity;
            }
            _verticalVelocity += Controller.Gravity * Time.deltaTime;
            movement += Vector3.up * (_verticalVelocity * Time.deltaTime);

            Controller.CharController.Move(movement);

            if (_stunTimer <= 0f)
            {
                Controller.HandleStunFinished();
            }
        }

        public override void Exit() { }
    }
}
