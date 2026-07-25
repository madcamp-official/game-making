# 에셋 반입 및 크레딧 규칙

## 1. PMD Sprite Repository 사용 조건

캐릭터 스프라이트의 주요 출처는 [PMD Sprite Repository](https://sprites.pmdcollab.org/)다.

사이트 안내에 따르면 커스텀 그래픽은 `CC BY-NC 4.0` 조건이며, 팬게임 등의 비상업적 사용은 허용하지만 상업적 프로젝트에는 사용할 수 없다. 사용한 에셋마다 참여 제작자를 개별적으로 표시해야 한다.

공식 PMD 게임에서 유래한 그래픽은 커스텀 에셋과 권리 관계가 다를 수 있다. 따라서 이 프로젝트는 비상업적 팬 프로젝트 범위로 제한하고, 외부 공개나 배포 전에 각 에셋의 조건을 다시 확인한다.

## 2. 다운로드 원칙

필요한 캐릭터만 필요한 시점에 받는다. 첫 프로토타입에서는 이상해씨 하나만 받는다.

1. 사이트에서 사용할 포켓몬 페이지를 연다.
2. `Download all sprites`로 스프라이트 묶음을 받는다.
3. 같은 페이지의 `credits.txt`를 함께 받는다.
4. 원본 파일과 크레딧 파일을 포켓몬별 폴더에 보관한다.
5. Unity에서 필요한 동작만 잘라 Animation Clip으로 만든다.
6. 사용하지 않는 대형 원본, 중복 파일, 압축 파일은 Git에 넣지 않는다.

사이트의 Credits Mode에서 실제 사용한 모든 에셋을 선택한 뒤 통합 크레딧 파일을 받는 방식도 사용할 수 있다.

## 3. 권장 저장 위치

```text
Assets/
├── ThirdParty/
│   └── PMDCollab/
│       ├── 0001_Bulbasaur/
│       │   ├── Source/
│       │   └── credits.txt
│       ├── 0002_Ivysaur/
│       └── 0003_Venusaur/
└── Game/
    └── Art/
        └── Characters/
            └── Bulbasaur/
                ├── Sprites/
                ├── Animations/
                └── Bulbasaur.controller
```

- `ThirdParty`: 출처 확인이 가능한 원본과 크레딧
- `Game`: Unity에서 실제 사용하는 Sprite, Animation Clip, Animator
- 폴더명은 `도감번호_영문이름` 형식으로 통일

## 4. Unity 임포트 기준

PMD 스타일 픽셀 스프라이트는 우선 아래 설정으로 통일하고, 실제 크기를 확인하며 조정한다.

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: 시트 구성에 따라 `Multiple`
- Filter Mode: `Point (no filter)`
- Compression: `None`
- Generate Mip Maps: 끔
- Pixels Per Unit: 프로젝트 공통값 사용
- Pivot: 캐릭터 발 위치를 기준으로 통일

`Pixels Per Unit`은 첫 캐릭터와 타일셋으로 테스트한 뒤 한 번만 결정한다. 캐릭터마다 다른 값으로 맞추지 않는다.

## 5. 첫 프로토타입에 필요한 애니메이션

처음부터 사이트의 모든 동작을 연결하지 않는다.

필수:

- `Idle`
- `Walk`
- 기본 공격에 사용할 동작 하나
- `Hurt` 또는 피격 점멸
- `Faint`

후순위:

- 감정 표현
- 수면과 기상
- 복잡한 특수 공격
- 이벤트 전용 포즈
- 사용하지 않는 방향과 변형

## 6. 크레딧 기록

에셋을 추가한 사람이 같은 커밋에서 아래 표와 원본 `credits.txt`를 함께 갱신한다.

| 에셋 | 출처 | 제작자 | 사용 위치 | 크레딧 파일 | 상태 |
|---|---|---|---|---|---|
| 이상해씨 스프라이트 | PMD Sprite Repository (PMDCollab/SpriteCollab, sprite/0001) | CHUNSOFT (공식 PMD 유래, credits.txt 기준) | 플레이어 | `Assets/ThirdParty/PMDCollab/0001_Bulbasaur/credits.txt` | 완료 (Idle·Walk·Attack·Hurt, 2026-07-24) |
| 이상해풀 스프라이트 | PMD Sprite Repository (PMDCollab/SpriteCollab, sprite/0002) | CHUNSOFT (공식 PMD 유래, credits.txt 기준) | 1층 보스 처치 후 | `Assets/ThirdParty/PMDCollab/0002_Ivysaur/credits.txt` | 완료 (Idle·Walk·Attack·Hurt, 2026-07-24) |
| 이상해꽃 스프라이트 | PMD Sprite Repository (PMDCollab/SpriteCollab, sprite/0003) | CHUNSOFT (공식 PMD 유래, credits.txt 기준) | 2층 보스 처치 후 | `Assets/ThirdParty/PMDCollab/0003_Venusaur/credits.txt` | 완료 (Idle·Walk·Attack·Hurt, 2026-07-24) |
| 버터플 스프라이트 | PMD Sprite Repository (PMDCollab/SpriteCollab, sprite/0012) | CHUNSOFT (공식 PMD 유래, credits.txt 기준) | 1층 보스 | `Assets/ThirdParty/PMDCollab/0012_Butterfree/credits.txt` | 완료 (Idle·Walk, 2026-07-24) |
| 코뿌리 스프라이트 | PMD Sprite Repository (PMDCollab/SpriteCollab, sprite/0112) | CHUNSOFT (공식 PMD 유래, credits.txt 기준) | 2층 보스 | `Assets/ThirdParty/PMDCollab/0112_Rhydon/credits.txt` | 완료 (Idle·Walk, 2026-07-24) |
| 갸라도스 스프라이트 | PMD Sprite Repository (PMDCollab/SpriteCollab, sprite/0130) | CHUNSOFT (공식 PMD 유래, credits.txt 기준) | 3층 보스 | `Assets/ThirdParty/PMDCollab/0130_Gyarados/credits.txt` | 완료 (Idle·Walk, 2026-07-24) |
| 잠만보 스프라이트 | PMD Sprite Repository (PMDCollab/SpriteCollab, sprite/0143) | CHUNSOFT (공식 PMD 유래, credits.txt 기준) | 1층 이벤트 | `Assets/ThirdParty/PMDCollab/0143_Snorlax/credits.txt` | 완료 (Sleep 2프레임 루프 애니메이션, 2026-07-25) |
| 아이템 아이콘 시트 (`item.png`) | 시트 내 표기: "Ripped by redblueyellow. No credit needed." (공식 게임 유래 아이콘) | redblueyellow (립), 원저작 게임프리크/닌텐도 | 유물 아이콘 (자뭉열매 10행 2열, 행복의알 14행 6열 → `relic_icons.png`로 가공) | 시트 하단 표기 | 완료 (2026-07-24) |
| 진화 씬 (`Script.txt` + `evolutionbg.png`) | Pokémon Essentials용 "Evolution Scene by KleinStudio V1.1" (팀이 반입) | KleinStudio (원 스크립트·배경), Ruby 코드는 `EvolutionCutscene.cs`로 자체 이식 | 진화 컷씬 배경 (`Assets/Game/Art/UI/evolutionbg.png`), 원본 스크립트는 `Assets/Game/Scripts/Evolution/Script.txt` 보관 | 스크립트 머리주석 표기 | 완료 (2026-07-25) |
| 던전 타일셋 (`Forest/Desert/Sea.png`) | 시트 내 표기: "Ripped and formatted by SilverDeoxys563. No credit is necessary, but it's always appreciated!" (PMD 공식 게임 유래: Forest Path·Northern Desert·Miracle Sea) | SilverDeoxys563 (립), 원저작 Spike Chunsoft | 1·2·3층 방 타일맵 (24px 타일, 물 타일 마젠타 자리표시자 후처리 + 타일별 2px 여백으로 재포장 — 원본은 `Assets/ThirdParty/Tilesets_original/`) | 시트 좌상단 표기 | 완료 (2026-07-25) |

## 7. 커밋 전 확인

- 원본 출처를 알 수 있는가?
- 제작자 이름을 모두 기록했는가?
- `credits.txt`를 함께 보관했는가?
- 비상업적 사용 조건에 맞는가?
- 실제 게임에서 사용하는 파일만 추가했는가?
- 이미지와 `.meta` 파일을 함께 추가했는가?
- 같은 파일의 중복 사본이나 ZIP을 올리지 않았는가?

