using UnityEngine;

/// <summary>
/// 선택지가 있는 이벤트의 공통 뼈대. 상호작용하면 대사창을 열고, 다 끝나면 출구를 연다.
/// 층별 내용은 <see cref="BuildPrompt"/>를 구현해서 채운다.
/// </summary>
public abstract class ChoiceEvent : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "E : 살펴본다";
    [SerializeField] private ExitDoor exitDoor;
    [Tooltip("대사창에 띄울 얼굴. 결과에 따라 나오는 것이라 처음 설명에는 쓰지 않는다.")]
    [SerializeField] protected Sprite portrait;

    private bool finished;

    public bool CanInteract => !finished && !EventDialogue.IsOpen;
    public string Prompt => prompt;

    public void Interact(GameObject interactor)
    {
        if (finished || EventDialogue.IsOpen) return;

        Player = interactor;
        EventPrompt first = BuildPrompt();
        if (first == null) { Finish(); return; }

        if (UIManager.Instance == null || !UIManager.Instance.ShowEvent(first, Finish))
            Finish();
    }

    /// <summary>상호작용한 플레이어. 체력을 깎거나 회복할 때 쓴다.</summary>
    protected GameObject Player { get; private set; }

    protected Health PlayerHealth => Player != null ? Player.GetComponent<Health>() : null;

    /// <summary>처음 띄울 설명과 선택지.</summary>
    protected abstract EventPrompt BuildPrompt();

    /// <summary>이벤트가 끝났다. 출구를 열고 더 이상 말을 걸 수 없게 한다.</summary>
    private void Finish()
    {
        finished = true;
        if (exitDoor != null) exitDoor.SetOpen(true);
        if (UIManager.Instance != null) UIManager.Instance.SetHint("");
        OnFinished();
    }

    /// <summary>끝난 뒤 연출이 필요하면 덮어쓴다.</summary>
    protected virtual void OnFinished() { }
}
