using Core.Common.Patterns;
using Core.Audio;
using Core.Player;
using UnityEngine;

namespace Core.Player.States
{
    public class AttackState : PlayerBaseState
    {
        private int _comboIndex;
        private float _timer;
        private bool _reserveNextCombo;
        private bool _wasAttackPressed;
        private float _currentVerticalVelocity;
        private float _pendingAuthoritativeElapsedTime;
        private bool _hasExplicitEntryComboIndex;

        // 캐싱된 공격 데이터 (매 프레임 배열 접근 방지)
        private AttackComboData _currentAttackData;

        public int CurrentComboIndex => _comboIndex;
        public bool HasNextComboStep => Controller.AttackCombos != null && _comboIndex + 1 < Controller.AttackCombos.Length;
        public bool HasReservedNextCombo => _reserveNextCombo;
        public bool CanAcceptNextComboInput => Controller.AttackCombos != null
                                               && Controller.AttackCombos.Length > 0
                                               && _timer <= _currentAttackData.comboInputWindow;
        public bool CanCancelIntoDashNow => _timer >= _currentAttackData.cancelStartTime;

        public AttackState(PlayerController controller) : base(controller) { }

        public void SetComboIndex(int index)
        {
            _comboIndex = index;
            _hasExplicitEntryComboIndex = true;
        }

        public void SetAuthoritativeElapsedTime(float elapsedTime)
        {
            _pendingAuthoritativeElapsedTime = Mathf.Max(0f, elapsedTime);
        }

        // FSM 진입 (최초 1회만 호출됨)
        public override void Enter()
        {
            // 콤보 시작 시점
            if (!_hasExplicitEntryComboIndex)
            {
                _comboIndex = 0;
            }

            _hasExplicitEntryComboIndex = false;
            _currentVerticalVelocity = 0f;

            StartComboStep();
        }

        // 내부 콤보 단계 시작
        private void StartComboStep()
        {
            // 데이터 검증
            if (Controller.AttackCombos == null || Controller.AttackCombos.Length == 0)
            {
                Controller.StateMachine.ChangeState(Controller.MoveState);
                return;
            }

            // 인덱스 안전장치
            if (_comboIndex >= Controller.AttackCombos.Length)
                _comboIndex = 0;

            // 현재 데이터 캐싱
            _currentAttackData = Controller.AttackCombos[_comboIndex];

            // 데미지 정보 업데이트 (Hitbox 활성화 시 사용됨)
            Controller.CurrentAttackDamage = _currentAttackData.damage;

            // 상태 리셋
            _timer = Mathf.Min(_pendingAuthoritativeElapsedTime, _currentAttackData.duration);
            _pendingAuthoritativeElapsedTime = 0f;
            _reserveNextCombo = false;
            _wasAttackPressed = true; // 진입 시점 버튼 눌림 가정 (Edge Trigger 준비)
            if (Controller.CanEmitAttackHitbox)
            {
                SoundController.Instance?.Play(SoundId.PlayerKatanaCombo);
                SoundController.Instance?.Play(SoundId.Player1VoiceAttack);
            }

            // Animation: Play Attack Combo (Attack1, Attack2, Attack3)
            if (Controller.Animator != null)
            {
                string animName = PlayerController.ANIM_STATE_ATTACK1;
                if (_comboIndex == 1) animName = PlayerController.ANIM_STATE_ATTACK2;
                else if (_comboIndex == 2) animName = PlayerController.ANIM_STATE_ATTACK3;

                Controller.Animator.CrossFade(animName, 0.1f);
            }

            // 방향 보정
            RotateToCamera();
            Controller.SetPendingComboHudStep(_comboIndex + 1);
            Controller.NotifyAuthoritativeAttackStepStarted(_comboIndex);
        }

        public override void Update(PlayerInputPacket input)
        {
            _timer += Time.deltaTime;

            // 1. Input Check
            HandleInput(input);

            // 2. Logic (Cancel / Transition)
            if (CheckDashCancel(input)) return;

            if (CheckComboTransition()) return;

            // 3. Physics (Delegated to Controller)
            HandlePhysics();
        }

