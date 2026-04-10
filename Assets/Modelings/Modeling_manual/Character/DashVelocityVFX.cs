using UnityEngine;

public class DashVelocityVFX : MonoBehaviour
{
    [Header("References")]
    public CharacterController playerController; // Or Rigidbody, depending on your setup
    public GameObject dashDustPrefab;
    public Transform footTransform;

    [Header("Velocity Settings")]
    [Tooltip("The speed that counts as a dash")]
    public float dashVelocityThreshold = 8f;
    [Tooltip("How long before the dust gets destroyed")]
    public float dustLifetime = 2f;

    private bool _wasDashing = false;

    void Update()
    {
        // 1. Get horizontal velocity (ignoring Y so falling doesn't trigger a dash)
        Vector3 horizontalVelocity = new Vector3(playerController.velocity.x, 0, playerController.velocity.z);
        float currentSpeed = horizontalVelocity.magnitude;

        // 2. Check if we are currently dashing based on speed
        bool isDashing = currentSpeed >= dashVelocityThreshold;

        // 3. Trigger VFX only on the exact frame the dash begins
        if (isDashing && !_wasDashing)
        {
            SpawnDustVFX(horizontalVelocity);
        }

        _wasDashing = isDashing;
    }

    private void SpawnDustVFX(Vector3 currentVelocity)
    {
        if (dashDustPrefab != null && footTransform != null)
        {
            // Spawn the dust at the feet's exact position
            GameObject dustInstance = Instantiate(dashDustPrefab, footTransform.position, Quaternion.identity);

            // Optional: Rotate the dust so it points opposite to the dash direction
            if (currentVelocity != Vector3.zero)
            {
                dustInstance.transform.forward = -currentVelocity.normalized;
            }

            // Clean up the memory after the particle effect finishes
            Destroy(dustInstance, dustLifetime);
        }
    }
}