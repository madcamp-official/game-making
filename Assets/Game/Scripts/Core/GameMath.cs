using UnityEngine;

/// <summary>게임 수치 계산에 쓰는 작은 도우미들.</summary>
public static class GameMath
{
    /// <summary>
    /// 반올림 (0.5는 항상 위로).
    ///
    /// <see cref="Mathf.RoundToInt"/>는 0.5에서 가까운 짝수로 가기 때문에
    /// RoundToInt(4.5)가 5가 아니라 4다. 유물 배율처럼 ".5가 자주 나오는" 계산에서
    /// 이 차이가 그대로 체감되므로, 사람이 기대하는 반올림을 쓴다.
    /// </summary>
    public static int RoundHalfUp(float value) =>
        value >= 0f ? Mathf.FloorToInt(value + 0.5f) : Mathf.CeilToInt(value - 0.5f);
}
