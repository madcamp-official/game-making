using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어와 적이 공통으로 사용하는 체력 컴포넌트.
/// 피해, 회복, 사망 이벤트를 관리하고 피격 시 스프라이트를 점멸시킨다.
/// </summary>
public class Health : MonoBehaviour
{
    [Tooltip("배율을 적용하기 전의 기본 최대 체력. 실제 최대치는 MaxHealth를 쓴다.")]
    [SerializeField, Min(1)] private int maxHealth = 10;
    [SerializeField, Min(0f)] private float invincibleDuration = 0.3f;

    private float maxHealthMultiplier = 1f;

    /// <summary>실제 최대 체력. 기본값에 유물 배율(생명의구슬 등)을 곱한 값이다.</summary>
    public int MaxHealth => Mathf.Max(1, GameMath.RoundHalfUp(maxHealth * maxHealthMultiplier));
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;
    public bool IsInvincible { get; private set; }

    /// <summary>회복량 배율 (큰뿌리). 플레이어 쪽에서만 설정한다.</summary>
    public float HealMultiplier { get; set; } = 1f;

    /// <summary>최대 체력 배율 (생명의구슬). 줄어들면 현재 체력도 같이 깎인다.</summary>
    public float MaxHealthMultiplier
    {
        get => maxHealthMultiplier;
        set
        {
            float clamped = Mathf.Max(0.01f, value);
            if (Mathf.Approximately(clamped, maxHealthMultiplier)) return;
            maxHealthMultiplier = clamped;
            CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
    }

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDamaged;
    public event Action OnDied;

    // 코루틴 대기 객체 재사용 (루프마다 할당 방지)
    private static readonly WaitForSeconds flashInterval = new WaitForSeconds(0.06f);

    private SpriteRenderer spriteRenderer;
    private Coroutine flashRoutine;

    private void Awake()
    {
        CurrentHealth = MaxHealth;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>기본 최대 체력을 바꾼다 (진화 등). 유물 배율은 그대로 유지된다.</summary>
    public void SetMaxHealth(int value, bool refill = true)
    {
        maxHealth = Mathf.Max(1, value);
        CurrentHealth = refill ? MaxHealth : Mathf.Min(CurrentHealth, MaxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || IsInvincible || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        OnDamaged?.Invoke();

        if (CurrentHealth <= 0)
        {
            // 진행 중이던 점멸을 멈추고 렌더러가 꺼진 채 남지 않게 복원한다.
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            IsInvincible = false;
            OnDied?.Invoke();
        }
        else
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(InvincibleFlash());
        }
    }

    /// <summary>사망 상태에서 지정 체력으로 되살린다.</summary>
    public void Revive(int amount)
    {
        CurrentHealth = Mathf.Clamp(amount, 1, MaxHealth);
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>
    /// 비어 있는 체력 중 <paramref name="fraction"/>만큼만 채운다. 1이면 완전 회복.
    /// 최대 체력이 아니라 "모자란 만큼"을 기준으로 하므로, 많이 다칠수록 많이 회복한다.
    /// </summary>
    public void HealMissingFraction(float fraction)
    {
        if (IsDead || fraction <= 0f) return;
        int missing = MaxHealth - CurrentHealth;
        if (missing <= 0) return;
        Heal(GameMath.RoundHalfUp(missing * Mathf.Clamp01(fraction)));
    }

    /// <summary>회복. 큰뿌리 같은 회복량 배율은 여기서 한 번에 적용된다.</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        int healed = Mathf.Max(1, GameMath.RoundHalfUp(amount * HealMultiplier));
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healed);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    private IEnumerator InvincibleFlash()
    {
        IsInvincible = true;
        float elapsed = 0f;
        while (elapsed < invincibleDuration)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return flashInterval;
            elapsed += 0.06f;
        }
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        IsInvincible = false;
        flashRoutine = null;
    }
}
