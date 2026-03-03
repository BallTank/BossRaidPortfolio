using Core.Common;
using UnityEngine;

namespace Core.Boss
{
    public class BossVisual : BaseVisual
    {
        [Header("Visual Elements")]
        [SerializeField] private GameObject _questionMarkUI;

        /// <summary>
        /// Animator 접근 프로퍼티 (LungeAttackPattern의 normalizedTime 체크 등에 사용)
        /// </summary>
        public Animator Animator => _animator;

        // Animation Name Constants (Add only new ones)
        private const string ANIM_LOCOMOTION = "Locomotion";
        private const string ANIM_BASIC_ATTACK = "Basic Attack";
        private const string ANIM_LUNGE_ATTACK = "Lunge Attack";
        private const string ANIM_FLAME_ATTACK = "Flame Attack";
        private const string ANIM_FIREBALL_SHOOT = "Fireball Shoot";
        private const string ANIM_LEGACY_CLAW_ATTACK = "Claw Attack";
        private const string ANIM_TAKE_OFF = "takeOff";
        private const string ANIM_TAKE_OFF_ALT = "TakeOff";
        private const string ANIM_FLY_FORWARD = "FlyForward";
        private const string ANIM_FLY_FORWARD_ALT = "Fly Forward";
        private const string ANIM_FLY_IDLE = "FlyIdle";
        private const string ANIM_FLY_IDLE_ALT = "Fly Idle";
        private const string ANIM_LAND = "Land";
        private const string ANIM_SCREAM = "Scream";

        // Animation IDs
        private static readonly int AnimLocomotion = Animator.StringToHash(ANIM_LOCOMOTION);
        private static readonly int AnimBasicAttack = Animator.StringToHash(ANIM_BASIC_ATTACK);
        private static readonly int AnimLungeAttack = Animator.StringToHash(ANIM_LUNGE_ATTACK);
        private static readonly int AnimFlameAttack = Animator.StringToHash(ANIM_FLAME_ATTACK);
        private static readonly int AnimFireballShoot = Animator.StringToHash(ANIM_FIREBALL_SHOOT);
        private static readonly int AnimLegacyClawAttack = Animator.StringToHash(ANIM_LEGACY_CLAW_ATTACK);
        private static readonly int AnimTakeOff = Animator.StringToHash(ANIM_TAKE_OFF);
        private static readonly int AnimTakeOffAlt = Animator.StringToHash(ANIM_TAKE_OFF_ALT);
        private static readonly int AnimFlyForward = Animator.StringToHash(ANIM_FLY_FORWARD);
        private static readonly int AnimFlyForwardAlt = Animator.StringToHash(ANIM_FLY_FORWARD_ALT);
        private static readonly int AnimFlyIdle = Animator.StringToHash(ANIM_FLY_IDLE);
        private static readonly int AnimFlyIdleAlt = Animator.StringToHash(ANIM_FLY_IDLE_ALT);
        private static readonly int AnimLand = Animator.StringToHash(ANIM_LAND);
        private static readonly int AnimScream = Animator.StringToHash(ANIM_SCREAM);
        private const float DefaultScreamDuration = 1.2f;

        private int _currentAnimState;
        private BossRootMotionRelay _rootMotionRelay;

        private void Awake()
        {
            ResolveRootMotionRelay();
        }

        public void SetSpeed(float speed)
        {
            // Blend Tree Parameter (Inherited AnimSpeed)
            if (_animator) _animator.SetFloat(AnimSpeed, speed);
        }

        public void SetLungeRootMotionEnabled(bool enabled)
        {
            if (_animator == null) return;
            if (_rootMotionRelay == null) ResolveRootMotionRelay();
            _rootMotionRelay?.SetLungeRootMotionEnabled(enabled);
        }

        public void PlayIdle()
        {
            CrossFade(AnimLocomotion);
            SetSpeed(0f);
        }

        public void PlayMove()
        {
            CrossFade(AnimLocomotion);
            // Speed is set by Controller via SetSpeed()
        }

        public void PlayAttack() => CrossFade(AnimBasicAttack);
        public void PlayLungeAttack()
        {
            if (_animator && _animator.HasState(0, AnimLungeAttack))
            {
                CrossFade(AnimLungeAttack);
                return;
            }

            // 아직 Animator 상태명이 변경되지 않은 경우 레거시 이름으로 폴백
            CrossFade(AnimLegacyClawAttack);
        }

