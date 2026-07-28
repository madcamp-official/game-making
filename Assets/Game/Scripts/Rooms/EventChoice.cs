using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이벤트 선택지 하나에 딸린 효과 설명 한 줄. 이롭냐 해롭냐에 따라 색이 갈린다.
/// </summary>
public struct EventEffectLine
{
    public readonly string text;
    public readonly bool harmful;

    private EventEffectLine(string text, bool harmful)
    {
        this.text = text;
        this.harmful = harmful;
    }

    /// <summary>이로운 효과 — 초록.</summary>
    public static EventEffectLine Good(string text) => new EventEffectLine(text, false);

    /// <summary>해로운 효과 — 붉은색.</summary>
    public static EventEffectLine Bad(string text) => new EventEffectLine(text, true);
}

/// <summary>선택지 하나. 고르면 <see cref="resolve"/>가 결과를 만들어 낸다.</summary>
public class EventChoice
{
    public readonly string label;
    public readonly EventEffectLine[] lines;
    public readonly Func<EventOutcome> resolve;

    public EventChoice(string label, Func<EventOutcome> resolve, params EventEffectLine[] lines)
    {
        this.label = label;
        this.resolve = resolve;
        this.lines = lines ?? Array.Empty<EventEffectLine>();
    }
}

/// <summary>
/// 선택의 결과. 대사와 결과 문구를 함께 보여 준다.
///
/// 대사와 결과를 한 화면에 같이 두는 이유: 명세가 "잠만보 대사 &gt; 유물을 받았습니다" 식으로
/// 두 토막을 붙여 쓰는데, 각각 클릭을 요구하면 "추가로 한 번 클릭하면 닫힘"과 어긋난다.
/// </summary>
public class EventOutcome
{
    /// <summary>따옴표 대사. 없으면 비워 둔다.</summary>
    public string quote;

    /// <summary>결과 설명.</summary>
    public string result;

    /// <summary>대사창에 띄울 얼굴. 없으면 글자만 나온다.</summary>
    public Sprite portrait;

    /// <summary>
    /// 비어 있지 않으면 닫지 않고 이 내용으로 선택지를 다시 띄운다. 잠만보 깨우기 실패처럼
    /// "다시 어떻게 하시겠습니까"로 돌아가는 경우에 쓴다.
    ///
    /// 문구가 아니라 프롬프트를 통째로 받는 이유: 실패할 때마다 선택지의 피해량과 확률이
    /// 올라가므로, 같은 선택지를 다시 그리면 안 되고 새로 만든 것을 그려야 한다.
    /// </summary>
    public EventPrompt reopenWith;

    public static EventOutcome Say(string quote, string result, Sprite portrait) =>
        new EventOutcome { quote = quote, result = result, portrait = portrait };

    public static EventOutcome Plain(string result) => new EventOutcome { result = result };

    public static EventOutcome Retry(EventPrompt next) => new EventOutcome { reopenWith = next };
}

/// <summary>대사창과 선택지 팝업에 넘길 한 판의 내용.</summary>
public class EventPrompt
{
    public string intro;
    public Sprite portrait;
    public List<EventChoice> choices = new List<EventChoice>();
}
