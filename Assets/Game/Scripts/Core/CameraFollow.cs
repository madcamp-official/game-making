using UnityEngine;

/// <summary>
/// 카메라가 대상을 부드럽게 따라간다. 플레이어 피격 시 짧게 흔들린다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;

    [Header("피격 화면 흔들림")]
    [SerializeField, Min(0f)] private float hurtShakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float hurtShakeMagnitude = 0.04f;

    private Vector3 velocity;
    private Vector3 followPosition;   // 흔들림이 섞이지 않은 순수 추적 위치
    private bool followInitialized;
    private float shakeUntil = -1f;
    private float shakeDuration;
    private float shakeMagnitude;
    private Health observedHealth;

    public void SetTarget(Transform newTarget) => target = newTarget;

    /// <summary>지정 시간 동안 카메라를 흔든다. 시간이 지날수록 잦아든다.</summary>
    public void Shake(float duration, float magnitude)
    {
        shakeUntil = Time.time + duration;
        shakeDuration = Mathf.Max(0.0001f, duration);
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

        if (!followInitialized)
        {
            followPosition = transform.position;
            followInitialized = true;
        }

        // 흔들림은 추적 위치에 "표시할 때만" 더한다.
        // 예전처럼 transform.position에 누적하면 흔들린 좌표가 다음 프레임 SmoothDamp의
        // 시작점이 되어 흔들림이 감속 운동에 먹혀 들어가고, 실제 진폭보다 훨씬 크게 요동쳤다.
        Vector3 goal = new Vector3(target.position.x, target.position.y, followPosition.z);
        followPosition = Vector3.SmoothDamp(followPosition, goal, ref velocity, smoothTime);

        Vector3 shakeOffset = Vector3.zero;
        if (Time.time < shakeUntil)
        {
            float falloff = (shakeUntil - Time.time) / shakeDuration; // 1 → 0
            shakeOffset = (Vector3)(Random.insideUnitCircle * (shakeMagnitude * falloff));
        }

        transform.position = followPosition + shakeOffset;
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
