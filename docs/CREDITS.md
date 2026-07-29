# 크레딧

**이상해씨의 던전 탐험** — 비상업 팬 프로젝트 (포켓몬 로그라이트 프로토타입)

## 캐릭터 스프라이트

출처: [PMD Sprite Repository](https://sprites.pmdcollab.org/) (PMDCollab/SpriteCollab)

| 포켓몬 | 도감 번호 | 제작자 (credits.txt 기준) |
|---|---|---|
| 이상해씨 | 0001 | CHUNSOFT (공식 PMD 유래) |
| 이상해풀 | 0002 | CHUNSOFT (공식 PMD 유래) |
| 이상해꽃 | 0003 | CHUNSOFT (공식 PMD 유래) |
| 캐터피 | 0010 | CHUNSOFT (공식 PMD 유래) |
| 고지 | 0028 | CHUNSOFT + Emmuffin 리워크 (PMDCollab 라이선스) |
| 나인테일 | 0038 | CHUNSOFT + baronessfaron 리워크 (Walk, PMDCollab 라이선스) |
| 단데기 | 0011 | CHUNSOFT (공식 PMD 유래) |
| 버터플 | 0012 | CHUNSOFT (공식 PMD 유래) |
| 콘팡 | 0048 | CHUNSOFT (공식 PMD 유래) |
| 닥트리오 | 0051 | CHUNSOFT (공식 PMD 유래) |
| 강챙이 | 0062 | CHUNSOFT (공식 PMD 유래) |
| 모다피 | 0069 | CHUNSOFT (공식 PMD 유래) |
| 쥬래곤 | 0087 | CHUNSOFT (공식 PMD 유래) |
| 킹크랩 | 0099 | CHUNSOFT (공식 PMD 유래) |
| 데구리 | 0075 | CHUNSOFT + Roll(Special0)은 Emmuffin 제작 (CC BY-NC 4.0) |
| 텅구리 | 0105 | CHUNSOFT (공식 PMD 유래) |
| 시라소몬 | 0106 | CHUNSOFT (공식 PMD 유래) |
| 홍수몬 | 0107 | CHUNSOFT (공식 PMD 유래) |
| 코뿌리 | 0112 | CHUNSOFT (공식 PMD 유래) |
| 아쿠스타 | 0121 | CHUNSOFT (공식 PMD 유래) |
| 스라크 | 0123 | CHUNSOFT (공식 PMD 유래) |
| 잉어킹 | 0129 | CHUNSOFT (공식 PMD 유래) |
| 갸라도스 | 0130 | CHUNSOFT (공식 PMD 유래) |
| 라프라스 | 0131 | CHUNSOFT (공식 PMD 유래) |
| 신뇽 | 0148 | CHUNSOFT (공식 PMD 유래) |
| 잠만보 | 0143 | CHUNSOFT (공식 PMD 유래) |

각 포켓몬의 원본 `credits.txt`는 `Assets/ThirdParty/PMDCollab/<도감번호_이름>/`에 보관되어 있다.

발밑 그림자도 같은 저장소의 애니메이션별 `*-Shadow.png` 시트에서 가져왔다. 시트의
빨강/초록/파랑은 그림자 크기 등급(소/중/대)별 영역이고, 종의 등급은 `AnimData.xml`의
`ShadowSize`를 따른다. 게임에서는 등급에 맞는 영역만 남긴 흰색 마스크
(`{애니}Shadow.png`)로 구워 `PmdFootShadow`가 본체 프레임과 짝지어 그린다.

## 대사창 얼굴

`Assets/Game/Art/Portraits/` — 같은 저장소의 `portrait/<도감번호>/Normal.png` (40×40).
잠만보·시라소몬·홍수몬·라프라스 네 장을 이벤트 대사창에 쓴다 (잉어킹 얼굴은 이벤트가
사라져 지금은 쓰는 곳이 없지만 보스전 소환물과 함께 보관한다). 원본 `credits.txt`는
스프라이트와 같은 폴더에 `portrait_credits.txt`로 함께 보관한다.

## 아이템 아이콘

- `item.png` — "Ripped by redblueyellow. No credit needed." 표기. 원저작: 게임프리크/닌텐도.
- 자뭉열매·행복의알 아이콘을 잘라 배경 투명화(`relic_icons.png`)하여 사용.

## 저작권 고지

Pokémon © Nintendo / Creatures Inc. / GAME FREAK inc.
Pokémon Mystery Dungeon © Spike Chunsoft.
본 프로젝트는 어떤 형태로도 판매·수익화하지 않는 학습용 팬 프로젝트다.
