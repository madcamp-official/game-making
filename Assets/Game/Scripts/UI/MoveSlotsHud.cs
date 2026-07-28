using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 오른쪽 아래 기술 칸 네 개. 포켓몬 DS 게임의 기술 선택 버튼처럼 2×2로 놓고,
/// 칸 하나는 가로가 세로보다 길다.
///
/// 쿨타임이 도는 동안에는 칸이 어두워졌다가 왼쪽에서부터 밝아진다.
/// 어두운 덮개를 오른쪽 기준으로 채워 두고 그 양을 줄이면, 밝은 부분이 왼쪽부터 자라난다.
/// </summary>
public class MoveSlotsHud : MonoBehaviour
{
    private const float MarginX = 24f;
    private const float MarginY = 24f;
    private const float SlotWidth = 156f;
    private const float SlotHeight = 52f;
    private const float Gap = 6f;
    private const int Border = 2;

    private static readonly Color ReadyColor = new Color(0.20f, 0.34f, 0.52f, 0.95f);
    private static readonly Color LockedColor = new Color(0.16f, 0.16f, 0.18f, 0.7f);
    /// <summary>배웠지만 지금은 쓸 수 없을 때 (전투방 밖).</summary>
    private static readonly Color RestingColor = new Color(0.18f, 0.22f, 0.3f, 0.8f);
    private static readonly Color CooldownVeil = new Color(0f, 0f, 0f, 0.62f);

    private class Slot
    {
        public RectTransform root;
        public Image background;
        public Image veil;
        public Text nameText;
        public Text keyText;
    }

    private readonly Slot[] slots = new Slot[MoveInfo.MaxMoves];
    private PlayerCombat combat;
    private PlayerMoves moves;

    public static MoveSlotsHud Create(Transform canvasRoot)
    {
        GameObject go = new GameObject("MoveSlotsHud", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(canvasRoot, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-MarginX, MarginY);
        rt.sizeDelta = new Vector2(SlotWidth * 2f + Gap, SlotHeight * 2f + Gap);

        MoveSlotsHud hud = go.AddComponent<MoveSlotsHud>();
        hud.Build(rt);
        return hud;
    }

    private void Build(RectTransform root)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            // 0 1
            // 2 3  — 왼쪽 위부터 오른쪽으로 채운다.
            int column = i % 2;
            int row = i / 2;

            RectTransform panel = PixelUi.MakePanel(root, "Slot" + i, Border);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(column * (SlotWidth + Gap),
                                                 -row * (SlotHeight + Gap));
            panel.sizeDelta = new Vector2(SlotWidth, SlotHeight);

            Slot slot = new Slot();
            slot.root = panel;
            // PixelUi.MakePanel의 첫 자식이 안쪽 채움이다. 그걸 칸 색으로 쓴다.
            slot.background = panel.GetChild(0).GetComponent<Image>();

            slot.nameText = PixelUi.MakeText(panel, "Name", 24, Color.white, TextAnchor.MiddleCenter);
            slot.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(slot.nameText.rectTransform, 0f, 8f);

            slot.keyText = PixelUi.MakeText(panel, "Key", 12,
                new Color(0.75f, 0.8f, 0.9f, 0.9f), TextAnchor.LowerRight);
            slot.keyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(slot.keyText.rectTransform, 0f, 0f);
            slot.keyText.rectTransform.offsetMin = new Vector2(4f, 3f);
            slot.keyText.rectTransform.offsetMax = new Vector2(-6f, 0f);

            // 덮개는 글자 위에 와야 쿨타임 중이라는 게 확실히 보인다.
            GameObject veilGo = new GameObject("Veil");
            veilGo.transform.SetParent(panel, false);
            slot.veil = veilGo.AddComponent<Image>();
            slot.veil.sprite = PrimitiveSprites.Square;
            slot.veil.color = CooldownVeil;
            slot.veil.raycastTarget = false;
            slot.veil.type = Image.Type.Filled;
            slot.veil.fillMethod = Image.FillMethod.Horizontal;
            slot.veil.fillOrigin = (int)Image.OriginHorizontal.Right;
            Stretch(slot.veil.rectTransform, Border, Border);

            slots[i] = slot;
        }
    }

    private static void Stretch(RectTransform rt, float insetX, float insetY)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(insetX, insetY);
        rt.offsetMax = new Vector2(-insetX, -insetY);
    }

    private void Update()
    {
        if (combat == null || moves == null) Bind();

        // 전투방 밖에서는 기술을 쓸 수 없다. 눌러도 아무 일이 없으면 고장으로 보이므로
        // 칸 전체를 흐리게 해서 지금은 쓸 수 없다는 걸 알린다.
        bool usable = PlayerCombat.MovesUsable;

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            MoveType move = MoveInfo.LearnOrder[i];
            bool learned = moves != null && moves.Has(move);

            if (!learned)
            {
                slot.background.color = LockedColor;
                slot.nameText.text = "—";
                slot.nameText.color = new Color(1f, 1f, 1f, 0.35f);
                slot.keyText.text = "";
                slot.veil.fillAmount = 0f;
                continue;
            }

            slot.background.color = usable ? ReadyColor : RestingColor;
            slot.nameText.text = MoveInfo.NameOf(move);
            slot.nameText.color = usable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            slot.keyText.text = MoveInfo.KeyLabelOf(move);

            float progress = combat != null ? combat.CooldownProgress01(move) : 1f;
            slot.veil.fillAmount = 1f - progress;
        }
    }

    private void Bind()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        combat = player.GetComponent<PlayerCombat>();
        moves = player.GetComponent<PlayerMoves>();
    }
}
