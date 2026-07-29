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
    private bool hitInvincible;
    private int invulnerabilityLocks;

    /// <summary>실제 최대 체력. 기본값에 유물 배율(생명의구슬 등)을 곱한 값이다.</summary>
    public int MaxHealth => Mathf.Max(1, GameMath.RoundHalfUp(maxHealth * maxHealthMultiplier));
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;
    /// <summary>피격 직후 무적 또는 연출·상태가 건 무적 잠금 중 하나라도 활성화되어 있으면 true.</summary>
    public bool IsInvincible => hitInvincible || invulnerabilityLocks > 0;

    /// <summary>회복량 배율 (큰뿌리). 플레이어 쪽에서만 설정한다.</summary>
    public float HealMultiplier { get; set; } = 1f;

    /// <summary>
    /// 받는 피해 배율. 고지가 웅크리는 동안 낮춰서 단단해진다.
    /// 배율이 아무리 낮아도 최소 1은 깎인다 — 완전 무적은 <see cref="BeginInvulnerability"/>의 몫이다.
    /// </summary>
    public float DamageTakenMultiplier { get; set; } = 1f;

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

    /// <summary>
    /// 전투로 얻어맞았을 때만 불린다 (울퉁불퉁멧의 반사 피해).
    /// <see cref="OnDamaged"/>와 달리 이벤트에서 치르는 대가(<see cref="TakeToll"/>)는 세지 않는다 —
    /// 잠만보를 흔들어 깎인 체력으로 반사 피해가 나가면 앞뒤가 맞지 않는다.
    /// </summary>
    public event Action OnCombatDamaged;

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
        if (IsInvincible) return;
        Deduct(amount, grantInvincibility: true);
    }

    /// <summary>
    /// 무적 시간을 쓰지도, 새로 만들지도 않고 체력을 깎는다. 이벤트에서 치르는 대가처럼
    /// "맞은 것"이 아닌 감소에 쓴다.
    ///
    /// 평범한 <see cref="TakeDamage"/>를 쓰면 안 되는 이유: 무적 시간은 스케일 시간으로 흐르는데
    /// 이벤트 대사창은 <see cref="Time.timeScale"/>을 0으로 세운다. 그래서 창이 떠 있는 동안에는
    /// 무적이 절대 풀리지 않아, 잠만보를 몇 번을 흔들어도 체력이 처음 한 번만 깎였다.
    /// </summary>
    public void TakeToll(int amount) => Deduct(amount, grantInvincibility: false);

    private void Deduct(int amount, bool grantInvincibility)
    {
        if (IsDead || amount <= 0) return;

        if (!Mathf.Approximately(DamageTakenMultiplier, 1f))
            amount = Mathf.Max(1, GameMath.RoundHalfUp(amount * DamageTakenMultiplier));

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        OnDamaged?.Invoke();

        if (CurrentHealth <= 0)
        {
            // 진행 중이던 점멸을 멈추고 렌더러가 꺼진 채 남지 않게 복원한다.
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            hitInvincible = false;
            invulnerabilityLocks = 0;
            OnDied?.Invoke();
        }
        else if (grantInvincibility)
        {
            OnCombatDamaged?.Invoke();
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(InvincibleFlash());
        }
    }

    /// <summary>사망 상태에서 지정 체력으로 되살린다.</summary>
    public void Revive(int amount)
    {
        CurrentHealth = Mathf.Clamp(amount, 1, MaxHealth);
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = null;
        hitInvincible = false;
        invulnerabilityLocks = 0;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>
    /// 연출이나 상태가 지속되는 동안 피해를 막는다. 여러 시스템이 동시에 호출해도 마지막 잠금이
    /// 해제될 때까지 무적이 유지되므로, 호출한 쪽은 반드시 <see cref="EndInvulnerability"/>와 짝을 맞춘다.
    /// </summary>
    public void BeginInvulnerability()
    {
        if (IsDead) return;
        invulnerabilityLocks++;
    }

    /// <summary><see cref="BeginInvulnerability"/>로 건 무적 잠금을 하나 해제한다.</summary>
    public void EndInvulnerability()
    {
        invulnerabilityLocks = Mathf.Max(0, invulnerabilityLocks - 1);
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
        hitInvincible = true;
        float elapsed = 0f;
        while (elapsed < invincibleDuration)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return flashInterval;
            elapsed += 0.06f;
        }
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        hitInvincible = false;
        flashRoutine = null;
    }
}
