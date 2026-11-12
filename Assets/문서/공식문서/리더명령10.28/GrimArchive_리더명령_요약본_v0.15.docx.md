# **GrimArchive 리더 명령 요약본**

v0.15  \|  2026년 11월 1일

리더는 파티의 공동 행동과 집중 공격을 지휘하고, 강공격·제압·보호로 기술 사용의 우선순위를 바꾼다. 리더를 잃으면 해당 명령과 버프가 끝나고, 후임 선정 전까지 파티원이 각자 행동한다.

정확한 계산·예외·검증 사례는 [리더 명령 설계명세서](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0)

# **1\. 리더의 권한**

**1-표1. 행동을 결정하는 주체**

| 주체 | 결정 범위 |
| :---- | :---- |
| **리더** | 집결·문 개방·파괴·방 이동·후퇴, 코어 직접 조사, 집중 공격 대상, 강공격·제압·보호. |
| **각 파티원** | 개인 이동 경로·공격 위치·사용 기술·치료 대상. 리더 자신도 이 개인 판단을 수행한다. |

다음 방은 리더가 직접 확인하거나 전달받은 정보로 고른다. 임무별 선택 기준은 파티 구성에서 정한다. 리더가 없어도 파티 소속과 임무는 유지한다.

## **1-1. 리더의 위치**

공동 이동을 시작할 때부터 리더를 포함한 각 파티원은 자신의 전투 역할에 맞는 진형 위치를 따른다. 힐러 리더도 적을 집중 공격 대상으로 지정하지만, 본인은 치료·지원을 계속한다. 공격할 적과 치료할 아군은 따로 판단한다.

# **2\. 리더 선정**

**2-표1. 초기 선정과 리더 유지**

| 상황 | 기준 |
| :---- | :---- |
| **최초 선정** | 던전 입장 전 편성 단계에서 확정한다. 입장할 때 다시 선정하지 않는다. |
| **선정 자격** | 생존·개인 도주 미확정·기본 통솔력 10 이상. 기본 통솔력 → 등급 → 미리 정한 무작위 순서로 비교한다. |
| **선정 후 유지** | 통솔력이 더 높은 인원이 충원되어도 자동 교체하지 않는다. 현재 통솔력이 10 미만이 되어도 기존 리더 자격을 유지한다. |
| **리더 부재** | 자격을 갖춘 유닛이 없으면 리더 없이 행동한다. 사망·도주에 따른 후임 선정은 10항을 따른다. |

# **3\. 통솔력**

통솔력은 0\~100이다. 기본 통솔력은 일시 보정 전 값이며 리더 선정과 기본 버프 자격에 사용한다. 현재 통솔력은 보정을 반영한 값이며 새 명령·전파 범위·효과 강도를 결정한다.

**3-표1. 현재 통솔력별 지휘 자격**

| 현재 통솔력 | 추가로 사용할 수 있는 지휘 |
| :---- | :---- |
| **20 이상** | 집중 공격 등 단순 전술 명령. |
| **40 이상** | 강공격·제압·보호. |
| **60 이상** | 별도로 임명된 상위 지휘관의 상위 명령. |
| **80 이상 / 100** | 보유한 고유 지휘 스킬 / 해당 유닛의 최종 강화. |

명령·스킬을 실제로 보유하고 다른 사용 조건도 충족해야 한다. 통솔력이 낮아져도 이미 받은 명령은 각 명령의 종료 조건까지 유지하며, 집결·문 개방·파괴·이동·후퇴는 기존 행동 조건으로 판단한다.

## **3-1. 정신력 감소 완화**

현재 지휘 중인 리더 본인의 일반 정신력 감소량을 현재 통솔력 × 0.5%만큼 완화한다. 통솔력 20·60·100일 때 각각 10%·30%·50%이며, 일반 파티원에게는 적용하지 않는다.

일반 보정 후 계산하며 소수점을 유지한다. 정신력을 −71·−100으로 강제 설정하는 경우와 이미 확정된 도주에는 적용하지 않는다. 계산·예외: [명세서 3-1항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.iji22tw03v2w)

