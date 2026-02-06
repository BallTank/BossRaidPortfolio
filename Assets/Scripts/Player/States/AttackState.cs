using BossRaid.Patterns;
using UnityEngine;

public class AttackState : BaseState
{
    private int _comboIndex;
    private float _timer;
    private bool _reserveNextCombo;
    private bool _wasAttackPressed;
    private float _currentVerticalVelocity;

    // 캐싱된 공격 데이터 (매 프레임 배열 접근 방지)
    private AttackComboData _currentAttackData;

    public AttackState(PlayerController controller) : base(controller)
    {
    }

    public void SetComboIndex(int index)
    {
        _comboIndex = index;
    }

    // FSM 진입 (최초 1회만 호출됨)
    public override void Enter()
    {
        // 콤보 시작 시점
        _comboIndex = 0;
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
        _timer = 0f;
        _reserveNextCombo = false;
        _wasAttackPressed = true; // 진입 시점 버튼 눌림 가정 (Edge Trigger 준비)

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
        if (_timer >= _currentAttackData.cancelStartTime)
        {
            if (input.HasFlag(InputFlag.Dash) && Controller.CanDash)
            {
                Controller.StateMachine.ChangeState(Controller.DashState);
                Debug.Log("⚡ Attack Canceled by Dash!");
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
        _comboIndex = 0;
        _reserveNextCombo = false;
    }

    private void RotateToCamera()
    {
        Transform cameraRoot = Controller.CameraRoot;
        Vector3 lookDir = cameraRoot.forward;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            Controller.transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }
}
