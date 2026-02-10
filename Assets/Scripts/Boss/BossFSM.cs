using UnityEngine;

namespace Core.Boss
{
    // ==================================================================================
    // Concrete Boss States
    // ==================================================================================

    public class BossIdleState : BossBaseState
    {
        public BossIdleState(BossController controller) : base(controller) { }

        public override void Enter()
        {
            Controller.StopMoving(); // Visual null-safe
        }

        public override void Update()
        {
            if (Controller.Target == null) return;

            float distance = Vector3.Distance(Controller.transform.position, Controller.Target.position);

            // 감지 범위 내 & 시야 확보 시 전투 전환
            if (distance <= Controller.DetectionRange && Controller.CheckLineOfSight())
            {
                Controller.StateMachine.ChangeState(Controller.CombatState);
            }
        }

        public override void Exit() { }
    }

    public class BossCombatState : BossBaseState
    {
        public BossCombatState(BossController controller) : base(controller) { }

        public override void Enter() { }

        public override void Update()
        {
            if (Controller.Target == null)
            {
                Controller.StateMachine.ChangeState(Controller.IdleState);
                return;
            }

            float distance = Vector3.Distance(Controller.transform.position, Controller.Target.position);

            // 범위 벗어남 -> 수색
            if (distance > Controller.DetectionRange)
            {
                Controller.StateMachine.ChangeState(Controller.SearchingState);
                return;
            }

            // 공격 사거리 체크
            if (distance > Controller.AttackRange)
            {
                Controller.MoveTo(Controller.Target.position, Controller.MoveSpeed);
            }
            else
            {
                Controller.StopMoving();
                // 임시로 바라보기만 함
                Controller.RotateTowards(Controller.Target.position);
            }
        }

        public override void Exit() { }
    }

    public class BossSearchingState : BossBaseState
    {
        private float _timer;
        private Vector3 _lastKnownPos;

        public BossSearchingState(BossController controller) : base(controller) { }

        public override void Enter()
        {
            Controller.Visual?.SetSearchingUI(true);
            _timer = Controller.SearchDuration;
            _lastKnownPos = Controller.Target != null ? Controller.Target.position : Controller.transform.position;
        }

        public override void Update()
        {
            _timer -= Time.deltaTime;

            // 재탐색 성공 시 Combat 복귀
            if (Controller.Target != null)
            {
                float distance = Vector3.Distance(Controller.transform.position, Controller.Target.position);
                if (distance <= Controller.DetectionRange && Controller.CheckLineOfSight())
                {
                    Controller.StateMachine.ChangeState(Controller.CombatState);
                    return;
                }
            }

            // 시간 초과 -> Idle
            if (_timer <= 0)
            {
                Controller.StateMachine.ChangeState(Controller.IdleState);
                return;
            }

            // 마지막 위치로 이동
            if (Vector3.Distance(Controller.transform.position, _lastKnownPos) > 0.5f)
            {
                Controller.MoveTo(_lastKnownPos, Controller.SearchingMoveSpeed);
            }
            else
            {
                Controller.StopMoving();
            }
        }

        public override void Exit()
        {
            Controller.Visual?.SetSearchingUI(false);
        }
    }

    public class BossHitState : BossBaseState
    {
        private float _timer;
        private const float StunDuration = 0.5f;

        public BossHitState(BossController controller) : base(controller) { }

        public override void Enter()
        {
            Controller.StopMoving();
            Controller.Visual?.TriggerHit();
            _timer = StunDuration;
        }

        public override void Update()
        {
            _timer -= Time.deltaTime;

            if (_timer <= 0)
            {
                // 경직 종료 후 상태 결정
                if (Controller.Target != null &&
                    Vector3.Distance(Controller.transform.position, Controller.Target.position) <= Controller.DetectionRange)
                {
                    Controller.StateMachine.ChangeState(Controller.CombatState);
                }
                else
                {
                    Controller.StateMachine.ChangeState(Controller.IdleState);
                }
            }
        }

        public override void Exit() { }
    }

    public class BossDeadState : BossBaseState
    {
        public BossDeadState(BossController controller) : base(controller) { }

        public override void Enter()
        {
            Controller.Visual?.TriggerDie();
            if (Controller.TryGetComponent(out CharacterController cc))
            {
                cc.enabled = false; // 물리 비활성화
            }
        }

        public override void Update() { } // 아무것도 안 함

        public override void Exit() { }
    }
}
