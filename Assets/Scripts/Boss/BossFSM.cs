using Core.Boss.Attacks;
using UnityEngine;

namespace Core.Boss
{
    // ==================================================================================
    // 보스 상태 구현
    // ==================================================================================

    public class BossIdleState : BossBaseState
    {
        public BossIdleState(BossController controller) : base(controller) { }

        public override void Enter()
        {
            Controller.StopMoving(); // Visual 미할당이어도 안전하게 동작
        }

        public override void Update()
        {
            if (Controller.Target == null) return;

            float planarDistance = Controller.GetPlanarDistanceToTarget();

            // 감지 범위 내 & 시야 확보 시 전투 전환
            if (planarDistance <= Controller.DetectionRange && Controller.CheckLineOfSight())
            {
                Controller.StateMachine.ChangeState(Controller.CombatState);
            }
        }

        public override void Exit() { }
    }

    public class BossCombatState : BossBaseState
    {
        private IBossAttackPattern _lastPhaseOnePattern;
        private IBossAttackPattern _lastPhaseTwoPattern;
        private bool _isChasingTarget;

        public BossCombatState(BossController controller) : base(controller) { }

        public override void Enter()
        {
            Controller.EnsurePhaseIntroForCurrentPhase();

            // 공격 종료 직후 경계 지터를 줄이기 위해 재진입 버퍼를 기준으로 추적 래치를 초기화한다.
            float planarDistance = Controller.GetPlanarDistanceToTarget();
            _isChasingTarget = planarDistance > Controller.AttackRange + Controller.ChaseReengageBuffer;
        }

        public override void Update()
        {
            if (Controller.Target == null)
            {
                Controller.StateMachine.ChangeState(Controller.IdleState);
                return;
            }

            float planarDistance = Controller.GetPlanarDistanceToTarget();

            if (Controller.IsPhaseIntroPlaying)
            {
                Controller.RotateTowards(Controller.Target.position);
                return;
            }

            // 범위 벗어남 -> 수색
            if (planarDistance > Controller.DetectionRange)
            {
                Controller.StateMachine.ChangeState(Controller.SearchingState);
                return;
            }

            UpdateChaseLatch(planarDistance);

            // 추적 상태에서는 이동/원거리 패턴 전환을 우선 처리한다.
            if (_isChasingTarget)
            {
                // 공격 발동 조건이 이미 충족되면 이동 전환보다 패턴 전환을 우선한다.
                if (Controller.CanAttack && Controller.IsPhaseTwoAttackWindow && TryStartAttackState())
                {
                    return;
                }

                // 추적 기능이 켜져 있을 때만 이동 (디버그용)
                if (Controller.EnableChase)
                {
                    Controller.MoveTo(Controller.Target.position, Controller.MoveSpeed);
                }
                else
                {
                    // 추적 비활성화 시 제자리에서 회전만 수행
                    Controller.StopMoving();
                    Controller.RotateTowards(Controller.Target.position);
                }

                return;
            }

            // 근접 교전 상태에서는 Locomotion을 멈추고 공격 기회를 탐색한다.
            Controller.StopMoving();
            Controller.RotateTowards(Controller.Target.position);

            // 공격 쿨타임 확인 후 공격 전환
            if (Controller.CanAttack && TryStartAttackState())
            {
                return;
            }
        }

        private void UpdateChaseLatch(float planarDistance)
        {
            float attackRange = Controller.AttackRange;
            float chaseReengageRange = attackRange + Controller.ChaseReengageBuffer;

            if (_isChasingTarget)
            {
                // 추적 중에는 공격 사거리 안으로 충분히 들어왔을 때만 추적을 해제한다.
                if (planarDistance <= attackRange)
                {
                    _isChasingTarget = false;
                }
                return;
            }

            // 정지 상태에서는 재진입 버퍼를 넘겼을 때만 추적을 다시 시작한다.
            if (planarDistance >= chaseReengageRange)
            {
                _isChasingTarget = true;
            }
        }

        private bool TryStartAttackState()
        {
            IBossAttackPattern selectedPattern = SelectPatternByPhase();
            if (selectedPattern == null)
            {
                return false;
            }

            Controller.AttackState.SetPattern(selectedPattern);
            Controller.StateMachine.ChangeState(Controller.AttackState);
            return true;
        }

        public override void Exit() { }

        private IBossAttackPattern SelectPatternByPhase()
        {
            if (Controller.IsPhaseOneAttackWindow)
            {
                return PickPhaseOnePattern();
            }

            if (Controller.IsPhaseTwoAttackWindow)
            {
                return PickPhaseTwoPattern();
            }

            return null;
        }

        private IBossAttackPattern PickPhaseOnePattern()
        {
            return PickFromTwo(
                Controller.EnableBasicAttack ? Controller.BasicAttackPattern : null,
                Controller.EnableLungeAttack ? Controller.LungeAttackPattern : null,
                ref _lastPhaseOnePattern);
        }

        private IBossAttackPattern PickPhaseTwoPattern()
        {
            return PickFromTwo(
                Controller.EnableProjectileAttack ? Controller.ProjectileAttackPattern : null,
                Controller.EnableAoEAttack ? Controller.AoEAttackPattern : null,
                ref _lastPhaseTwoPattern);
        }

        private static IBossAttackPattern PickFromTwo(
            IBossAttackPattern first,
            IBossAttackPattern second,
            ref IBossAttackPattern lastPicked)
        {
            bool hasFirst = first != null;
            bool hasSecond = second != null;
            if (!hasFirst && !hasSecond) return null;
            if (!hasFirst)
            {
                lastPicked = second;
                return second;
            }

            if (!hasSecond)
            {
                lastPicked = first;
                return first;
            }

            // 두 패턴이 모두 가능하면 직전 패턴을 피해서 번갈아 사용한다.
            IBossAttackPattern picked = lastPicked == first ? second : first;
            lastPicked = picked;
            return picked;
        }
    }

    public class BossAttackState : BossBaseState
    {
        private IBossAttackPattern _currentPattern;

        public BossAttackState(BossController controller) : base(controller) { }

        /// <summary>
        /// 실행할 공격 패턴을 설정한다. CombatState에서 전환 전에 호출.
        /// </summary>
        public void SetPattern(IBossAttackPattern pattern)
        {
            _currentPattern = pattern;
        }

        public override void Enter()
        {
            // 패턴 미할당 시 안전하게 복귀
            if (_currentPattern == null)
            {
                Debug.LogWarning("BossAttackState: No pattern assigned!");
                Controller.StateMachine.ChangeState(Controller.CombatState);
                return;
            }

            _currentPattern.Enter(Controller);
        }

        public override void Update()
        {
            if (_currentPattern == null) return;

            // true 반환 = 공격 종료
            if (_currentPattern.Update(Controller))
            {
                Controller.StateMachine.ChangeState(Controller.CombatState);
            }
        }

        public override void Exit()
        {
            _currentPattern?.Exit(Controller);

            // 공격 쿨다운 시작
            Controller.StartAttackCooldown();
        }
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
                float planarDistance = Controller.GetPlanarDistanceToTarget();
                if (planarDistance <= Controller.DetectionRange && Controller.CheckLineOfSight())
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
            if (BossController.GetPlanarDistance(Controller.transform.position, _lastKnownPos) > 0.5f)
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
                    Controller.GetPlanarDistanceToTarget() <= Controller.DetectionRange)
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
