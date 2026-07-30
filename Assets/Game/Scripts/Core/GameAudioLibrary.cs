using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 층에 속하지 않는 소리를 모아 둔 곳: 메뉴 음악과 효과음.
///
/// 방 음악은 <see cref="FloorData"/>가 층별로 들고 있다 — 층마다 다르니 층 데이터에 붙는 게 맞다.
/// 반면 메뉴 음악과 "유물을 얻었다" 같은 소리는 층과 무관해서 붙을 자리가 없었다. 그래서 여기
/// 하나로 모으고, <see cref="GameAudio"/>가 Resources에서 이 에셋 하나만 집어 든다.
///
/// mp3 자체를 Resources에 두지 않는 이유는 크기다 (한 곡에 4MB 남짓). Resources 폴더는 쓰이든
/// 말든 통째로 빌드에 들어가므로, 참조만 여기 담고 파일은 Assets/Game/Audio/BGM에 남겨 둔다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Audio Library", fileName = "GameAudioLibrary")]
public class GameAudioLibrary : ScriptableObject
{
    [Header("음악")]
    [Tooltip("타이틀·캐릭터 선택·조작 안내에서 흐르는 곡. 판을 깨고 온 결과 화면도 이 곡이다.")]
    public AudioClip menuBgm;

    [Tooltip("쓰러졌을 때 한 번만 흐르는 곡. 끝나면 다음 화면으로 넘어갈 때까지 아무 소리도 나지 않는다.")]
    public AudioClip gameOverBgm;

    [Header("효과음")]
    [Tooltip("무언가를 손에 넣었을 때 — 유물이든 상점에서 산 회복약이든.")]
    [FormerlySerializedAs("relicAcquired")]
    public AudioClip itemAcquired;

    [Tooltip("기술 강화를 고르고 났을 때.")]
    public AudioClip moveLearned;

    [Tooltip("주인공의 공격이 적에게 닿았을 때.")]
    public AudioClip playerHit;

    [Tooltip("주인공이 얻어맞았을 때.")]
    public AudioClip playerHurt;

    [Tooltip("메뉴에서 고르는 칸이 바뀌었을 때. 커서를 옮기는 내내 울리므로 짧고 작아야 한다.")]
    public AudioClip uiHover;

    [Header("소리 크기")]
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;

    [Tooltip("타격·피격음에만 곱하는 크기. 이 둘은 싸우는 내내 울리므로 획득음과 같은 크기로 두면 " +
             "화면을 뒤덮는다. 0.6이면 다른 효과음의 여섯 할.")]
    [Range(0f, 1f)] public float hitVolumeScale = 0.6f;

    [Tooltip("메뉴 커서음에만 곱하는 크기. 칸을 옮길 때마다 울려서 타격음보다도 작아야 한다.")]
    [Range(0f, 1f)] public float uiVolumeScale = 0.35f;

    [Tooltip("곡이 바뀔 때 겹쳐 넘기는 시간(초). 0이면 바로 갈아탄다.")]
    [Range(0f, 4f)] public float crossfade = 0.5f;

    [Tooltip("방의 적을 모두 물리친 뒤 음악을 얼마로 줄일지. 0.3이면 세 할 크기.")]
    [Range(0f, 1f)] public float clearedRoomVolumeScale = 0.3f;

    [Tooltip("그렇게 줄어들기까지 걸리는 시간(초).")]
    [Range(0f, 4f)] public float duckFade = 0.8f;
}
