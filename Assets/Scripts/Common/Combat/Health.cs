using System;
using Core.Common;
using Core.Interfaces;
using UnityEngine;

namespace Core.Combat
{
    /// <summary>
    /// 생명력을 가진 모든 개체(플레이어, 몬스터)의 기본 컴포넌트.
    /// IDamageable을 구현하여 데미지 처리를 담당합니다.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        private enum RuntimeWriteAuthorityMode
        {
            LocalAuthority,
            ReadOnlyReplica
        }

        [Header("Status")]
        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private int _currentHealth;
        [SerializeField] private bool _isInvincible = false;
        [SerializeField, HideInInspector] private RuntimeWriteAuthorityMode _runtimeWriteAuthorityMode = RuntimeWriteAuthorityMode.LocalAuthority;

        public event Action<int> OnDamageTaken;
        public event Action OnDeath;

        public bool IsDead => _currentHealth <= 0;
        public int MaxHealth => _maxHealth;
        public int CurrentHealth => _currentHealth;
        public float HealthRatio => _maxHealth > 0 ? (float)_currentHealth / _maxHealth : 0f;
        public bool HasRuntimeWriteAuthority => _runtimeWriteAuthorityMode == RuntimeWriteAuthorityMode.LocalAuthority;

        private void Awake()
        {
            _currentHealth = _maxHealth;
            HitTraceLogger.Log($"[HitTrace][BOOT][Health][Awake] object={gameObject.name} hp={_currentHealth}/{_maxHealth}");
        }

        public void TakeDamage(int damage)
        {
            if (!HasRuntimeWriteAuthority)
            {
                HitTraceLogger.Log($"[HitTrace][S11][FAIL] target={gameObject.name} reason=NoWriteAuthority damage={damage}");
                return;
            }

            if (IsDead)
            {
                HitTraceLogger.Log($"[HitTrace][S11][FAIL] target={gameObject.name} reason=AlreadyDead damage={damage}");
                return;
            }

            if (_isInvincible)
            {
                HitTraceLogger.Log($"[HitTrace][S11][FAIL] target={gameObject.name} reason=Invincible damage={damage}");
                return;
            }

            if (damage <= 0)
            {
                HitTraceLogger.Log($"[HitTrace][S11][FAIL] target={gameObject.name} reason=NonPositiveDamage damage={damage}");
                return;
            }

            _currentHealth -= damage;
            HitTraceLogger.Log($"[HitTrace][S11][PASS] target={gameObject.name} damage={damage} hp={_currentHealth}/{_maxHealth}");
            Debug.Log($"{gameObject.name} took {damage} damage. HP: {_currentHealth}/{_maxHealth}");

            OnDamageTaken?.Invoke(damage);

            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        public void SetInvincible(bool state)
        {
            _isInvincible = state;
        }

        public void Heal(int amount)
        {
            if (!HasRuntimeWriteAuthority) return;
            if (IsDead) return;

            _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
            Debug.Log($"💚 {gameObject.name} healed {amount}. HP: {_currentHealth}/{_maxHealth}");
        }

        public void SetRuntimeWriteAuthority(bool canWrite)
        {
            _runtimeWriteAuthorityMode = canWrite
                ? RuntimeWriteAuthorityMode.LocalAuthority
                : RuntimeWriteAuthorityMode.ReadOnlyReplica;
        }

        public void ResetRuntimeWriteAuthority()
        {
            _runtimeWriteAuthorityMode = RuntimeWriteAuthorityMode.LocalAuthority;
        }

        private void Die()
        {
            _currentHealth = 0;
            Debug.Log($"💀 {gameObject.name} has died.");
            OnDeath?.Invoke();
        }
    }
}
