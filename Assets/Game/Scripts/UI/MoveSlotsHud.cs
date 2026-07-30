using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 오른쪽 아래 기술 칸 네 개. 생김새는 <c>Assets/Game/Art/UI/moves.png</c>의 기술 목록을
/// 따른다 — 금색 베벨 테두리 안에 <b>위는 밝은 띠에 기술 이름</b>, <b>아래는 어두운 띠에
/// 속성 꼬리표와 조작키</b>가 놓인다 (원작은 그 자리에 속성 배지와 PP가 있다).
///
/// 예전에는 남색 판에 흰 글자였는데, 판이 반투명이라 뒤쪽 지형에 따라 글자가 묻혔다.
/// 지금은 밝은 띠에 어두운 글자라 무엇이 뒤에 있든 대비가 변하지 않는다.
///
/// 쿨타임이 도는 동안에는 칸이 어두워졌다가 왼쪽에서부터 밝아진다.
/// 어두운 덮개를 오른쪽 기준으로 채워 두고 그 양을 줄이면, 밝은 부분이 왼쪽부터 자라난다.
/// </summary>
public class MoveSlotsHud : MonoBehaviour
{
    private const float MarginX = 24f;
    private const float MarginY = 24f;
    private const float SlotWidth = 176f;
    private const float SlotHeight = 64f;
    private const float Gap = 8f;
    /// <summary>테두리 스프라이트의 9슬라이스 두께. 안쪽 띠는 이만큼 물러나 앉는다.</summary>
    private const float FrameInset = 6f;
    /// <summary>위쪽(이름) 띠의 높이. 아래 띠는 남는 만큼 가져간다.</summary>
    private const float NameBandHeight = 32f;

    // ⚠️ 띠는 어둡게, 글자는 밝게. PMD 폰트는 글리프에 검은 윤곽이 구워져 있어서
    // (PmdUi.MakeText 참고) 어두운 글자를 주면 윤곽과 뭉쳐 덩어리가 된다. 처음에는
    // moves.png처럼 밝은 띠에 어두운 글자로 뒀다가 기술 이름이 읽히지 않았다.
    // 원작은 자기 폰트라 그렇게 할 수 있었지만 우리 폰트는 밝은 글자용이다.
    private static readonly Color NameBand = new Color32(58, 52, 40, 255);
    private static readonly Color InfoBand = new Color32(92, 80, 62, 255);
    private static readonly Color NameText = new Color32(255, 252, 240, 255);
    private static readonly Color InfoText = new Color32(236, 226, 206, 255);

    private static readonly Color LockedBand = new Color32(64, 64, 68, 255);
    private static readonly Color LockedInfoBand = new Color32(80, 80, 84, 255);
    private static readonly Color LockedText = new Color32(150, 150, 156, 255);

    /// <summary>배웠지만 지금은 쓸 수 없을 때(전투방 밖) 띠를 눌러 두는 정도.</summary>
    private static readonly Color RestingTint = new Color(0.72f, 0.72f, 0.74f, 1f);
    private static readonly Color CooldownVeil = new Color(0.05f, 0.05f, 0.08f, 0.66f);

    private class Slot
    {
        public RectTransform root;
        public Image frame;
        public Image nameBand;
        public Image infoBand;
        public Image veil;
        public Text nameText;
        public Text keyText;
        /// <summary>왼쪽 아래 속성 꼬리표 — 근접·원거리 같은 공격 속성.</summary>
        public Image tagChip;
        public Text tagText;
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

    /// <summary>
    /// 속성 꼬리표의 색. 포켓몬 타입 배지처럼 <b>짙은 바탕에 흰 글자</b>다 — 우리 폰트는
    /// 밝은 글자용이라(<see cref="PmdUi.MakeText"/>) 바탕이 짙어야 글자가 산다.
    /// </summary>
    private static void TagPalette(string tag, out Color box, out Color ink)
    {
        ink = new Color32(255, 252, 244, 255);
        switch (tag)
        {
            case "근접":
                box = new Color32(184, 76, 32, 255);
                return;
            case "원거리":
                box = new Color32(48, 84, 168, 255);
                return;
            default:                                  // "방당 1회" 같은 제약 표시
                box = new Color32(42, 112, 60, 255);
                return;
        }
    }

    private void Build(RectTransform root)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            // 0 1
            // 2 3  — 왼쪽 위부터 오른쪽으로 채운다.
            int column = i % 2;
            int row = i / 2;

            var slot = new Slot();

            slot.frame = PmdUi.MakeSliced(root, "Slot" + i, PmdUi.MoveFrameSprite);
            slot.root = slot.frame.rectTransform;
            slot.root.anchorMin = slot.root.anchorMax = new Vector2(0f, 1f);
            slot.root.pivot = new Vector2(0f, 1f);
            slot.root.anchoredPosition = new Vector2(column * (SlotWidth + Gap),
                                                     -row * (SlotHeight + Gap));
            slot.root.sizeDelta = new Vector2(SlotWidth, SlotHeight);

