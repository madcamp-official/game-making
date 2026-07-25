using UnityEngine;

/// <summary>
/// 카메라가 대상을 부드럽게 따라간다. 플레이어 피격 시 짧게 흔들린다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    [Header("피격 화면 흔들림")]
    [SerializeField, Min(0f)] private float hurtShakeDuration = 0.15f;
    [SerializeField, Min(0f)] private float hurtShakeMagnitude = 0.12f;

    private Vector3 velocity;
    private float shakeUntil = -1f;
    private float shakeMagnitude;
    private Health observedHealth;

    public void SetTarget(Transform newTarget) => target = newTarget;

    /// <summary>지정 시간 동안 카메라를 흔든다.</summary>
    public void Shake(float duration, float magnitude)
    {
        shakeUntil = Time.time + duration;
        shakeMagnitude = magnitude;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return;
            target = pc.transform;
        }

        // 플레이어 피격에 흔들림 연결 (대상이 바뀌면 다시 구독)
        if (observedHealth == null)
        {
            observedHealth = target.GetComponent<Health>();
            if (observedHealth != null)
                observedHealth.OnDamaged += OnTargetDamaged;
        }

        Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
        Vector3 next = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        if (Time.time < shakeUntil)
            next += (Vector3)(Random.insideUnitCircle * shakeMagnitude);
        transform.position = next;
    }

    private void OnTargetDamaged()
    {
        Shake(hurtShakeDuration, hurtShakeMagnitude);
    }

    private void OnDestroy()
    {
        if (observedHealth != null)
            observedHealth.OnDamaged -= OnTargetDamaged;
    }
}
