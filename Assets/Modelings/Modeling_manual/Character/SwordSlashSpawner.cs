using UnityEngine;

public class SwordSlashSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject slashPrefab; // 메시 또는 파티클 slash 프리팹
    public Transform swordTip;
    public float velocityThreshold = 18f;
    public float cooldown = 0.2f; // 한 번 휘두를 때 slash가 과도하게 생성되지 않게 막는다.
    public PlayerController playerController;

    private Vector3 _lastPosition;
    private float _nextSpawnTime;

    void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }
    }

    void Start()
    {
        _lastPosition = swordTip.position;
    }

    void Update()
    {
        if (!IsAttackStateActive())
        {
            _lastPosition = swordTip.position;
            return;
        }

        float currentVelocity = (swordTip.position - _lastPosition).magnitude / Time.deltaTime;

        if (currentVelocity > velocityThreshold && Time.time > _nextSpawnTime)
        {
            SpawnSlash();
            _nextSpawnTime = Time.time + cooldown;
        }

        _lastPosition = swordTip.position;
    }

    void SpawnSlash()
    {
        // 월드 공간에 생성해 swordTip 부모 이동을 따라가지 않게 한다.
        GameObject slash = Instantiate(slashPrefab, swordTip.position, swordTip.rotation);

        // 애니메이션/VFX가 끝난 뒤 남지 않도록 제거한다.
        Destroy(slash, 1.5f);
    }

    bool IsAttackStateActive()
    {
        if (playerController == null || playerController.StateMachine == null)
        {
            return false;
        }

        return playerController.StateMachine.CurrentState == playerController.AttackState;
    }
}