            // 위쪽 밝은 띠 — 기술 이름이 앉는다. 위 모서리에 걸어 두고 아래로 자란다.
            slot.nameBand = MakeBand(slot.root, "NameBand", NameBand);
            RectTransform nameRt = slot.nameBand.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.sizeDelta = new Vector2(-FrameInset * 2f, NameBandHeight - FrameInset);
            nameRt.anchoredPosition = new Vector2(0f, -FrameInset);

            // 아래쪽 어두운 띠 — 속성과 조작키.
            slot.infoBand = MakeBand(slot.root, "InfoBand", InfoBand);
            slot.infoBand.rectTransform.anchorMin = new Vector2(0f, 0f);
            slot.infoBand.rectTransform.anchorMax = new Vector2(1f, 1f);
            slot.infoBand.rectTransform.offsetMin = new Vector2(FrameInset, FrameInset);
            slot.infoBand.rectTransform.offsetMax = new Vector2(-FrameInset, -NameBandHeight);

            slot.nameText = PixelUi.MakeText(slot.nameBand.rectTransform, "Name", 24,
                                             NameText, TextAnchor.MiddleCenter);
            slot.nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(slot.nameText.rectTransform, 4f, 0f);

            // 속성 꼬리표는 아래 띠의 왼쪽에 붙는다.
            slot.tagText = PmdUi.MakeChip(slot.infoBand.rectTransform, "Tag", "", 12,
                                          Color.white, Color.black);
            slot.tagChip = slot.tagText.transform.parent.GetComponent<Image>();
            RectTransform chipRt = slot.tagChip.rectTransform;
            chipRt.anchorMin = chipRt.anchorMax = new Vector2(0f, 0.5f);
            chipRt.pivot = new Vector2(0f, 0.5f);
            chipRt.sizeDelta = new Vector2(66f, 18f);
            chipRt.anchoredPosition = new Vector2(5f, 0f);

            // 조작키는 반대쪽(오른쪽)에 적는다. 원작의 PP가 있던 자리다.
            slot.keyText = PixelUi.MakeText(slot.infoBand.rectTransform, "Key", 12,
                                            InfoText, TextAnchor.MiddleRight);
            slot.keyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(slot.keyText.rectTransform, 6f, 0f);

            // 덮개는 글자 위에 와야 쿨타임 중이라는 게 확실히 보인다.
            slot.veil = PmdUi.MakeSliced(slot.root, "Veil", null);
            slot.veil.color = CooldownVeil;
            slot.veil.type = Image.Type.Filled;
            slot.veil.fillMethod = Image.FillMethod.Horizontal;
            slot.veil.fillOrigin = (int)Image.OriginHorizontal.Right;
            Stretch(slot.veil.rectTransform, FrameInset, FrameInset);

            slots[i] = slot;
        }
    }

    private static Image MakeBand(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = PrimitiveSprites.Square;
        image.color = color;
        image.raycastTarget = false;
        return image;
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

        // 전투방 밖에서도, 방을 다 정리한 뒤에도 기술을 쓸 수 없다. 눌러도 아무 일이 없으면
        // 고장으로 보이므로 칸 전체를 흐리게 해서 지금은 쓸 수 없다는 걸 알린다.
        bool usable = PlayerCombat.MovesUsable;

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null) continue;

            MoveType move = MoveInfo.LearnOrder[i];
            bool learned = moves != null && moves.Has(move);

            if (!learned)
            {
                slot.frame.sprite = PmdUi.MoveFrameOffSprite;
                slot.nameBand.color = LockedBand;
                slot.infoBand.color = LockedInfoBand;
                slot.nameText.text = "—";
                slot.nameText.color = LockedText;
                slot.keyText.text = "";
                slot.tagChip.gameObject.SetActive(false);
                slot.veil.fillAmount = 0f;
                continue;
            }

            slot.frame.sprite = PmdUi.MoveFrameSprite;
            // 색은 그대로 두고 밝기만 낮춘다 — 쓸 수 없다는 것과 못 배웠다는 것이 달라 보여야 한다.
            Color tint = usable ? Color.white : RestingTint;
            slot.frame.color = tint;
            slot.nameBand.color = NameBand * tint;
            slot.infoBand.color = InfoBand * tint;

            slot.nameText.text = MoveInfo.NameOf(move);
            slot.nameText.color = NameText;
            slot.keyText.text = MoveInfo.KeyLabelOf(move);
            slot.keyText.color = InfoText;

            string tag = MoveInfo.TagOf(move);
            slot.tagChip.gameObject.SetActive(!string.IsNullOrEmpty(tag));
            TagPalette(tag, out Color box, out Color ink);
            slot.tagChip.color = box * tint;
            slot.tagText.text = tag;
            slot.tagText.color = ink;

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