        public void PlayProjectileAttack()
        {
            if (_animator == null) return;

            if (_animator.HasState(0, AnimFlameAttack))
            {
                CrossFade(AnimFlameAttack);
                return;
            }

            if (_animator.HasState(0, AnimFireballShoot))
            {
                CrossFade(AnimFireballShoot);
                return;
            }

            // 투사체 전용 상태가 아직 없으면 기본 공격 모션으로 폴백
            CrossFade(AnimBasicAttack);
        }

        public void PlayTakeOff()
        {
            if (TryCrossFade(AnimTakeOff)) return;
            if (TryCrossFade(AnimTakeOffAlt)) return;
            PlayIdle();
        }

        public void PlayFlyForward()
        {
            if (TryCrossFade(AnimFlyForward)) return;
            if (TryCrossFade(AnimFlyForwardAlt)) return;
            // FlyForward 상태가 없을 때도 Walk로 떨어지지 않도록 비행 계열로 폴백한다.
            PlayFlyIdle();
        }

        public void PlayFlyIdle()
        {
            if (TryCrossFade(AnimFlyIdle)) return;
            if (TryCrossFade(AnimFlyIdleAlt)) return;
            PlayIdle();
        }

        public void PlayLand()
        {
            if (TryCrossFade(AnimLand)) return;
            PlayIdle();
        }

        public float PlayScream()
        {
            if (!TryCrossFade(AnimScream))
            {
                PlayIdle();
                return DefaultScreamDuration;
            }

            return GetClipLengthOrDefault(ANIM_SCREAM, DefaultScreamDuration);
        }

        // Override Base Methods to use CrossFade with state tracking
        public override void TriggerHit()
        {
            CrossFade(AnimHit);
            base.TriggerHit(); // Flashing effect
        }

        public override void TriggerDie()
        {
            CrossFade(AnimDie);
            // No base.TriggerDie() call needed if we handle CrossFade here
        }

        private void CrossFade(int stateHash, float duration = 0.1f)
        {
            if (_animator && _currentAnimState != stateHash)
            {
                _currentAnimState = stateHash;
                _animator.CrossFade(stateHash, duration);
            }
        }

        private bool TryCrossFade(int stateHash, float duration = 0.1f)
        {
            if (_animator == null) return false;
            if (!_animator.HasState(0, stateHash)) return false;

            CrossFade(stateHash, duration);
            return true;
        }

        private float GetClipLengthOrDefault(string clipName, float fallback)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return fallback;

            AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null) continue;
                if (!string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase)) continue;
                return clip.length > 0f ? clip.length : fallback;
            }

            return fallback;
        }

        private void ResolveRootMotionRelay()
        {
            if (_animator == null) return;

            GameObject animatorObject = _animator.gameObject;
            _rootMotionRelay = animatorObject.GetComponent<BossRootMotionRelay>();
            if (_rootMotionRelay == null)
            {
                _rootMotionRelay = animatorObject.AddComponent<BossRootMotionRelay>();
            }

            _rootMotionRelay.Configure(GetComponentInParent<BossController>());
            _rootMotionRelay.SetLungeRootMotionEnabled(false);
        }

        public void SetSearchingUI(bool active)
        {
            if (_questionMarkUI) _questionMarkUI.SetActive(active);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class BossRootMotionRelay : MonoBehaviour
    {
        private Animator _animator;
        private BossController _owner;
        private bool _lungeRootMotionEnabled;
        private Vector3 _cachedLocalPosition;
        private Quaternion _cachedLocalRotation;
        private bool _hasCachedLocalPose;
        private Vector3 _previousVisualWorldPosition;
        private const float RootMotionDeltaEpsilon = 0.0001f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_owner == null)
            {
                _owner = GetComponentInParent<BossController>();
            }

            _previousVisualWorldPosition = transform.position;
        }

        public void Configure(BossController owner)
        {
            _owner = owner;
        }

        public void SetLungeRootMotionEnabled(bool enabled)
        {
            if (_lungeRootMotionEnabled == enabled)
            {
                if (_animator != null)
                {
                    _animator.applyRootMotion = enabled;
                }
                return;
            }

            _lungeRootMotionEnabled = enabled;

            if (enabled)
            {
                CacheVisualLocalPose();
                _owner?.SetLungeRootMotionActive(true);
            }
            else
            {
                _owner?.SetLungeRootMotionActive(false);
                RestoreVisualLocalPose();
            }

            _previousVisualWorldPosition = transform.position;

            if (_animator != null)
            {
                _animator.applyRootMotion = enabled;
            }
        }

        private void OnAnimatorMove()
        {
            if (!_lungeRootMotionEnabled) return;
            if (_animator == null || _owner == null) return;

            Vector3 animatorDeltaPosition = _animator.deltaPosition;
            Vector3 appliedDeltaPosition = ResolveAppliedDeltaPosition(animatorDeltaPosition, out bool usedVisualFallback);
            _owner.ApplyLungeRootMotion(appliedDeltaPosition);

            // 자식 Visual의 로컬 기준점을 유지해 부모/자식 좌표 불일치를 방지한다.
            RestoreVisualLocalPose();
            _previousVisualWorldPosition = transform.position;

            if (!_owner.ShouldEmitLungeRootMotionDebugLog()) return;

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            bool isLungeState = stateInfo.IsName("Lunge Attack");
            bool isClawState = stateInfo.IsName("Claw Attack");
            Vector3 bossPos = _owner.transform.position;
            float animatorDeltaXZMag = new Vector2(animatorDeltaPosition.x, animatorDeltaPosition.z).magnitude;
            float appliedDeltaXZMag = new Vector2(appliedDeltaPosition.x, appliedDeltaPosition.z).magnitude;
            Vector3 localOffset = transform.localPosition - _cachedLocalPosition;
            string spatialProbeText = _owner.BuildLungeSpatialProbeText("RootMotion", stateInfo.normalizedTime);

            Debug.Log(
                $"[LungeDebug][RootMotion] " +
                $"applyRootMotion={_animator.applyRootMotion} " +
                $"state(Lunge={isLungeState},Claw={isClawState}) " +
                $"nTime={stateInfo.normalizedTime:F3} " +
                $"animDelta=({animatorDeltaPosition.x:F3},{animatorDeltaPosition.y:F3},{animatorDeltaPosition.z:F3}) " +
                $"appliedDelta=({appliedDeltaPosition.x:F3},{appliedDeltaPosition.y:F3},{appliedDeltaPosition.z:F3}) " +
                $"fallback={usedVisualFallback} " +
                $"animDeltaXZMag={animatorDeltaXZMag:F3} " +
                $"appliedDeltaXZMag={appliedDeltaXZMag:F3} " +
                $"visualLocalOffset=({localOffset.x:F3},{localOffset.y:F3},{localOffset.z:F3}) " +
                $"bossPos=({bossPos.x:F3},{bossPos.y:F3},{bossPos.z:F3}) " +
                $"{spatialProbeText}");
        }

        private Vector3 ResolveAppliedDeltaPosition(Vector3 animatorDeltaPosition, out bool usedVisualFallback)
        {
            usedVisualFallback = false;

            Vector3 animatorDeltaXZ = animatorDeltaPosition;
            animatorDeltaXZ.y = 0f;

            float epsilonSqr = RootMotionDeltaEpsilon * RootMotionDeltaEpsilon;
            if (animatorDeltaXZ.sqrMagnitude > epsilonSqr)
            {
                return animatorDeltaXZ;
            }

            Vector3 visualDelta = transform.position - _previousVisualWorldPosition;
            visualDelta.y = 0f;
            if (visualDelta.sqrMagnitude <= epsilonSqr)
            {
                return Vector3.zero;
            }

            usedVisualFallback = true;
            return visualDelta;
        }

        private void CacheVisualLocalPose()
        {
            _cachedLocalPosition = transform.localPosition;
            _cachedLocalRotation = transform.localRotation;
            _hasCachedLocalPose = true;
        }

        private void RestoreVisualLocalPose()
        {
            if (!_hasCachedLocalPose) return;

            transform.localPosition = _cachedLocalPosition;
            transform.localRotation = _cachedLocalRotation;
        }
    }
}
