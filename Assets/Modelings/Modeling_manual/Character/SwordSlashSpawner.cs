using UnityEngine;

public class SwordSlashSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject slashPrefab; // A prefab with a mesh or particle slash
    public Transform swordTip;
    public float velocityThreshold = 18f;
    public float cooldown = 0.2f; // Prevents spawning 60 slashes in one swing

    private Vector3 _lastPosition;
    private float _nextSpawnTime;

    void Start()
    {
        _lastPosition = swordTip.position;
    }

    void Update()
    {
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
        // Instantiate in World Space (no parent) so it stays put
        GameObject slash = Instantiate(slashPrefab, swordTip.position, swordTip.rotation);

        // Ensure the prefab destroys itself after the animation/vfx ends
        Destroy(slash, 1.5f);
    }
}