# **4\. 명령 종류**

적 발견 대사 “적이다\! 공격해\!”는 정보 전파다. 별도 공격 명령이나 쿨타임을 만들지 않으며, 각 유닛은 자신의 행동 기준으로 즉시 대응한다.

**4-표1. 전술 명령에 따른 행동 차이**

| 명령 | 발령을 검토하는 상황 | 명령을 받은 뒤의 행동 |
| :---- | :---- | :---- |
| **집중 공격** | 직접 확인한 적의 점수가 신규 지정·교체 기준을 충족한다.[\[1\]](#bookmark=id.o4nfjqkd0uxj) | 지정한 적의 공격을 우선한다. 긴급 보호·치료와 수행 조건은 유지한다. |
| **강공격** | 새 교전·집중 공격 대상 변경·공격 대상 행동 불능·확인한 적 HP 30% 이하. | 현재 공격 대상을 유지하고 고위력 공격 기술을 우선한다. |
| **제압** | 아군의 근접 교전 또는 힐러·원거리 아군에 대한 적의 근접 공격을 확인한다. | 지정 적에게 CC기, 그 적을 공격하는 아군에게 공격 버프를 우선한다. |
| **보호** | 자신 또는 아군이 HP 30% 이하로 공격 위협을 받거나, 행동 불능 중 공격받는다. | 각자 개인 보호 기준으로 치료·보호 버프·위협 적 CC기를 우선한다. |
| **상위 공동 공격** | 현재 직접 확인한 적의 집중 공격 조건을 충족하고, 같은 교전에 참여할 소속 파티 둘 이상에 전달할 수 있다. | 명령을 받은 여러 파티가 지정 적 하나를 우선 공격한다. |

강공격·제압·보호는 현재 통솔력 40 이상, 해당 명령 보유, 리더 명령의 쿨타임 종료, 명령을 받을 파티원 중 지금 연결 기술을 사용할 인원 1명 이상을 모두 요구한다.

제압은 힐러·원거리 아군을 자신의 근접 공격 사거리 안에서 공격하는 적을 먼저 비교하며, 이 경우 그 적에게 적용할 CC기를 지금 쓸 파티원이 필요하다. 다른 아군의 근접 교전을 지원할 때는 CC기 또는 공격 버프가 가능하면 된다.[\[14\]](#bookmark=id.1ssucraj4w52)

보호는 아군 한 명을 공동 대상으로 지정하지 않는다. 각 파티원이 보호할 대상을 고르며, 사망 예측을 기본 긴급 보호 조건으로 사용하지 않는다.

**4-표2. 명령 중 기술 선택**

| 상태 | 선택 기준 |
| :---- | :---- |
| **평소** | 기존 역할별 행동 기준과 기술 우선도. |
| **행동 우선 명령 적용 중** | 지금 사용할 수 있는 연결 기술의 명령별 순위를 먼저 비교하고, 동순위는 기존 개인 기준으로 선택한다.[\[3\]](#bookmark=id.6d48rfpboxcf) |
| **연결 기술을 쓸 수 없음** | 일반 행동을 계속한다. 남은 적용시간 안에 사용 조건을 충족하면 명령을 수행한다. |

CC기는 기절·속박처럼 행동을 제한하는 기술이다. 치료 HP·기술 사용·행동 취소 조건은 기존 전투 규칙을 따른다.

## **4-1. 명령 시간**

**4-1-표1. 적용시간과 리더 명령의 쿨타임**

| 구분 | 시간 | 의미 |
| :---- | :---- | :---- |
| **강공격·제압·보호 적용** | 5초 | 파티원은 명령을 받은 때부터, 리더는 명령한 때부터 기술 우선순위를 적용한다. |
| **일반 전술 명령 후 쿨타임** | 10초 | 같은 지휘자는 새 일반·상위 전술 명령을 기다린다. |
| **상위 전술 명령 후 쿨타임** | 15초 | 같은 지휘자의 일반·상위 전술 명령이 함께 기다린다. |
| **고유 지휘 스킬** | 스킬별 설정 | 개별 쿨타임과 리더 명령의 쿨타임을 모두 확인한다. |
| **집결·문 개방·파괴·이동·후퇴** | 위 쿨타임 없음 | 기존 조건을 충족하면 명령하며 전술 명령 쿨타임을 다시 시작하지 않는다. |

쿨타임은 지휘자별로 계산한다. 그 동안 파티원은 공격·기술·기존 명령 수행을 계속한다. 새 행동 우선 명령은 이전 우선순위를 대체하며, 같은 명령을 다시 전달해도 적용시간을 늘리지 않는다.

## **4-2. 명령 선택**

동시에 가능한 전술 명령은 고유 스킬 → 상위 공동 공격 → 집중 공격 → 행동 우선 명령 순서로 검토한다. 강공격·제압·보호끼리는 현재 발령 조건을 모두 충족한 명령 중 다음 순서로 선택한다.

**4-2-표1. 파티 컨셉별 행동 우선 명령**

| 파티 컨셉 | 먼저 선택 → 나중에 선택 |
| :---- | :---- |
| **모험가** | 보호 → 제압 → 강공격 |
| **도적** | 강공격 → 제압 → 보호 |
| **용병** | 강공격 → 보호 → 제압 |
| **군대** | 제압 → 보호 → 강공격 |
| **용사** | 보호 → 강공격 → 제압 |

파티 컨셉의 순서는 후임 리더에게도 유지한다. 개인의 긴급 보호·치료·자기방어와 행동 취소 기준은 별도로 유지한다.

## **4-3. 다시 명령할 때**

**4-3-표1. 명령을 반복하는 조건**

| 명령 | 다시 명령할 때 확인할 내용 |
| :---- | :---- |
| **제압·보호** | 10초 쿨타임 뒤 현재 위험·사용 기술·통솔력·보유·전달·우선순위를 다시 확인한다. 조건이 남아 있으면 새로 명령한다. |
| **제압 대상** | 같은 교전에서 직전 대상은 기억하되, 현재 먼저 비교할 적들에 포함되고 생존·직접 확인·교전·기술 조건을 계속 충족할 때 다시 지정한다. |
| **강공격** | 기존 발령 계기가 필요하다. HP 30% 이하 계기는 같은 적·같은 교전에서 1회이며 처음 확인한 때 이미 30% 이하인 경우도 포함한다. |

명령이 즉시 전달됐다면 5초에 우선순위 적용이 끝나고 남은 쿨타임 5초 동안 개인 행동을 한다. 직전 제압 대상을 기억해도 끝난 우선순위가 계속 적용되지는 않는다.

# **5\. 집결**

전투가 있었던 방은 전투 종료 후 5초 경계를 마치고 임무별 활동 종료를 확인한다. 후퇴 조건과 리더가 아는 미처리 코어의 처리를 먼저 확인한다.[\[2\]](#bookmark=id.h90bipmjdakf)

**5-표1. 집결을 검토하는 임무별 기준**

| 임무 | 현재 방에서 확인할 기준[\[4\]](#bookmark=id.s3px3u782032) |
| :---- | :---- |
| **탐색** | 방 지형 전체 확인 결과. 전체 임무 수량을 먼저 달성하면 임무 달성 후 기준을 따른다.[\[5\]](#bookmark=id.9jfzz5eva07z) |
| **회수** | 리더가 아는 수행 가능한 회수 대상의 처리 완료. 미발견 회수물을 전부 찾을 필요는 없다. |
| **소탕** | 다음 문까지 확인한 범위에 적이 없고 대응할 적·교전 정보가 남지 않음. 방 전체 전멸 확인은 필요 없다. |
| **점령** | 필수 시설 완공과 남은 전투를 확인해 다음 진출·거점 대기를 판단. 목표 달성만으로 외부 귀환하지 않는다. |
| **보스 공략** | 보스 임무 완료 결과와 남은 전투. 개별 완료 조건은 후속 콘텐츠에서 정한다. |

기본 대기 기한은 집결 명령 후 8초다. 미합류자의 도착 예정 정보를 받으면 가장 늦은 도착 예정 시각＋3초와 기본 기한 중 늦은 시각까지 기다린다. 전원이 먼저 합류하면 즉시 완료하고, 같은 정보를 다시 받아도 기한을 새로 시작하지 않는다.[\[6\]](#bookmark=id.nmkz76gjc3tv)

기한 뒤 마지막 위치를 아는 미합류자는 5초 수색한다. 위치 정보가 없으면 미확인 상태로 이번 대기에서 제외한다. 단순 부재를 사망·도주로 확정하지 않는다.

# **6\. 집중 공격**

신규 지정·대상 교체는 리더가 현재 직접 확인한 적끼리 비교한다. 지휘 목표 점수에는 리더가 기록한 위험도와 파티 종류별 위협 배율·대상 종류 배율을 사용한다.[\[7\]](#bookmark=id.cauzn6cuq5ss)

**6-표1. 집중 공격을 새로 지정하거나 바꾸는 조건**

| 상황 | 점수 조건 |
| :---- | :---- |
| **새로 지정** | 직접 확인한 적이 2명 이상이고, 최고 점수 ≥ 두 번째 점수 × 1.5. |
| **기존 적에서 교체** | 다른 적의 점수 ≥ 현재 지정한 적의 점수 × 1.2. |

현재 통솔력·쿨타임·명령 전달·전술 명령 순서도 충족해야 한다. 점수 계산과 배율표: [명세서 10항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.id5g8rxf0ght)

**6-표2. 지정한 적을 놓쳤을 때**

| 파티원의 상황 | 집중 공격 수행 |
| :---- | :---- |
| **다른 교전 없음** | 마지막 확인 위치를 최대 5초 추적한다. 도착 후 최대 3초 대기는 이 5초에 포함한다. |
| **다른 적과 교전** | 그 파티원만 기존 추적과 집중 공격 지정을 즉시 끝낸다. 다른 파티원은 유지한다. |
| **긴급 보호·치료 발생** | 긴급 행동을 먼저 하지만 추적 기한은 멈추거나 갱신하지 않는다. |

집중 공격 전체에 고정된 유지시간은 없다. 개인별로 추적 기한과 다른 종료 조건을 판단하며, 종료한 지정을 근거로 이전 추적을 자동 재개하지 않는다.[\[8\]](#bookmark=id.zdbzk68i7k4n)

집중 공격에는 개인 공격 대상 변경 이동 한도를 적용하지 않는다. 다만 역할별 수행 조건과 자신의 “다른 근접 적과의 접근 제한 거리”는 적용한다. 적의 숨은 실제 사거리로 판단하지 않으며, 허용 위치·경로가 모두 불가능하다고 확인된 파티원만 지정을 끝낸다.

# **7\. 판단 정보**

**7-표1. 사용할 수 있는 정보**

| 정보 | 범위 |
| :---- | :---- |
| **적의 HP 비율** | 관련 기록·도감 이해도로 HP 정보가 해금되고 현재 직접 확인한 경우. 해금 단계는 도감 설계에서 정한다. |
| **인류 아군의 HP 비율** | 모든 인류 유닛이 직접 확인한 인류 아군의 HP 비율을 안다. 다른 파티도 같으며 시야 밖 HP는 자동 갱신하지 않는다. |
| **발견·위치·사망·조사 결과** | 허용된 전파 조건으로 다른 파티와도 교환한다.[\[9\]](#bookmark=id.f8b0vwd9h278) |
| **위험도·예상 피해량** | 개인 평가 수치이므로 정보 교환만으로 다른 유닛의 기록에 복사하지 않는다. |

과거 HP를 현재값으로 삼아 새 HP 조건을 판단하지 않는다. 정보가 전달됐다는 사실만으로 지휘 관계가 생기거나 명령을 받은 것으로 처리하지 않는다.

## **7-1. 명령 전달**

명령은 현재 통솔력에 따른 전파 범위를 사용한다. 소속과 실제 전달 조건을 충족한 파티원에게 적용하며, 상위 명령의 한 번 전달은 9항을 따른다.[\[10\]](#bookmark=id.y7jycg2tholm)

# **8\. 기본 버프**

**8-표1. 기본 버프의 적용 기준**

| 항목 | 기준 |
| :---- | :---- |
| **적용 자격** | 리더의 기본 통솔력 20 이상.[\[11\]](#bookmark=id.8fj63hbjbc86) |
| **강도** | 현재 통솔력 × 0.5%. 현재 통솔력 100이면 50% 증가. |
| **종류** | 초반 모험가: 물리·마법 방어력 / 초반 도적: 물리·마법 공격력.[\[12\]](#bookmark=id.moqlkd8kp7pf) |
| **적용 거리** | 거리 제한 없음. |
| **후임 선정** | 파티 버프 종류는 유지하고, 후임의 기본 통솔력으로 자격·현재 통솔력으로 강도를 다시 계산. |

상위 지휘관의 기본 버프가 적용되는 동안 일반 리더의 기본 버프는 적용하지 않는다. 상위 효과가 더 약해도 같으며, 상위 버프가 끝나면 살아 있는 일반 리더의 버프를 다시 적용한다.

# **9\. 상위 지휘**

상위 지휘관은 원정 편성 단계에서 별도로 임명하며 원정당 최대 한 명이다. 상위 지휘관 없이 편성할 수 있다. 상위 명령에는 현재 통솔력 60 이상과 해당 명령 보유가 필요하다.

**9-표1. 상위 명령의 적용**

| 상황 | 처리 |
| :---- | :---- |
| **지휘 범위** | 같은 원정의 여러 소속 파티. 일반 리더는 자기 파티를 지휘한다. |
| **일반 명령과 충돌** | 일반 명령 수행을 잠시 중단하되 기존 종료 조건·제한 시간은 계속 계산한다. 상위 명령 종료 때 아직 끝나지 않은 일반 명령만 재개한다.[\[13\]](#bookmark=id.wwmjaj80klnp) |
| **소속 리더가 직접 받음** | 자기 전파 범위 안의 자기 파티원에게 같은 명령을 한 번 전달할 수 있다. 다른 리더를 거쳐 계속 전달하지 않는다.[\[13\]](#bookmark=id.wwmjaj80klnp) |
| **상위 지휘관 사망·개인 도주** | 해당 명령·버프 종료. 상위 지휘관을 자동 승계하지 않는다. 일반 리더를 겸했다면 그 파티의 일반 리더 후임만 선정한다. |

상위 명령의 추가 종류는 파티 구성 문서 논의를 마친 뒤 정한다.

# **10\. 지휘 상실**

**10-표1. 리더를 잃은 뒤의 처리**

| 사건 | 즉시 바뀌는 행동 | 일반 리더 후임 선정 |
| :---- | :---- | :---- |
| **리더 사망** | 해당 리더의 명령·버프·집결·공동 이동·후퇴·자동 보호 포메이션 종료. | 실제 사망 시점부터 10초 뒤. |
| **리더의 개인 도주 확정** | 해당 리더의 명령·버프 종료. 파티원은 개인 행동으로 전환. | 생존 소속 파티원 중 처음으로 도주를 확인한 시점부터 10초 뒤. |

살아 있는 상위 지휘관의 기존 명령은 유지한다. 후임은 현재 상황에 맞춰 새 명령을 판단하고 이전 리더의 명령을 자동 복원하지 않는다.

리더의 개인 도주가 확정되면 각 파티원은 그 사실을 처음 알았을 때 기본 정신력 10 감소에 기존 보정을 적용한다. 도주에는 사망 충격을 적용하지 않는다.

**10-순서도1. 일반 리더 사망과 후임 선정**  [\[원본 편집\]](https://app.diagrams.net/?mode=google&lang=ko&page-id=L01#G1H1IDkGON3O4xQUOd4Y5OoAtQWoGgDd26)

![][image1]

후임 선정의 10초는 실제 사망부터 계산한다. 다음 항목의 사망 충격은 각 파티원이 사망 사실을 처음 안 때부터 따로 계산한다.

## **10-1. 사망 충격**

**10-1-표1. 사망을 처음 안 파티원의 변화**

| 변화 | 적용 내용 |
| :---- | :---- |
| **능력치 충격** | 직접 확인·전파로 사망을 처음 안 때부터 10초간 적용 대상 세부 스테이터스 20% 감소. 현재·최대 HP와 MP 제외. |
| **정신력 감소** | 직접 확인: 기본 20 / 전달받음: 기본 10 감소. 일반 파티원 사망 감소량과 더하지 않고 대체한 뒤 기존 보정 적용. |

같은 사망 정보의 반복 전달·다른 리더 사망의 추가 발생·파생 능력치 계산: [명세서 18항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.lvnjcq8ero2k)

## **10-2. 리더가 없는 행동**

소속과 기존 임무는 유지하지만 집결·공동 이동·자동 보호 포메이션은 새로 구성하지 않는다. 정보 교환과 개인 전투·보호·치료 판단은 계속한다.

후임이 없는 파티원은 개인 도주 미확정·HP 20% 이하 등 개인 후퇴 조건에 따라 출구로 후퇴하며 거점 휴식·재입장을 하지 않는다. HP가 20%를 초과하면 종료된 파티 후퇴 명령만을 이유로 후퇴하지 않는다. 별도 조건으로 시작한 개인 후퇴와 확정된 개인 도주는 계속된다.

후임은 “내가 지휘한다\!”라고 말한다. 후임이 없을 때의 반응 대사는 콘텐츠 연출로 표현한다.

# **11\. 전투 예시**

상황: 모험가 파티의 힐러 리더와 전위·궁수가 함께 싸운다. 리더의 현재 통솔력은 40이고 집중 공격·제압·보호를 보유한다. 전위는 기절 기술, 힐러는 공격 버프를 제압 1순위 기술로 보유한다. 궁수에게는 제압에 연결된 기술이 없다.

**11-표1. 집중 공격 후 제압을 명령한 예시**

| 시점·사건 | 각 유닛의 행동 |
| :---- | :---- |
| **집중 공격 명령** | 리더가 직접 확인한 적 2명 중 점수 150인 적 A와 100인 적 B를 비교한다. 다른 발령 조건도 충족해 A를 지정하고 10초 쿨타임을 시작한다. |
| **명령 10초 뒤** | 전위가 A와 근접 교전 중이다. 보호·고유 스킬·상위 명령·새 집중 공격의 발령 조건은 없고, 제압 기술의 사용 조건은 충족해 제압을 명령한다. |
| **제압을 즉시 전달받은 뒤** | 전위는 A에게 기절, 힐러는 A를 공격하는 아군에게 공격 버프를 우선한다. 궁수는 기존 집중 공격에 따라 일반 사격을 계속한다. |
| **제압 명령 5초 뒤** | 제압 기술의 우선순위가 끝난다. 집중 공격 지정은 별도 종료 조건이 없으면 유지한다. |
| **제압 명령 10초 뒤** | 현재 위험과 수행 조건을 다시 확인해 새 명령 여부를 판단한다. |

기술 보유 여부가 달라도 명령을 받은 사실은 같다. 연결 기술이 없는 궁수도 일반 행동을 멈추지 않는다. 세 명령의 다른 사용 상황은 명세서 5-3항에서 비교한다.[\[14\]](#bookmark=id.1ssucraj4w52)

# **주석**

본문의 번호를 누르면 해당 주석으로 이동한다. 주석 번호를 누르면 본문의 관련 항목으로 돌아간다.

[\[1\]](#bookmark=id.5qs4kieunqnx) 6-표1의 집중 공격 신규 지정·교체 기준. [명세서 11항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.ndg4fzrswhdb)

[\[2\]](#bookmark=id.df0bytx1o1y5) 5항의 집결 시작과 완료 조건. [명세서 5-2항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.arde43i87l9z)

[\[3\]](#bookmark=id.8fasx3em9bha) 4-표2의 명령별 기술 순위·동순위·연결 기술 설정. [명세서 9항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.x9mcihy7z3u)

[\[4\]](#bookmark=id.y0jg1g941oom) 5-표1의 임무별 활동 종료. [파티 목표 7-1항](https://docs.google.com/document/d/15b1fk34uoS3WAqh_aOy0e-rB8fYe3CvDVmNP4NS5-sQ/edit?tab=t.0#heading=h.avzcl22hvzu)

[\[5\]](#bookmark=id.o36nv5uwk6im) 5-표1의 전체 임무 수량 최초 달성 후 활동. [파티 목표 8항](https://docs.google.com/document/d/15b1fk34uoS3WAqh_aOy0e-rB8fYe3CvDVmNP4NS5-sQ/edit?tab=t.0#heading=h.wdkgjgj3k467)

[\[6\]](#bookmark=id.iypa4cwmdm0f) 5항의 대기 기한·미합류자 수색. [파티 이동 2항](https://docs.google.com/document/d/1ah1hRzFYmsVrGNiKC8DYlV3tg9eztAbSjK6V1eqNMpk/edit?tab=t.0#heading=h.rp2afahci1qf) · [5항](https://docs.google.com/document/d/1ah1hRzFYmsVrGNiKC8DYlV3tg9eztAbSjK6V1eqNMpk/edit?tab=t.0#heading=h.5szi34m00ibo) · [6항](https://docs.google.com/document/d/1ah1hRzFYmsVrGNiKC8DYlV3tg9eztAbSjK6V1eqNMpk/edit?tab=t.0#heading=h.td715v23fvpa)

[\[7\]](#bookmark=id.raov0t7ioush) 6항의 최초 인지 시 전투 역할·위험도 정보. [전투 목표·아군 보호·지원 1항](https://docs.google.com/document/d/1G7k_UKZS8-Z9ij72ZY-c24bC8A210mex2vwD7w1899o/edit?tab=t.0#heading=h.u57hyvq1hk7g)

[\[8\]](#bookmark=id.5t3qmy5k8yn3) 6-표2의 추적과 개인별 종료. [명세서 12-2항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.vdfgll6ehgti)

[\[9\]](#bookmark=id.syt305m61amy) 7-표1의 확인 사실과 개인 평가 수치 구분. [전파·소리·간접인지 개념 5항](https://docs.google.com/document/d/1-oug_Mi0EFkP38XqCrdPSwTa4JFYXu0Y/edit#bookmark=id.ga1194ad927f)

[\[10\]](#bookmark=id.5oou3w3nk6wo) 7-1항의 전파 범위·재전파·공유 예외. [명세서 13항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.w54tj9zez5hq)

[\[11\]](#bookmark=id.b36uzv9wugf) 8-표1의 버프 자격: 현재 통솔력이 일시적으로 20 미만이어도 자격은 유지하며 강도만 낮아진다. [명세서 14항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.8e4x4nxul3s7)

[\[12\]](#bookmark=id.otev52xdfdd5) 8-표1의 버프 종류는 파티 분류를 따른다. 군대·용병·용사 등 미정 효과는 파티 구성·콘텐츠 설계에서 정한다. [명세서 14-2항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.ufexukge0c8o)

[\[13\]](#bookmark=id.r43dr0siy9e7) 9-표1의 상위 공동 공격·일반 명령 재개·소속 리더의 한 번 전달. [명세서 16-1항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.8w1zaiz0t7tu)

[\[14\]](#bookmark=id.lsj0v11ew15y) 4항의 제압 대상 선정과 11항의 비교 예시. [명세서 5-1항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.u7zbxgi6aaau) · [5-3항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0#heading=h.wi3yqplagy4v)

[image1]: <data:image/png;base64,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>