# Original User Request

## Initial Request — 2026-08-24T20:32:07+09:00

`Mon_DollKnight`(인형 기사), `Mon_GnoleA`(놀), `Mon_GoblinHoodA`(고블린 후드) 3종 몬스터에 대해 기존 '근접 탱커'와 동일한 스킬/스탯 구성을 적용하여 `units.json`에 유닛 정의를 추가하고, 유닛 프리팹을 생성/연결합니다.

Integrity mode: development

Requirements:
### R1. units.json에 몬스터 3종 데이터 등록
- `Assets/Data/units.json` 파일에 다음 3종 몬스터 유닛을 추가:
  1. **인형 기사 (Mon_DollKnight)**: `typeName: "인형 기사"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_DollKnight"`, 근접 탱커 스탯 및 스킬(`육중한 내리찍기`, `급습 할퀴기`, `발톱 후려치기`) 부여.
  2. **놀 (Mon_GnoleA)**: `typeName: "놀"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_GnoleA"`, 근접 탱커 스탯 및 스킬 부여. (기존 '근접 탱커' entry는 그대로 두거나 호환성 유지).
  3. **고블린 후드 (Mon_GoblinHoodA)**: `typeName: "고블린 후드"`, `unitClass: "Monster"`, `spriteLibrary: "Mon_GoblinHoodA"`, 근접 탱커 스탯 및 스킬 부여.

### R2. 유닛 프리팹 생성 및 에셋 연결 검증
- 각 몬스터가 `Assets/Sprite/Mon/` 폴더 내의 `.spriteLib` (스프라이트 라이브러리) 에셋과 정상적으로 매핑되는지 확인.
- 프리팹 변환 로직(`JsonToUnitPrefabConverter`) 또는 자동화 스크립트를 통해 `Assets/Resources/Units/` 경로에 프리팹이 누락/참조 깨짐 없이 정상 생성되는지 검증.

### Acceptance Criteria:
- `Assets/Data/units.json`에 3종 몬스터(`Mon_DollKnight`, `Mon_GnoleA`, `Mon_GoblinHoodA`) 정보가 올바른 JSON 문법으로 등록됨.
- 모든 몬스터의 `unitClass`가 `"Monster"`로 설정되고 스프라이트 라이브러리 이름이 에셋 파일명과 일치함.
- C# 코드 및 프로젝트 컴파일 에러가 0건이어야 함 (`dotnet build` 검증).
- 3종 몬스터의 프리팹이 `Assets/Resources/Units/`에 생성되거나 정상 로드 가능한 상태여야 함.
