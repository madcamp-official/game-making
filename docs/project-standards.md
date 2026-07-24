# 단계 0 — 프로젝트 기준 (확정)

2026-07-24 확정. 로드맵 단계 0의 결정 사항을 기록한다.

## 엔진과 프로젝트 설정

| 항목 | 값 | 상태 |
|---|---|---|
| Unity 버전 | `6000.5.3f1` | 확인 완료 |
| 렌더 파이프라인 | URP 2D (Universal 2D 템플릿) | 확인 완료 |
| Version Control Mode | `Visible Meta Files` | 확인 완료 |
| Asset Serialization | `Force Text` | 확인 완료 |
| 입력 시스템 | Input System (신형) 전용, `activeInputHandler: 1` | 확인 완료 |

## 기준 해상도

- 기본 해상도: **1920×1080 (16:9)**
- 카메라: Orthographic, Size 5 기준으로 시작 (플레이테스트 후 조정)

## 입력 키

| 동작 | 키 |
|---|---|
| 이동 (8방향) | `WASD` 또는 방향키 |
| 기본 공격 | `Space` |

## 전투 방식 (최종 확정)

**실시간 8방향 이동 + 실시간 전투**로 확정한다. 격자 턴제로 전환하지 않는다.
(로드맵 2절의 기본안을 그대로 채택. 개발 중간 전환 금지.)

## 폴더 구조

로드맵 6절의 `Assets/Game` 구조를 그대로 사용한다. 외부 원본은 `Assets/ThirdParty/PMDCollab`에 둔다.

## 스프라이트 공통 값

- Pixels Per Unit: **32** (첫 캐릭터 + 타일 테스트 후 한 번만 재조정 가능)
- Filter Mode: `Point (no filter)`, Compression: `None`, Mip Maps: 끔
- Pivot: 캐릭터 발 위치 기준

## Git

- `.gitignore`는 프로젝트 루트의 Unity 표준 규칙 사용 (`Library`, `Temp`, `Logs`, `UserSettings` 등 제외)
- 커밋에 `.meta` 파일 포함, Scene/Prefab 동시 수정 금지 (로드맵 10절 참고)
