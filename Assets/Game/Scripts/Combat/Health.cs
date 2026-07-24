using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어와 적이 공통으로 사용하는 체력 컴포넌트.
/// 피해, 회복, 사망 이벤트를 관리하고 피격 시 스프라이트를 점멸시킨다.
/// </summary>
public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private float invincibleDuration = 0.3f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;
    public bool IsInvincible { get; private set; }

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDamaged;
    public event Action OnDied;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void SetMaxHealth(int value, bool refill = true)
    {
        maxHealth = Mathf.Max(1, value);
        CurrentHealth = refill ? maxHealth : Mathf.Min(CurrentHealth, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || IsInvincible || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnDamaged?.Invoke();

        if (CurrentHealth <= 0)
        {
            OnDied?.Invoke();
        }
        else
        {
            StartCoroutine(InvincibleFlash());
        }
    }

    /// <summary>사망 상태에서 지정 체력으로 되살린다.</summary>
    public void Revive(int amount)
    {
        CurrentHealth = Mathf.Clamp(amount, 1, maxHealth);
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private IEnumerator InvincibleFlash()
    {
        IsInvincible = true;
        float elapsed = 0f;
        while (elapsed < invincibleDuration)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.06f);
            elapsed += 0.06f;
        }
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        IsInvincible = false;
    }
}