        private void HandleInput(PlayerInputPacket input)
        {
            if (!Controller.CanQueueAttackComboFromInput)
            {
                _wasAttackPressed = input.HasFlag(InputFlag.Attack);
                return;
            }

            // 선입력 구간 체크
            if (_timer <= _currentAttackData.comboInputWindow)
            {
                bool isAttackDown = input.HasFlag(InputFlag.Attack);
                if (isAttackDown && !_wasAttackPressed)
                {
                    _reserveNextCombo = true;
                }
                _wasAttackPressed = isAttackDown;
            }
        }

        private bool CheckDashCancel(PlayerInputPacket input)
        {
            if (!Controller.CanCancelAttackIntoDashFromInput)
            {
                return false;
            }

            if (_timer >= _currentAttackData.cancelStartTime)
            {
                if (input.HasFlag(InputFlag.Dash) && Controller.CanDash)
                {
                    Controller.StateMachine.ChangeState(Controller.DashState);
                    Debug.Log("? Attack Canceled by Dash!");
                    return true;
                }
            }
            return false;
        }

        private bool CheckComboTransition()
        {
            // 애니메이션(시간) 종료 체크
            if (_timer >= _currentAttackData.duration)
            {
                if (_reserveNextCombo && _comboIndex + 1 < Controller.AttackCombos.Length)
                {
                    // 다음 콤보로 진행
                    _comboIndex++;
                    StartComboStep(); // 내부 메서드 호출 (State Exit/Enter 발생 안함 -> 오버헤드 감소 및 안전)
                    return true; // 이번 프레임 처리 완료
                }
                else
                {
                    // 콤보 종료 -> 이동 상태로 복귀
                    Controller.StateMachine.ChangeState(Controller.MoveState);
                    return true;
                }
            }
            return false;
        }

        public bool TryQueueAuthoritativeNextCombo(float? authoritativeFacingYaw, out int nextComboIndex)
        {
            nextComboIndex = _comboIndex + 1;

            if (_reserveNextCombo || !HasNextComboStep || !CanAcceptNextComboInput)
            {
                return false;
            }

            if (authoritativeFacingYaw.HasValue)
            {
                Controller.SetPendingAuthoritativeAttackFacingYaw(authoritativeFacingYaw.Value);
            }

            _reserveNextCombo = true;
            return true;
        }

        public bool TryApplyAuthoritativeComboStep(int comboIndex, float elapsedTime = 0f, float? authoritativeFacingYaw = null)
        {
            if (Controller.AttackCombos == null
                || comboIndex < 0
                || comboIndex >= Controller.AttackCombos.Length)
            {
                return false;
            }

            if (authoritativeFacingYaw.HasValue)
            {
                Controller.SetPendingAuthoritativeAttackFacingYaw(authoritativeFacingYaw.Value);
            }
            else
            {
                Controller.ClearPendingAuthoritativeAttackFacingYaw();
            }

            _comboIndex = comboIndex;
            _pendingAuthoritativeElapsedTime = Mathf.Max(0f, elapsedTime);
            _hasExplicitEntryComboIndex = false;
            StartComboStep();
            return true;
        }

        private void HandlePhysics()
        {
            if (Controller.CharController.isGrounded)
            {
                _currentVerticalVelocity = -2f;
            }
            else
            {
                _currentVerticalVelocity += Controller.Gravity * Time.deltaTime;
            }

            // 리팩토링된 공통 메서드 사용
            Controller.ApplyGravity(_currentVerticalVelocity);
        }

        public override void Exit()
        {
            // 상태가 비정상 종료되어도 공격 판정이 남지 않도록 강제 종료한다.
            Controller.OnHitEnd();
            Controller.HideComboHud();
            _comboIndex = 0;
            _reserveNextCombo = false;
            _hasExplicitEntryComboIndex = false;
            Controller.ClearPendingAuthoritativeAttackFacingYaw();
            Controller.ClearPendingAuthoritativeDashFacingYaw();
        }

        private void RotateToCamera()
        {
            Vector3 lookDir = Controller.GetAttackFacingDirection();
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Controller.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }
}



