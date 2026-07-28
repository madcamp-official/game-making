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
    [SerializeField, Min(0f)] private float radius = 1.4f;

    // 매 프레임 배열 할당을 피하기 위한 공용 버퍼
    private static readonly Collider2D[] overlapBuffer = new Collider2D[12];
    private static readonly ContactFilter2D noFilter = ContactFilter2D.noFilter;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void Update()
    {
        // 이벤트 대사창이 떠 있는 동안에는 E가 대사창 넘기기로 쓰이므로 여기서 또 받으면 안 된다.
        if ((health != null && health.IsDead) || EventDialogue.IsOpen)
        {
            if (UIManager.Instance != null) UIManager.Instance.SetHint("");
            return;
        }

        IInteractable best = null;
        float bestSqrDistance = float.MaxValue;
        Vector2 position = transform.position;
        int count = Physics2D.OverlapCircle(position, radius, noFilter, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            IInteractable interactable = overlapBuffer[i].GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract) continue;
            float sqrDistance = (position - (Vector2)overlapBuffer[i].transform.position).sqrMagnitude;
            if (sqrDistance < bestSqrDistance) { bestSqrDistance = sqrDistance; best = interactable; }
        }

        if (UIManager.Instance != null)
            UIManager.Instance.SetHint(best != null ? best.Prompt : "");

        Keyboard kb = Keyboard.current;
        if (best != null && kb != null && kb.eKey.wasPressedThisFrame)
            best.Interact(gameObject);
    }
}
