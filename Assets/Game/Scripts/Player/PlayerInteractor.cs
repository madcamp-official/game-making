using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 상호작용 대상 인터페이스. 이벤트 오브젝트와 상점 상품이 구현한다.
/// </summary>
public interface IInteractable
{
    bool CanInteract { get; }
    string Prompt { get; }
    void Interact(GameObject interactor);
}

/// <summary>
/// 플레이어 주변의 상호작용 대상을 찾아 힌트를 띄우고 E 키로 실행한다.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float radius = 1.4f;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void Update()
    {
        if (health != null && health.IsDead)
        {
            if (UIManager.Instance != null) UIManager.Instance.SetHint("");
            return;
        }

        IInteractable best = null;
        float bestDistance = float.MaxValue;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, radius))
        {
            IInteractable interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract) continue;
            float distance = Vector2.Distance(transform.position, hit.transform.position);
            if (distance < bestDistance) { bestDistance = distance; best = interactable; }
        }

        if (UIManager.Instance != null)
            UIManager.Instance.SetHint(best != null ? best.Prompt : "");

        Keyboard kb = Keyboard.current;
        if (best != null && kb != null && kb.eKey.wasPressedThisFrame)
            best.Interact(gameObject);
    }
}
