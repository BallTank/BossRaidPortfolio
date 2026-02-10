using System;
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
        [Header("Status")]
        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private int _currentHealth;
        [SerializeField] private bool _isInvincible = false;

        public event Action<int> OnDamageTaken;
        public event Action OnDeath;

        public bool IsDead => _currentHealth <= 0;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        public void TakeDamage(int damage)
        {
            if (IsDead || _isInvincible) return;

            _currentHealth -= damage;
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
            if (IsDead) return;

            _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
            Debug.Log($"💚 {gameObject.name} healed {amount}. HP: {_currentHealth}/{_maxHealth}");
        }

        private void Die()
        {
            _currentHealth = 0;
            Debug.Log($"💀 {gameObject.name} has died.");
            OnDeath?.Invoke();
        }
    }
}
