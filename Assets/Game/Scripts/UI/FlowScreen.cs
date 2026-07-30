using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 흐름 화면(타이틀·캐릭터 선택·안내·결과)이 함께 쓰는 뼈대.
///
/// 네 화면이 하는 일이 거의 같다 — 캔버스를 하나 세우고, 대화창 칸을 몇 개 놓고, 마우스가
/// 올라간 칸을 밝히고, 누르면 그 칸의 일을 한다. 그 반복을 여기 모아 둔다.
///
/// uGUI 버튼을 쓰지 않는 이유: 이 씬에는 <c>EventSystem</c>이 없다. 좌클릭이 공격이라
/// 넣으면 게임 입력과 겹친다(<c>RelicTooltip</c>과 같은 사정이다). 그래서 마우스 위치를
/// 사각형과 직접 견주고, 키보드 위/아래로도 고를 수 있게 한다.
///
/// 시간은 <see cref="Time.timeScale"/>이 0인 동안에도 흘러야 하므로 실제 시간으로 잰다.
/// </summary>
public abstract class FlowScreen : MonoBehaviour
{
    protected GameFlow Flow { get; private set; }
    protected Canvas Canvas { get; private set; }
    protected RectTransform Root { get; private set; }

    /// <summary>고를 수 있는 칸들. 파생 화면이 채운다.</summary>
    protected readonly List<PmdUi.Entry> entries = new List<PmdUi.Entry>();

    /// <summary>지금 가리키는 칸. 없으면 −1.</summary>
    protected int cursor = -1;

    /// <summary>칸을 눌렀다. 파생 화면이 처리한다.</summary>
    protected abstract void Activate(int index);

    /// <summary>화면을 세운다. 파생 화면은 <see cref="Build"/>에서 칸을 놓는다.</summary>
    protected static T Create<T>(GameFlow flow, string name, int sortingOrder) where T : FlowScreen
    {
        var go = new GameObject(name);
        var screen = go.AddComponent<T>();
        screen.Flow = flow;
        screen.Canvas = PmdUi.MakeCanvas(go.transform, name + "Canvas", sortingOrder);
        screen.Root = PmdUi.MakeFullScreen(screen.Canvas.transform, "Root");
        screen.Build();
        screen.Refresh();
        return screen;
    }

    protected abstract void Build();

    public void Close()
    {
        if (this != null && gameObject != null) Destroy(gameObject);
    }

    /// <summary>고른 칸을 겉모습에 반영한다.</summary>
    protected void Refresh()
    {
        for (int i = 0; i < entries.Count; i++)
            entries[i].SetSelected(i == cursor);
        OnCursorChanged();
    }

    /// <summary>커서가 옮겨졌다. 곁에 붙은 설명을 갈아 끼울 때 쓴다.</summary>
    protected virtual void OnCursorChanged() { }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        // 마우스가 올라간 칸을 따라간다. 칸 밖이면 지금 칸을 그대로 둔다 —
        // 마우스를 살짝 벗어날 때마다 선택이 풀리면 누르기가 성가시다.
        if (mouse != null)
        {
            Vector2 point = mouse.position.ReadValue();
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].enabled || !entries[i].Contains(point)) continue;
                SetCursor(i);
                break;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].enabled || !entries[i].Contains(point)) continue;
                    Activate(i);
                    return;
                }
            }
        }

        if (kb == null) return;
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Step(1);
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Step(-1);
        if ((kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) &&
            cursor >= 0 && cursor < entries.Count && entries[cursor].enabled)
            Activate(cursor);
    }

    /// <summary>
    /// 커서를 옮기고 소리를 낸다. 칸이 실제로 바뀔 때만 울린다.
    ///
    /// 마우스와 키보드가 커서를 옮기는 길이 둘이라 소리도 두 군데에서 나야 하는데,
    /// <see cref="Refresh"/>에 넣으면 화면을 세울 때(<see cref="Create{T}"/>가 한 번 부른다)도
    /// 울린다. 열자마자 나는 소리는 무엇이 바뀌었다는 뜻이 아니다.
    /// </summary>
    private void SetCursor(int index)
    {
        if (cursor == index) return;
        cursor = PmdUi.TrackHoverSound(cursor, index);
        Refresh();
    }

    /// <summary>고를 수 없는 칸은 건너뛰며 커서를 옮긴다.</summary>
    private void Step(int delta)
    {
        if (entries.Count == 0) return;
        int index = cursor < 0 ? (delta > 0 ? -1 : 0) : cursor;
        for (int i = 0; i < entries.Count; i++)
        {
            index = (index + delta + entries.Count) % entries.Count;
            if (entries[index].enabled) { SetCursor(index); return; }
        }
    }
}
