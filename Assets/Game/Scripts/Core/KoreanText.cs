/// <summary>
/// 한글 조사를 앞말에 맞춰 고른다.
///
/// 이름이 들어가는 문장에 "을(를)"처럼 두 개를 다 적어 두면 대사창에서 눈에 걸린다.
/// 유물 이름은 데이터에서 오므로 문장을 통째로 미리 써 둘 수도 없다.
/// </summary>
public static class KoreanText
{
    /// <summary>받침이 있으면 "을", 없으면 "를".</summary>
    public static string ObjectParticle(string word) => Pick(word, "을", "를");

    /// <summary>받침이 있으면 "이", 없으면 "가".</summary>
    public static string SubjectParticle(string word) => Pick(word, "이", "가");

    /// <summary>받침이 있으면 "은", 없으면 "는".</summary>
    public static string TopicParticle(string word) => Pick(word, "은", "는");

    /// <summary>
    /// 마지막 글자에 받침이 있는지 보고 고른다. 한글이 아닌 글자로 끝나면 받침 없는 쪽을 쓴다 —
    /// 숫자나 영문까지 제대로 읽으려면 표를 들고 있어야 하는데, 여기 쓰이는 이름은 전부 한글이다.
    /// </summary>
    private static string Pick(string word, string withFinal, string withoutFinal)
    {
        if (string.IsNullOrEmpty(word)) return withoutFinal;

        char last = word[word.Length - 1];
        // 한글 음절은 U+AC00부터 28개의 받침이 순서대로 붙는다. 나머지가 0이면 받침이 없다.
        if (last < '가' || last > '힣') return withoutFinal;
        return (last - '가') % 28 != 0 ? withFinal : withoutFinal;
    }
}
