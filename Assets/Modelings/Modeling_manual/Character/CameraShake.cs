using UnityEngine;

[DefaultExecutionOrder(100)]
public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    public float shakeDuration = 0.2f;

    [Tooltip("How many degrees the camera tilts. Try 1 to 3.")]
    public float shakeMagnitude = 2f;

    private float _currentShakeTimer = 0f;
    private Quaternion _shakeOffset = Quaternion.identity;

    public void TriggerShake()
    {
        // This will print to your Unity Console so we know the signal arrived!
        Debug.Log("💥 TriggerShake was successfully called!");
        _currentShakeTimer = shakeDuration;
    }

    private void Update()
    {
        // DIAGNOSTIC TEST: Press 'T' on your keyboard while playing
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("⌨️ Keyboard 'T' pressed!");
            TriggerShake();
        }
    }

    private void LateUpdate()
    {
        // 1. Remove last frame's shake so we don't permanently twist the camera
        transform.localRotation = transform.localRotation * Quaternion.Inverse(_shakeOffset);

        // 2. If we are shaking, calculate a new random tilt
        if (_currentShakeTimer > 0)
        {
            float pitch = Random.Range(-1f, 1f) * shakeMagnitude; // Up/Down
            float yaw = Random.Range(-1f, 1f) * shakeMagnitude;   // Left/Right
            float roll = Random.Range(-1f, 1f) * (shakeMagnitude * 0.5f); // Slight screen tilt

            _shakeOffset = Quaternion.Euler(pitch, yaw, roll);
            _currentShakeTimer -= Time.deltaTime;
        }
        else
        {
            _shakeOffset = Quaternion.identity; // Stop shaking
        }

        // 3. Apply the shake on top of whatever your Camera Controller is doing
        transform.localRotation = transform.localRotation * _shakeOffset;
    }
}