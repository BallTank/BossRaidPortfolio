using Core.Combat;
using UnityEngine;

public class SimultaneousDeathTest : MonoBehaviour {
    [SerializeField] private Health _playerHealth;
    [SerializeField] private Health _bossHealth;
    [SerializeField] private KeyCode _triggerKey = KeyCode.K;
    [SerializeField] private int _damage = 9999;

    private void Update() {
        if (!Input.GetKeyDown(_triggerKey)) return;

        // 같은 프레임에 둘 다 사망시키는 테스트
        _playerHealth.TakeDamage(_damage);
        _bossHealth.TakeDamage(_damage);
    }
}
