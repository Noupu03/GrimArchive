# 파티 구성 설계 명세서

## v0.4  \|  2026년 10월 19일  \|  편성 임무 공동 행동과 검증 사례

파티는 던전 입장 전 정한 소속과 임무를 유지하며, 리더가 집결과 공동 방 이동을 결정한다. 리더를 잃으면 공동 행동을 종료하고 후임 선정 전까지 각자 판단한다. 후임이 없어도 기존 소속과 임무는 유지한다.

# **1\. 파티의 성립과 구성 요소.**

**기획의도** : 인류의 정체·목표·규모를 따로 정해 다양한 침입 편성을 만들 수 있게 한다.

이 문서는 파티 편성·임무·보고·집결·공동 이동과 보호 포메이션을 정한다. 코어 처리와 후퇴·지휘 상실이 공동 행동에 미치는 결과도 설명한다.명령 발령 자격·목표 점수·전파 범위·버프·사망 충격은 「리더와 명령 설계 명세서」를 따른다. [\[관련 명세서 열기\]](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit?tab=t.0)

## **1-1. 기본 용어.**

• **컨셉:** 도적·모험가·군대처럼 인류 집단의 정체와 전투 성향.

• **임무:** 탐색·회수·소탕·점령처럼 출발 전에 부여한 목표.

• **단계:** 위험도·진행 조건에 따라 편성 수준과 규모를 구분하는 기준.

• **독립 유닛:** 처음부터 리더가 있는 파티로 편성되지 않아 개인 판단으로 활동하는 인류. 리더를 잃은 기존 파티원과는 소속 상태를 구분한다.

## **1-2. 파티 성립과 지휘 상실.**

• 리더가 있는 인류 파티는 던전 입장 전 편성 단계에서 초기 리더를 정한다. 살아 있고 개인 도주가 확정되지 않은 구성원 중 **기본 통솔력 10 이상**인 유닛을 비교한다. 기본 통솔력 → 유닛 등급 → 같은 비교 동안 유지하는 무작위 순서로 선정한다. 던전 입장 시 다시 선정하지 않으며, 통솔력이 높은 충원자가 들어와도 자동 교체하지 않는다.

• 리더가 전파 범위 밖에 있어도 파티는 유지된다. 거리 때문에 리더를 재선정하거나 파티를 해체하지 않는다.

• 일반 리더가 사망하면 실제 사망 시점부터 10초 뒤 후임을 선정한다. 개인 도주가 확정된 경우에는 소속 생존 파티원이 그 사실을 처음 확인한 시점부터 10초 뒤 선정한다. 후임 자격과 동점 처리는 초기 리더 선정 기준을 따른다. 리더를 잃어도 파티 소속과 임무는 유지한다. 집결·공동 이동·자동 보호 포메이션과 해당 리더의 파티 후퇴 명령은 종료하며, 후임이 정해지기 전에는 각자 목표·이동·문 개방을 판단한다. 후임이 없어도 파티 소속을 해체하지 않는다.

리더 자격과 후임 선정의 세부 조건은 리더 명세서 2항, 지휘 상실과 개인 행동은 17항을 따른다.[\[1\]](#bookmark=id.yvjy7zlm3a1h)

• 독립 유닛끼리도 허용된 사실 정보를 교환한다. 정보 교환 자체로 집결·공동 진입·보호 포메이션을 새로 만들지 않는다.

편성 시 관리할 항목은 다음과 같다.

| 항목 | 내용 |
| ----- | ----- |
| **구성** | 컨셉, 임무, 단계, 총인원, 유닛 종류·등급·역할 |
| **지휘** | 리더 후보·기본 통솔력, 보유 명령·버프, 상위 소속 |
| **행동** | 임무별 완료 조건, 개인 활동 우선순위, 공동 이동과 진형 |
| **콘텐츠 입력** | 웨이브 등장 조건·가중치, 역할별 비중, 특수 유닛과 장비 |

# **2\. 초기 인류와 후속 편성.**

**기획의도** : 초기 독립 유닛에서 조직적인 파티와 정예 편성으로 위협을 확장한다.

## **2-1. 초기 독립 유닛.**

• 최초 인류는 도망자·갈 곳 없는 사람들 같은 부랑자 컨셉이다.

• 초기 독립 유닛은 인류 거점이 없을 때만 등장한다. 기존 파티원이 리더를 잃어 개인 행동으로 전환한 경우에도 원래 파티 소속과 임무는 유지한다.

• 정보 교환은 가능하지만 집결하지 않는다. 문 개방·다음 방 진입·장애물 회수·파괴를 각자 판단한다.

## **2-2. 초기 파티 인원.**

아래 인원수는 리더를 포함한 초기 구성값이다.

| 컨셉 | 인원 | 지휘와 버프 |
| ----- | ----- | ----- |
| **모험가** | 2\~4명 | 리더 자격과 통솔력 조건 적용. 방어 버프는 기본 통솔력 20 이상 |
| **도적** | 4\~6명 | 리더 자격과 통솔력 조건 적용. 공격 버프는 기본 통솔력 20 이상 |

## **2-3. 컨셉과 단계에 따른 확장.**

• 군대·용병·도적 등은 많은 일반 유닛과 소수 엘리트를 함께 넣는 편성을 허용한다.

• 모험가·용사·특정 엘리트 적은 소수 정예 편성을 허용한다.  
같은 컨셉도 단계에 따라 인원과 유닛 수준이 달라질 수 있다.

• 모든 파티에 탱커·힐러·CC 담당을 필수로 넣지 않는다. 보유 역할과 스킬로 가능한 행동만 수행한다.

• 용사 파티의 강한 후반 버프와 통솔력 80·100 고유 스킬은 개별 콘텐츠에서 정한다. 초반 버프 값을 그대로 복사해 확정하지 않는다.

## **2-4. 임무와 컨셉의 관계.**

회수 파티·점령 파티는 임무로 구분한 이름이고, 도적·모험가·군대는 컨셉이다.  
구성 데이터에서 두 요소를 구분해, 군대 컨셉의 점령 임무처럼 조합할 수 있게 한다.

# **3\. 임무 선택과 수량 집계.**

**기획의도** : 귀환자가 남긴 기록과 진행 상황이 다음 인류의 목적에 반영되게 한다.

## **3-1. 임무 선택.**

1\. 귀환 기록·진행 상황으로 선택 가능한 임무 후보를 구성한다.

2\. 후보별 가중치로 임무를 선택한다. 특정 사건이 임무를 지정하면 그 지정 결과를 사용한다.

3\. 선택된 임무를 수행할 수 있는 컨셉과 구성 규칙으로 파티를 편성한다.

임무 선택은 인류 시스템이 수행한다. 플레이어가 인류 파티를 직접 편성하거나 명령하지는 않는다.  
후보 가중치·최소 기록 수·해금 조건·구체적인 등장 시점은 웨이브 콘텐츠에서 정한다.

## **3-2. 임무 수량의 의미.**

시스템 집계는 완료 결과를 제공하며 미발견 위치와 개인 지도를 함께 제공하지 않는다.

| 종류 | 집계 기준 |
| ----- | ----- |
| **타일 탐색** | 이번 진입 이후 새로 시야로 확인한 지형 타일. 같은 타일 중복 집계 금지 |
| **방 탐색** | 방 지형 전체의 확인 완료. 임무 발급 시 미완료 방 B와 추가 방 E로 요구 수 B＋E를 고정 |
| **몬스터 처치** | 실제 처치 사건을 중복 없이 합산. 종류별 처치 반영 배율은 후속 콘텐츠 값 |
| **회수** | 실제 회수 완료 사건을 사용. 조사 완료와 구분 |
| **코어 파괴** | 실제 파괴 완료. 조사 완료와 구분 |
| **점령** | 예정된 해당 시설의 완공 등 실제 점령 완료 조건 |

• 방 탐색의 B에는 이미 알려진 보스방을 넣지 않는다. 진행 중 새 방을 발견해도 발급된 요구 수를 늘리지 않는다.

• 여러 파티가 공통 임무를 수행하면 같은 완료 사건을 한 번만 합산한다. 마지막 타격을 한 파티에 별도 공통 임무 보너스를 붙이지 않는다.

• 리더에게 임무 수량 달성 여부와 방 지형 확인 완료 여부를 제공한다. 적·회수물·구성원 위치와 각 대원의 개인 지도는 자동 공유하지 않는다.

• 방 지형 확인 완료는 숨은 적·함정 부재나 방의 안전을 보장하지 않는다.

# **4\. 임무별 평상시 행동과 집결 판단.**

**기획의도** : 목표가 다른 파티가 같은 방에서도 서로 다른 활동을 하도록 한다.

평상시 행동은 현재 임무와 컨셉에 따른 개인 활동이다. 전투가 끝나면 필수 5초 경계 후 아래 기준을 판단한다.

| 임무 | 현재 방에서 확인할 종료 조건 |
| :---: | ----- |
| **탐색** | 현재 방의 지형 확인 완료. 타일 수·방 수는 시스템 집계 |
| **회수** | 리더가 아는 현재 방의 수행 가능한 회수 대상 처리 |
| **소탕** | 다음 문까지 확인하며 알려진 적과 남은 교전 처리 |
| **코어 파괴** | 리더 조사 후 공격 가능한 인원이 공격하여 실제 파괴 완료 |
| **점령** | 해당 방에 예정된 시설 완공과 남은 전투 처리 |
| **보스** | 지정 보스 임무 완료와 남은 교전 처리 |

## **4-1. 탐색과 회수.**

• 탐색은 실제 시야 확인 범위가 모여 방 지형 확인 완료를 충족했는지 판단한다. 미확인 타일을 시스템이 개인 지도에 채워 넣지 않는다.

• 회수는 리더가 알고 있는 현재 방의 수행 가능한 회수 대상을 처리했는지 확인한다. 알려지지 않은 모든 시체·오브젝트를 찾아내야 집결하는 규칙이 아니다.

• 회수량 집계만을 위해 모든 회수 대상을 리더에게 보고하러 이동하지 않는다. 일반 사실 전파로 확보한 정보는 판단에 사용할 수 있다.

## **4-2. 소탕.**

소탕 파티는 리더가 선택한 다음 문으로 이동하며, 직접 확인한 적과 전달받은 교전에 대응한다. 문을 모르면 시야를 넓혀 찾는다.  
대응할 교전이 없고 다음 문으로 진행할 조건을 갖추면 집결할 수 있다. 방 전체의 미발견 적까지 전멸시킬 필요는 없다.

## **4-3. 코어와 보스.**

코어 파괴는 별도 임무가 될 수 있다.  
다른 임무 중에도 리더가 미처리 코어를 알게 되면 코어 처리를 우선한다.  
보스 판단에는 실제 보스와 방을 확인한 정보를 사용한다.  
열린 문 너머에서 발견한 것만으로 그 방에 강제 진입하지 않는다.

개별 보스 임무의 편성·운반·전투 단계·보상은 후속 콘텐츠에서 정한다.  
현재는 보스 임무의 완료 결과와 남은 교전 여부를 집결 판단에 연결한다.

# **5\. 전체 활동 흐름과 임무 수량 달성.**

**기획의도** : 목표 수량을 달성한 뒤에도 집결과 추가 활동이 반복 초기화되지 않게 한다.

![P01 파티 활동과 다음 방 이동][image1]

순서도 P01  파티 활동과 다음 방 이동   [**\[원본 편집\]**](https://app.diagrams.net/?mode=google&lang=ko&page-id=P01#G1H1IDkGON3O4xQUOd4Y5OoAtQWoGgDd26)

## **5-1. 최초 수량 달성과 추가 활동.**

• 전체 임무 수량을 처음 달성하면 집결 판단에 반영한다. 이후 처치·회수 수가 하나 증가할 때마다 같은 집결을 새로 발령하지 않는다.

• 집결을 마치면 일반 회수·조사·탐색을 위해 다시 흩어지지 않는다. 다음 문을 모르면 진형을 유지해 문을 찾는다.

• 새 방에서는 별도 탐색 시작 명령 없이 남은 전투·유효 임무와 개인 탐색을 판단한다. 수량 달성 상태만으로 즉시 재집결하지 않는다.

• 임무 수량만 달성했다고 즉시 외부 귀환하지 않는다. 알려진 일반 방이 없고 보스방만 남은 상황의 정상 귀환 판단은 기존 임무 규칙을 따른다.

리더가 미처리 코어를 확인하면 진행 중이거나 완료된 집결·다음 방 이동을 해제한다.  
코어를 처리한 뒤에는 해제 전 활동에 따라 재집결하거나 기존 파티 활동으로 돌아간다.

# **6\. 보고 이동과 대기 행동.**

**기획의도** : 진행에 필요한 정보는 리더에게 전달하되, 단순 집계와 장애물 처리 때문에 보고 이동이 남발되지 않게 한다.

## **6-1. 보고 이동의 대상.**

• 리더가 다음 이동을 결정하는 데 필요한 새 문과 코어 발견 정보를 보고한다.

• 모든 시체·회수물·오브젝트를 발견할 때마다 보고하러 이동하지 않는다. 임무 수량은 시스템이 집계한다.

• 길을 막는 시체·파괴 가능한 건설물은 개인이 처리한다. 보고 이동을 먼저 요구하지 않는다.

## **6-2. 리더가 전파 범위 밖인 경우.**

1\. 시스템이 파악한 **현재 리더 위치**를 보고 이동의 기준으로 사용한다.

2\. 리더가 이동하면 목적지를 갱신한다. 리더가 교체되면 유효한 새 리더 위치를 기준으로 한다.

3\. 자신의 지도·점유 크기·문과 경로 조건으로 이동한다. 리더 위치 제공으로 미확인 지형·적·함정까지 알게 하지 않는다.

4\. 전파 조건을 충족하면 보고한다. 리더가 이미 해당 정보를 받았음을 확인한 중복 보고자는 보고 이동을 종료한다.

## **6-3. 보고 도중 명령과 전투.**

일반 보고 이동 중 집결 명령을 받으면 기록을 보존하고 집결하면서 전달할 기회를 사용한다.  
코어 보고는 집결만으로 중단하지 않는다. 적이나 교전을 확인하면 기존 행동 전환·전투 우선순위를 적용한다.

## **6-4. 당장 수행할 활동이 없는 대원.**

• 먼저 임무·탐색·필요한 문 탐색 등 수행할 활동이 남았는지 확인한다.

• 활동이 없고 집결 명령도 없으면 현재 리더 위치를 기준으로 자신의 역할에 맞는 유효 위치를 잡는다. 이동 중에도 리더 위치를 갱신한다.

• 문을 발견했다는 이유만으로 모든 대원을 문 앞에 고정 대기시키지 않는다. 집결 명령이 있으면 문 주변의 지정 진형 위치로 이동한다.

• 리더에게 가는 경로를 아직 모르면 목적지 방향으로 시야를 넓혀 경로를 갱신한다. 수행 가능한 경로와 활동이 모두 없을 때는 기존 이동 규칙에 따라 대기한다.

# **7\. 집결 발령과 대기 기한.**

**기획의도** : 현재 행동의 완료와 늦는 대원의 합류를 처리하면서 집결 기한을 명확히 한다.

**7-순서도1. 집결 대기와 미집결자 수색   [\[원본 편집\]](https://app.diagrams.net/?mode=google&lang=ko&page-id=P02#G1H1IDkGON3O4xQUOd4Y5OoAtQWoGgDd26)**

![P02 집결 대기와 미집결자 수색][image2]

## **7-1. 집결 수신자의 행동.**

• 새 일반 조사·회수를 시작하지 않는다. 이미 진행 중인 상호작용은 기존 유지·중단 조건을 처리한 뒤 합류한다.

• 비전투 보호 유닛은 보호 포메이션을 끝내고 집결한다. 코어 보고는 별도 예외로 유지한다.

• 집결 경로를 실제로 막는 적·함정·문·시체·건설물은 기존 통과 대응을 한다. 다른 개인 목표를 처리하려고 이탈하지 않는다.

## **7-2. 대기 종료 시각.**

기본 대기 종료 시각은 발령 시각＋**8초**다.  
유효한 도착 예정 정보를 받으면 ‘정보 산출 시각＋예상 잔여 이동시간＋**3초**’와 비교해 늦은 시각을 사용한다.  
아직 합류하지 않은 대원이 여러 명이면, 전달받은 도착 예정 시각 중 가장 늦은 시각을 사용한다.

같은 정보를 다시 받거나 매 프레임 재판단해도 기한을 연장하지 않는다. 예상 잔여 이동시간은 그 값을 계산한 시각과 함께 사용한다.  
전원이 먼저 합류하면 대기 기한 전이라도 집결을 완료한다.

# **8\. 미집결자 수색과 발견 결과.**

**기획의도** : 부재를 곧바로 사망으로 판단하지 않고 제한된 수색 뒤 원래 진행을 이어 가게 한다.

## **8-1. 수색 위치와 방문 순서.**

1\. 유효 기한이 지나도 미집결자가 남으면 마지막 유효 확인 위치를 확인한다. 위치 정보가 없으면 미확인 상태로 이번 대기에서 제외한다.

2\. 여러 위치가 있으면 알려진 경로 기준 도착 예상시간이 짧은 위치를 우선한다. 동률이면 실제 확인 시각이 최근인 위치를 선택한다.

3\. 모인 인원이 해당 위치를 관찰할 수 있는 유효 지점으로 함께 이동한다. 시체 점유 타일을 정지 목적지로 잡지 않는다.

4\. 도착 후 전방·후방·좌우를 나눠 **5초간** 수색한다. 접근과 재집결 이동시간은 이 5초에서 제외한다.

수색은 실제 시야·인지·방 진입 권한으로 수행한다.  
새로운 유효 정보가 없으면 같은 위치를 다시 찾지 않는다.  
같은 장소의 미집결 기록이 여러 개여도 수색을 반복하지 않는다.

## **8-2. 발견 결과.**

찾던 인원인지와 사망 원인 확인 여부를 구분한다.

| 확인 결과 | 처리 |
| ----- | ----- |
| **찾던 생존자** | 위치 수색 종료. 기존 합류·진행 중 행동·전투·보호 규칙 적용 |
| **찾던 시체이고 원인 불명** | 사망 확인·전파·정신력 처리 후 대기 대상 제외.발견 시점부터 별도 원인 수색 **5초** 시작 |
| **찾던 시체이고 원인 확인** | 대기 대상 제외. 별도 원인 미상 수색 없이 확인한 위협에 기존 대응 |
| **다른 시체** | 그 사망 사건만 처리. 원래 미집결자의 수색 중심·방향·종료 시각 유지 |
| **5초 동안 미발견** | 미확인 유지. 이번 대기에서 제외하고 다음 유효 수색 위치 또는 공동 진행 판단 |

## **8-3. 종료와 중단.**

• 수색 뒤에 공통 **3초** 대기를 추가하지 않는다. 수색 **3초**째 찾던 시체를 발견했으나 원인을 모르면, 그때부터 원인 수색 **5초**를 시작한다. 중단 사건이 없으면 총 **8초**다.

• 집결 명령이 유지되면 수색 시작 위치 주변의 유효 진형 자리에 다시 모인다. 명령이 끝났으면 유효한 개인 행동으로 복귀한다.

• 전투·후퇴·개인 도주는 우선 처리한다. 후퇴 조건을 충족하면 수색 때문에 지연시키지 않는다.

• 수색 중 잠시 흩어진 참여자를 그 이유만으로 새 미집결자로 분류해 수색을 반복하지 않는다.

# **9\. 공동 이동과 문 주변 배치.**

**기획의도** : 대형 유닛·느린 유닛도 실제 공간 조건에 맞게 함께 이동하도록 한다.

## **9-1. 공동 속도와 개인 속도.**

• 집결 후 공동 이동은 실제 참여하는 생존 인원의 유효 이동속도 중 가장 느린 값을 기준으로 한다. 전투·회피·강제 이동은 해당 행동의 속도를 따른다.

• 늦게 합류하는 유닛과 보고 이동·개인 탐색은 개인 적용 이동속도를 사용한다. 합류 뒤 공동 이동 상태가 되면 공동 속도를 적용한다.

• 단순 정지나 상호작용 중이라는 이유로 이동 능력치 자체를 0으로 취급하지 않는다. 합류 불가는 미집결 규칙으로 처리한다.

## **9-2. 진형과 통과 순서.**

공동 이동을 시작할 때부터 리더도 자신의 전투 역할에 맞는 위치를 사용한다. 리더라는 이유로 중앙에 고정하지 않는다. 필요한 역할이 없는 자리에 다른 대원을 역할 대체 유닛으로 강제 배정하지 않는다.

좁은 통로에서는 다음 역할 순서를 적용한다.

| 순서 | 역할 |
| :---: | ----- |
| **1** | 근접 탱커와 근접 공격 |
| **2** | 근접 지원 |
| **3** | 원거리 공격 |
| **4** | 원거리 지원 |

## **9-3. 문 앞 대기.**

• 문 앞 2칸을 비우고 그 뒤의 유효한 진형 위치에 대기한다. 대형 유닛은 점유 타일 전체로 확인한다.

• 정해진 자리가 벽·다른 유닛·점유 크기로 불가능하면 가장 가까운 유효 자리를 사용한다. 다른 유닛과 겹치거나 문 앞 금지 공간을 침범하지 않는다.

• 문 발견·문 개방·다음 방 진입은 별도 단계다. 파티 대원은 유효한 리더의 방 이동 결정과 실제 문 통과 조건을 따른다.

• 개인 경로는 달라도 리더가 정한 공용 방 연결을 따른다. 다른 방을 경유하는 우회는 새 방 경로 결정이 필요하다.

## **9-4. 집결 이후.**

집결 뒤 새 일반 비전투 대상을 발견하면 기록·전파하고 공동 이동을 유지한다.  
다음 문을 모르면 진형을 유지한 채 찾는다. 새 방에 들어가면 개인 탐색과 임무 행동으로 전환한다.

## **9-5. 일반 방 휴식 연결.**

집결 뒤에는 후퇴 조건을 먼저 확인한다. 계속 진행할 수 있으면 다음 방 이동 전에 휴식 가능 여부를 판단한다.  
같은 방의 일반 휴식은 한 번이다. 반응 가능한 소리로 중단한 남은 휴식은 재개하지 않는다. 소리 확인이나 함정 처리만으로 새 휴식 기회를 만들지 않는다.

휴식할 수 없거나 기회를 이미 사용했으면 이동한다.  
일반 휴식의 시작·회복·최대 시간은 별도 휴식 문서에서 정한다. 치료 스킬은 전투 중에만 사용한다.  
거점 휴식은 [15](#bookmark=id.gaa1147a2e26)·[16](#bookmark=id.ga9d62778cd8)항의 별도 절차를 따른다.

# **10\. 상호작용 보호 포메이션.**

**기획의도** : 조사나 해제를 시작한 대원을 주변 파티원이 지키는 행동을 정보 전달과 연결한다.

![P03 상호작용 보호 포메이션 참여][image3]

순서도 P03  상호작용 보호 포메이션 참여   [**\[원본 편집\]**](https://app.diagrams.net/?mode=google&lang=ko&page-id=P03#G1H1IDkGON3O4xQUOd4Y5OoAtQWoGgDd26)

## **10-1. 참여 조건.**

• 지휘를 유지하는 파티가 존재해야 한다. 리더가 근처에 있을 필요는 없고 통솔력 40 조건도 두지 않는다. 리더가 없는 동안에는 자동 보호 포메이션을 새로 만들지 않는다.

• 조사 또는 함정 해제를 실제 시작한 유닛의 **최초 전파를 직접 받은 같은 파티 유닛**이다.

• 현재 참여 가능한 일반 탐색 상태이며 집결·방 이동 명령, 전투·도주·후퇴, 다른 상호작용·보호 포메이션 등 유지할 행동이 없다.

직접 발견이나 재전파 수신만으로는 보호에 참여하지 않는다.  
함정 담당자를 정하는 **2초** 응답 대기 중에는 보호 포메이션을 만들지 않는다.  
실제 해제가 시작될 때 최초 전파를 보낸다.

## **10-2. 중복 참여.**

상호작용마다 별도의 보호 포메이션을 둘 수 있다. 한 유닛은 하나에 참여하며 기존 보호를 유지한다.  
새로 참가 자격을 갖춘 유닛이 합류하면 유효한 배치를 다시 계산할 수 있다.

# **11\. 보호 배치와 파티 목표 합류.**

**기획의도** : 일반 보호와 임무 목표 합류를 구분해 같은 정보를 받아도 필요한 행동만 수행하게 한다.

## **11-1. 보호 위치와 감시.**

• 상호작용자가 오브젝트를 바라보는 방향을 전방으로 한다. 근접 역할은 전방 → 좌우 → 가장 가까운 이동 가능 타일 순서로 배치한다.

• 원거리 역할은 후방 2칸 이상 → 좌우 후방 → 가장 가까운 비점유 타일 순서다. 공간이 허용하면 최소 2칸 거리를 유지한다.

• 전방·후방·좌우 감시를 나눈다.  
정면 시야선의 **50% 이상**이 벽에 막히면 인접한 유효 방향으로 재배정한다.  
여러 방향이 막혔으면 감시 인원이 적은 유효 방향을 우선한다.  
인원별 세부 방향은 탐색반응 문서 [6-6](https://docs.google.com/document/d/1Aoe9PcHfhSxK5HzqPe5bNCPNZ4U8axPM/edit#bookmark=id.ga8ca07558ef)·[6-7항](https://docs.google.com/document/d/1Aoe9PcHfhSxK5HzqPe5bNCPNZ4U8axPM/edit#bookmark=id.gada807efb1b)을 따른다.

• 파티 목표에서는 리더를 상호작용자 옆에 우선 배치한다. 불가능하면 좌우 후방, 그마저 불가능하면 가장 가까운 비점유 유효 위치를 사용한다.

• 리더와 상호작용자는 같은 정면 방향을 본다. 리더가 직접 코어를 조사하면 리더가 기준 유닛이며 자기 옆 자리를 또 만들지 않는다.

## **11-2. 유지와 종료.**

• 상호작용 완료·취소를 알게 되면 해당 보호를 종료한다. 리더 사망·개인 도주 확정 시에도 자동 보호 포메이션을 종료한다. 후임이 선정되면 현재 상호작용과 참가 조건으로 새로 판단하며 이전 포메이션을 자동 복원하지 않는다.

• 집결을 받은 비전투 보호자는 집결한다. 상호작용 당사자는 진행 중 행동의 기존 유지·중단 조건을 적용한다.

• 소리에 반응할 수 있는 보호자 중 실제로 소리를 감지한 유닛만 반응한다. 한 명의 이탈만으로 전원을 자동 이탈시키지 않는다.

• 상호작용자 본인이 피격되면 중단한다. 보호자만 피격된 경우 유지 우선 임무로 정한 상호작용은 유지할 수 있다.

• 상호작용이 중단됐고 공격 방향만 알면 그 방향을 수색한다. 방향도 모르면 기존 보호 위치별로 방향을 나눠 미식별 공격 수색을 **5초**간 수행한다.

## **11-3. 일반 파티 목표의 합류 전파.**

1\. 임무 대상에 도달한 유닛은 합류 정보를 즉시 전파하고 기다리지 않고 상호작용을 시작한다.

2\. 합류 가능한 같은 파티 수신자는 유닛당 한 번 재전파한 뒤 이동한다. 현재 행동을 유지해야 하면 즉시 합류용 재전파를 하지 않고 그 행동을 계속한다.

3\. 전투·도주·후퇴·다른 상호작용·보호 때문에 합류하지 못했으면 그 행동이 끝난 뒤 판단한다. 전투 후에는 5초 경계를 먼저 마친다.

4\. 집결·방 이동 명령이 없고 직접 확인 또는 허용된 정보 갱신으로 임무 대상이 유효함을 확인했을 때만 합류한다.

일반 사실의 진행 중·완료·취소 정보는 전달할 수 있다.  
그 재전파만으로 일반 보호 포메이션의 참가 자격을 얻지는 않는다.

# **12\. 코어 보고와 조사 및 파괴.**

**기획의도** : 코어 발견이 방 이동을 중단시키는 공동 목표가 되도록 하고, 조사와 실제 파괴를 구분한다.

![P04 코어 보고와 조사 및 파괴][image4]

순서도 P04  코어 보고와 조사 및 파괴   [**\[원본 편집\]**](https://app.diagrams.net/?mode=google&lang=ko&page-id=P04#G1H1IDkGON3O4xQUOd4Y5OoAtQWoGgDd26)

## **12-1. 발견과 보고.**

• 일반 대원은 코어를 직접 조사하지 않고 리더에게 보고한다. 전파 범위 밖이면 현재 리더 위치로 이동한다.

• 집결만으로 코어 보고 이동을 중단하지 않는다. 리더가 이미 정보를 받았음을 확인하면 중복 보고 이동을 끝낸다.

• 리더가 미처리 코어를 직접 확인하거나 보고받으면 진행 중이거나 완료된 집결·다음 방 이동을 해제한다.

• 코어 처리를 위한 집결 해제는 같은 방·같은 파티 전체에 해당 코어 위치와 함께 전달한다. 개인 지도 전체, 다른 적의 위치와 다른 발견 내용까지 자동으로 전달하지 않는다.

진행 중 상호작용은 기존 유지·중단 조건을 처리한 뒤 코어로 합류한다. 발견자는 보고 후 리더와 함께 코어로 돌아간다.  
전투 대응·상태 악화 후퇴·확정된 도주는 코어 합류보다 우선한다.

# **13\. 코어 처리와 리더 사망.**

**기획의도** : 리더와 완료 정보가 없는 유닛이 코어 절차를 자동으로 이어받지 않게 한다.

## **13-1. 정상 처리.**

1\. 리더가 유효한 상호작용 위치에서 코어를 조사한다. 리더가 일시적으로 행동할 수 없으면 기존 중단·재개 조건을 확인한다.

2\. 조사가 완료되면 후속 공격 가능 상태와 필요한 정보를 기록한다. 공격 가능한 대원은 코어를 공격한다.

3\. 전투로 중단되면 전투 대응과 필수 5초 경계 뒤 남은 공격을 재개한다. 이미 완료한 조사는 반복하지 않는다.

4\. 실제 파괴가 완료되면 코어 때문에 해제한 집결은 다시 발령한다. 탐색 중 발견한 경우 기존 파티 활동으로 복귀한다.

코어 조사 완료·실제 파괴·층 개방은 서로 다른 사건이다.  
코어의 통제권·시설·수복·활성화는 코어·점령 문서의 완료 조건을 따른다.

## **13-2. 초기 독립 유닛.**

초기 리더 없는 독립 유닛은 코어를 발견하면 기록만 한다. 리더 조사와 파티의 공동 코어 공격을 자동으로 시작하지 않는다.

## **13-3. 코어 처리 중 리더 사망.**

• 일반 리더가 사망하면 실제 사망 시점부터 10초 뒤 후임을 선정한다. 개인 도주가 확정된 경우에는 소속 생존 파티원이 그 사실을 처음 확인한 시점부터 10초 뒤 선정한다. 후임 자격과 동점 처리는 초기 리더 선정 기준을 따른다. 후임은 남아 있는 코어 상태와 자신에게 허용된 조사 정보를 확인한 뒤 새 행동을 판단한다.

• 리더가 사망하거나 개인 도주가 확정되면 그 리더의 미완료 코어 조사, 집결·공동 이동·자동 보호 포메이션을 종료한다. 보고 내용은 보존하며 후임 선정 후 현재 리더와 보고 필요를 다시 판단한다.

• 이미 조사 완료 정보를 확보한 유닛은 수행 가능한 개인 공격으로 코어를 공격할 수 있다. 정보를 받지 않은 유닛에게 조사 완료 상태를 자동 제공하지 않는다.

• 살아 있는 상위 지휘관의 유효 명령은 별도로 판단한다. 명령 수신만으로 조사 권한이 없는 일반 대원에게 코어 조사 권한을 새로 주지 않는다.

## **13-4. 최종 단계 연결.**

점령 파티, 코어 장치 운반·설치 담당, 최종 침공의 점령 잔류 제한은  
코어·점령 및 플레이 루프의 해당 단계 규칙을 따른다.  
일반 파티 규칙에서 새 운반 담당이나 특수 도구 작업을 임의로 추가하지 않는다.

# **14\. 전투 후 행동과 통로 확보.**

**기획의도** : 개인 전투 대응과 공동 진행을 연결하고, 제거 가능한 통로 차단물 앞에서 보고 때문에 멈추지 않게 한다.

## **14-1. 전투 종료 뒤.**

• 일반 탐색·임무 수행·정상 귀환 중 전투가 끝나면 시야 방향을 돌려 **5초** 경계한다.  
새 적을 확인하면 즉시 전투를 판단한다.

• 경계 뒤 현재 유효 명령과 파티별 활동 종료 기준을 확인한다. 남은 임무·탐색이 있으면 수행하고 필요한 경우 리더가 집결한다.

• 이미 집결·공동 이동·보고 중이었다면 경계 뒤 유효한 해당 행동을 재개한다. 리더 사망으로 종료된 명령을 자동 복원하지 않는다.

## **14-2. 정상 귀환과 후퇴.**

• 정상 귀환 중 새 비전투 대상은 기록하되 우회해 조사하지 않는다. 소리만 감지하면 이동 중 방향만 확인하고 추적·정지 대기로 바꾸지 않는다.

• 정상 귀환 중 적을 직접 확인해 교전하면 전투 후 5초 경계 뒤 귀환을 재개한다.

• 상태 악화 후퇴와 확정된 개인 도주는 탈출을 우선한다. 통과를 실제로 막는 전투에는 대응하되, 지나갈 수 있으면 계속 이동한다. 적 전멸이나 일반 다음 방 집결을 기다리지 않는다.

## **14-3. 시체와 건설물.**

제거할 수 있는 통로 차단물은 개인이 처리하며, 전투 여부에 따라 시체의 처리 방식이 달라진다.

| 상황 | 기본 처리 |
| ----- | ----- |
| **비전투 중 길막 시체** | 회수해 통로 확보 |
| **전투 중 길막 시체** | 파괴해 통로 확보 |
| **통행을 막는 파괴 가능한 건설물** | 개인이 파괴. 리더에게 보고하러 가지 않음 |
| **이동 불가지만 투사체 통과 가능** | 원거리 유닛이 사거리·시야 조건을 충족하면 그 자리에서 적 공격 |
| **리더가 전투 중 차단물 확인** | 필요하면 해당 오브젝트를 공동 파괴 대상으로 지정 |

통로 확보는 회수·파괴할 수 있도록 설정한 시체와 차단물에 적용한다. 파괴 불가능한 구조 벽까지 공격 대상으로 삼지 않는다.  
유닛 크기와 점유 충돌은 기존 경로 규칙을 적용한다.

# **15\. 후퇴와 거점 휴식.**

**기획의도** : 후퇴하는 파티를 다시 저지할 기회를 만들고, 일반 파티의 끝없는 회복 반복을 제한한다.

![P05 후퇴와 거점 휴식 후 행동][image5]

순서도 P05  후퇴와 거점 휴식 후 행동   [**\[원본 편집\]**](https://app.diagrams.net/?mode=google&lang=ko&page-id=P05#G1H1IDkGON3O4xQUOd4Y5OoAtQWoGgDd26)

평균 대상은 현재 집결했거나 함께 행동하는 생존 인원이다. 개인별 비율을 같은 비중으로 평균한다.  
미집결자·사망자·개인 도주 확정자는 제외한다. 전투 배치를 위해 잠시 흩어진 것은 파티 이탈로 취급하지 않는다.

| 구분 | 평균 HP | 평균 정신력 비율 | 판정 |
| ----- | ----- | ----- | ----- |
| **일반 파티 후퇴** | 20% 이하 | 15% 이하 | 둘 중 하나 충족 |
| **점령 파티 후퇴** | 40% 이하 | 30% 이하 | 둘 중 하나 충족 |
| **거점 회복 완료** | 60% 이상 | 40% 이상 | 둘 다 충족 |

정신력 비율은 (현재 정신력＋100)÷2다. 평균에서 제외됐다는 이유로 해당 인원의 사망·도주·위치를 리더에게 자동 전달하지 않는다.

# **16\. 휴식 이후의 분기와 리더 상실.**

**기획의도** : 현재 후퇴의 주체와 개인 행동의 전환을 분명히 하고, 리더 없는 공동 휴식과 재출격이 남지 않게 한다.

## **16-1. 일반 파티의 거점 휴식.**

• 현재 사용할 수 있는 기존 점령 거점에서 첫 거점 휴식을 수행한다. 거점이 없거나 사용할 수 없으면 출구로 후퇴한다.

• 거점에 도착·집결한 생존 인원 중 개인 도주 미확정자가 휴식 대상이다. 전투 중이면 현장 전투에 참여하고 쉴 수 있게 된 뒤 회복한다.

• 휴식은 평균 HP 60%와 정신력 비율 40%를 모두 충족하면 끝낸다. 개인별 최소 회복 조건은 없다.

• 첫 휴식 종료 때 임무를 달성했으면 출구로 귀환한다. 미달성이면 재탐험하며 이후 수량 달성만으로 즉시 귀환하지 않는다.

• 재탐험 중 다시 후퇴 조건을 충족하면 거점에 다시 쉬지 않고 출구로 간다. 미완료 휴식의 전투 중단·재개는 새 완료 횟수로 세지 않는다.

## **16-2. 휴식 중 처리.**

거점 휴식 중 소리만 감지하면 방향만 확인하고 회복을 유지한다.  
HP는 재생력 ×2 × 실제 휴식 시간(초), 정신력은 원수치 \+1/초로 회복한다. 탐험 중 HP 회복량을 중복 가산하지 않는다.  
일반 방 휴식의 최대 시간과 거점 회복 완료 조건은 구분한다.

## **16-3. 리더 상실과 후임 선정 전 행동.**

1\. 사망한 리더의 모든 발령 명령을 종료한다. 이미 진행 중인 파티 후퇴도 포함한다.

2\. 일반 리더가 사망하면 실제 사망 시점부터 10초 뒤 후임을 선정한다. 개인 도주가 확정된 경우에는 소속 생존 파티원이 그 사실을 처음 확인한 시점부터 10초 뒤 선정한다. 후임 자격과 동점 처리는 초기 리더 선정 기준을 따른다. 후임이 없으면 소속과 임무를 유지한 채 개인 행동을 계속한다.

3\. 리더가 없는 동안에도 소속과 임무는 유지하고 각자 활동한다. 집결·공동 이동·자동 보호 포메이션을 유지하거나 새로 만들지 않는다.

4\. 확정된 개인 도주가 없고 개인 HP가 20% 이하이면 출구로 개인 후퇴한다. 거점 휴식·재탐험·재입장은 없다.

정신력 비율만으로 리더 없는 유닛의 개인 후퇴를 시작하지 않는다. 개인 도주는 별도의 정신력 규칙을 따른다.  
한 번 시작한 개인 후퇴는 HP가 **20%**를 초과해 회복돼도 취소하지 않는다.

리더를 잃어도 개인 도주 확률·침공별 조우·이미 완료한 휴식 이력은 초기화하지 않는다.  
살아 있는 상위 지휘관의 유효한 명령은 발령자가 다르므로 유지한다.

## **16-4. 동료의 개인 도주 확인.**

같은 파티원의 도주를 처음 직접 확인하거나 유효한 보고로 알면, 기본 정신력 감소량 5에 일반 감소 보정을 적용한다.  
같은 도주 사건은 유닛마다 한 번만 적용한다. 재보고·회복·방 이동으로 초기화하지 않는다.  
부재·추정이나 평균 계산에서의 제외만으로 도주 사실을 자동 전달하지 않는다.

# **17\. 점령 파티와 충원 연결.**

**기획의도** : 점령 거점을 다음 진출의 기반으로 삼고, 플레이어가 회복·건설 중인 인류를 공격할 수 있게 한다.

## **17-1. 점령 활동.**

• 통과하는 방에는 예정된 기초 시설을 먼저 완공한다. 방어 거점으로 정한 방에서는 예정된 방어 시설까지 완공한다.

• 진출 후보는 지형·접근 경로가 기록되고 전달받은 확인 결과상 남은 적대 유닛·방어 건물·야생 생성 거점이 없다고 판단한 방이다. 미발견 적·함정의 부재를 보장하지 않는다.

• 점령 파티는 목표 완료만으로 귀환하지 않는다. 거점으로 후퇴해 회복하고 조건이 맞으면 다시 출격한다.

## **17-2. 후퇴와 재출격.**

회복·인원·대상·건설 조건을 모두 충족해야 재출격한다.

| 항목 | 조건 |
| ----- | ----- |
| **후퇴 목적지** | 사용 가능한 방어 거점 중 가까운 곳. 없으면 기초 거점, 둘 다 없으면 출구 |
| **회복** | 휴식 대상의 평균 HP 60% 이상 및 정신력 비율 40% 이상 |
| **인원** | 최초 출격 인원 N0의 80% 이상인 현장 생존 개체 |
| **진출 대상** | 사용 가능한 기록과 경로 조건을 충족하는 방 |
| **건설** | 방어 거점으로 정한 방의 예정 방어 시설 완공 |

N0는 충원이나 손실 뒤에도 바꾸지 않는다. 대형 유닛도 한 명으로 센다.  
시체·외출자·미집결자·미도착 충원은 현장 인원에서 제외한다.  
예: N0가 **5명**이면 **4명 이상**, **6명**이면 **5명 이상**이어야 한다.

## **17-3. 후속 파티를 통한 충원.**

• 같은 집단의 후속 파티가 필요한 충원 인원을 함께 데려온다. 고정 시간마다 별도 충원 전용 파티를 만들지 않는다.

• 부족분은 거점 정원에서 기존 소속 생존 병력·복귀 예정 외출자·이미 출발한 충원 인구수를 빼서 계산한다. 시체는 인구수에 포함하지 않는다.

• 점령 파티가 다른 거점으로 후퇴하면 시스템이 충원 합류 목적지를 갱신한다. 직접 만남이나 보고를 기다리지 않는다. 적·함정·지도 정보까지 자동 공유하지는 않는다.

• 일반 임무 파티의 기존 인원은 거점을 방문했다고 자동 편입하지 않는다.  
거점 상한이 가득 차면 기존 부족분 충원만 한다.

유효한 기존 정보가 있으면 회복 뒤 조건을 다시 평가한다. 매번 새 후퇴 파티의 정보 전달을 기다리지 않는다.

# **18\. 공동 행동의 검증 사례.**

**기획의도** : 단순한 발견·집계·부재가 의도하지 않은 공동 행동을 만들지 않는지 확인한다.

아래는 설계의 기대 결과이며 실제 게임 실행 테스트 통과 기록은 아니다.

| 사례 | 입력 | 기대 결과 |
| :---: | ----- | ----- |
| **P01** | 거점 없음, 초기 부랑자, 리더 후보 없음 | 정보 교환 가능. 개인 문 개방·이동 |
| **P02** | 리더는 생존하지만 전파 범위 밖 | 파티 유지. 개인 보고는 현재 리더 위치 사용 |
| **P03** | 필요한 문 탐색이 남음 | 할 일 없는 대원으로 처리하지 않고 탐색 판단 |
| **P04** | 문 발견, 집결 명령 없음 | 문 앞 전원 고정 대기·임의 다음 방 진입 없음 |
| **P05** | 전투 종료, 현재 방 임무 미완료 | 5초 경계 뒤 임무 수행 |
| **P06** | 집결 완료, 후퇴 기준 충족 | 휴식·미집결자 추가 수색보다 후퇴 판단 우선 |
| **P07** | 수량 달성 상태로 새 방 진입 | 개인 활동 시작. 즉시 반복 집결 없음 |
| **P08** | 명령 0초, 전원 6초에 도착 | 6초 집결 완료 |
| **P09** | 리더가 전달받은 도착 예정 시각이 집결 명령 후 10초인 경우와 4초인 경우를 각각 확인한다. 대원은 기한까지 도착하지 않는다. | 10초 도착 예정: 명령 후 13초까지 대기.4초 도착 예정: 4＋3＝7초보다 기본 기한 8초가 늦으므로 명령 후 8초까지 대기. |
| **P10** | 같은 도착 예정 정보 재수신 | 기한을 새로 8초 또는 3초로 초기화하지 않음 |
| **P11** | 수색 위치가 둘, ETA 4초와 7초 | 4초 위치 우선. ETA 동률이면 최근 확인 위치 |
| **P12** | 3초째 찾던 시체, 원인 불명 | 그 시점부터 원인 수색 5초. 총 8초 |
| **P13** | 수색 중 다른 시체 발견 | 원래 미집결 수색 종료 시각 유지 |
| **P14** | 5초 수색 미발견 | 미확인 유지·이번 대기 제외. 추가 3초 없음 |
| **P15** | 3×3 유닛의 일부 점유가 문 앞 2칸 침범 | 그 대기 위치 불가. 유효 위치 재선정 |

# **19\. 보호와 지휘 상실 검증 사례.**

**기획의도** : 같은 상황을 문서별로 다르게 처리하지 않도록 예외를 대조한다.

보호·코어·후퇴의 주요 기대 결과는 다음과 같다.

| 사례 | 입력 | 기대 결과 |
| :---: | ----- | ----- |
| **P16** | 리더 멀리 있음, 보호 시작 최초 전파 직접 수신 | 다른 참가 조건 충족 시 보호 가능 |
| **P17** | 상호작용 직접 발견 또는 재전파만 수신 | 그 이유만으로 일반 보호 참가 불가 |
| **P18** | 함정 담당자 응답 대기 2초 | 아직 보호 포메이션 생성 안 함 |
| **P19** | 보호자 한 명만 소리 감지 | 해당 유닛만 허용된 반응. 전원 자동 해산 없음 |
| **P20** | 집결 중 리더가 미처리 코어 정보 수신 | 집결 해제와 해당 코어 위치를 같은 방·같은 파티 전체에 전달한다. 개인 지도와 다른 적 정보는 자동 전달하지 않는다. |
| **P21** | 조사 완료 뒤 코어 공격 중 전투 발생 | 전투·5초 경계 뒤 남은 공격. 조사 반복 없음 |
| **P22** | 길막 시체 발견, 비전투 / 전투 | 보고 이동 없이 회수 / 파괴 |
| **P23** | 파티 후퇴 중 리더 사망, 후임 없음, HP 25% | 기존 후퇴 종료·개인 행동. 거점 휴식 없음 |
| **P24** | 같은 상황에서 개인 HP 20% | 개인 후퇴로 출구 이동 |
| **P25** | 개인 후퇴 후 HP가 25%로 회복 | 출구 이동 유지 |
| **P26** | 후임 없음, HP 30%, 정신력 비율 15% | 정신력만으로 개인 후퇴 확정 안 함. 개인 도주 규칙 별도 |
| **P27** | 거점 회복 HP60%, 정신력39.9% | 휴식 유지. 둘 다 충족해야 종료 |
| **P28** | 점령 N0=6, 현장 생존 4명 / 5명 | 4명은 인원 조건 미달. 5명은 다른 조건도 확인 |
| **P29** | 초기 독립 유닛이 코어 발견 | 기록만. 리더 조사와 공동 파괴 자동 시작 없음 |
| **P30** | 리더 사망 뒤 완료 정보 보유 / 미보유 | 보유자는 개인 코어 공격 가능.미보유자에 자동 정보 제공 없음 |

# **20\. 후속 콘텐츠와 연결 문서.**

**기획의도** : 현재 공통 구조와 앞으로 채워야 할 인류 콘텐츠를 구분해 편성 기준이 빠지지 않게 한다.

## **20-1. 컨셉 확정 뒤 작성할 항목.**

• 어떤 컨셉이 어떤 기준으로 웨이브 후보가 되는지와 실제 선택 방식.

• 위험도·흥미도·시간·플레이어 진행 등 등장 조건의 연결, 단계별 인원·역할·등급 비중과 혼합 편성.

• 클래스별 실제 기본 통솔력, 보유 명령·연결 기술·기본 버프의 구체적인 콘텐츠 수치와 통솔력 80·100 고유 스킬. 초기 리더·상위 지휘관 편성, 자격과 승계 절차는 리더 명세서의 확정 규칙을 적용한다.

• 컨셉별 공격 대상 위협 배율, 탐색·회수·전투 우선순위와 예외. Excel과 DOCX로 별도 정리.

• 컨셉별 진형 모양·개별 자리·간격·활동 범위와 인원 증감 시 배치.

• 비활성·해제 함정 등의 회수 대상 여부, 회수·운반 완료 사건과 손실 처리.

• 여러 파티의 경로 배정과 합동 임무, 코어 장치 운반·설치 담당 편성, 후속 보스 임무의 실제 절차.

## **20-2. 담당 문서 연결.**

이 문서는 파티의 공동 행동을 정한다. 이동·인지·전파·후퇴·점령의 세부 계산과 수치는 해당 기준 문서를 함께 확인한다.

[01 파티 목표와 비전투 목표 선택 v0.12](https://docs.google.com/document/d/15b1fk34uoS3WAqh_aOy0e-rB8fYe3CvDVmNP4NS5-sQ/edit)

[03 행동 전환과 중단 및 재개 v0.12](https://docs.google.com/document/d/1y-hqZdDMzKe2Hzb6dentXGWo7pB96uxOYpWn6Kp4kXw/edit)

[04 이동 경로와 속도 및 점유 충돌 v0.12](https://docs.google.com/document/d/1v2Z5AsPoplE2SOJllUWIvmyOUc2l8U8lmplWid7CijA/edit)

[05 파티 이동과 집결 및 보고 v0.12](https://docs.google.com/document/d/1ah1hRzFYmsVrGNiKC8DYlV3tg9eztAbSjK6V1eqNMpk/edit)

[03 탐색반응 경계 조사 함정대응 시스템 v0.6](https://drive.google.com/file/d/1Aoe9PcHfhSxK5HzqPe5bNCPNZ4U8axPM/view)

[후퇴 도주 설계 명세서 수정안 v2](https://docs.google.com/document/d/1D1CzLmywYrdkWLHijLsANmKT_rWOzsO5udBDAKZUjUg/edit)

[코어 점령 설계 명세서 v0.3](https://drive.google.com/file/d/1l-seq4Mb5sE1z2YURKP2fD1PPMN_JXb7/view)

## **20-3. 후속 문서 검수 범위.**

리더 위치 제공, 자동 보호 참가 조건, 통솔력 단일화, 기본 버프의 거리 조건 삭제, 리더 상실 시 파티 후퇴 종료는 리더 명세서와 같은 기준을 사용한다. 표·순서도·검증 사례도 각 본문 조건을 따른다.

두 주제 문서의 피드백을 마친 뒤 타 문서별 변경안을 다시 확인한다.  
최종 변경안은 핵심·내용 변경 요약본과 전체 문장별 변경을 담은 AI 입력용 문서로 나눈다.

## **20-4. 특화 장비 기준의 후속 작성.**

특화 장비 착용 인원은 인류가 파악한 현재 던전 전체 위험도에 따라 늘어난다. 소탕·탐색·회수 모두 높은 위험도에서는 착용 조건을 충족한 전원까지 허용한다.

공격 속성과 스킬 디버프 종류를 정리한 문서 → 장비 문서 → 위험도별 특화 장비 착용 인원 데이터 표 순으로 작성한다. 위험도 구간별 인원과 세부 배분 기준은 장비 문서 작성 후 확정하며, 이 문서에서는 상세하게 다루지 않는다.

**주석**

[\[1\]](#bookmark=id.h242svmxxtov) 관련 규칙: [리더 명령 설계명세서 2·17항](https://docs.google.com/document/d/19Jw7HbsHvt-HQsPggp1OK72QPeAtH3jnB9OZID0IxJU/edit)

[image1]: <data:image/png;base64,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>

[image2]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAqAAAAHXCAIAAADskX92AAA/BUlEQVR4Xu3dh58URf7/8d/fcOrgksQlGRAlrIAgipizYgD0zIoJBRE9RUVRURREz4CigiAKEk4Mp54KmFBAkgEl5ygZ2VPxvs6v2JKytj7dPbO73TPTPa/n4/3wUV1d3TOLQ7+3Z3fZ/5cGAACJ8//cCQAAEH8UPAAACUTBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPAAACUTBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPAAACUTBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPAAACUTBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwSP2enTvRUh0cV9wQExQ8Ig3df0t//kXQiINNY84ouARY7Q7yVncFx9Q8Ch4xJi8ChMSUbiJR+xQ8IgxeRUmJKJQ8IgdCh4xJq/ChEQUCh6xQ8EjxuRVmJCIQsEjdih4xJi8ChMSUSh4xA4FjxiTV2FCIgoFj9ih4BFj8ipMSESh4BE7FDxiTF6FCYkoFDxih4JHjMmrcGLy4QdTpn/2ldlsV9bBjplP/a1EHuuXbBZ7rvnh+0X2eNuWHQGLnag1b7w+Qc7HLhQ8YoeCR4zJq3DsMmjgYFWBTspFwduxa1VWrCpgeTa5uKxlWxO/NZ6Tajz1o0/kfMAJKXggLyh4xJi8Csc3TrMGF7xnf+vogs9ysVljr/RcYMez4ANOSMEDeUHBI8bkVTim2b3rv6oIt2/daWb8Cv6Srpf9a+JksykrVha85/jC87ua+K3xnHQKXmXenG+DT0jBA3lBwSPG5FU4plEtuGv7brsXPQv+4i7d1JpVK9boZtVx1siCN1+5d+adAwPmnQM97+DV2P5SvT1PwQN5QcEjxuRVOI5RFXjaSWeowfBhL6nxrTffVi4K/t6771e76h54kDzWmZEFn3GsI798bi+2k2XBmxNS8EBeUPCIMXkVjl0GDxr62SfT7ZmyFm3KKxf8s/8cdu2VPeSxKv3vecCZyfKb7Jw1Zpe9JmM6H3+yGQeckIIH8oKCR4zJq3BM885b7zkzG9ZtUrFnZIPaPZpNOrQ7Tk468TuhfNwsHz1FwQN5QsEjxuRVOKaRTalu3+XX4GXkgfYulSalh7ZsXqbHSxYt81xjx/5Gv4yRj77oxyXOCe/oc5c8MI6h4BE7FDxiTF6FYxpZtCpOwcsFOvJsevGHH0yRk87moIGPB6+x5z0jl8mZZYtXyBPGLhQ8YoeCR4zJq3BMI3tR3sGrNcOeHS6P9YxavHvXf+VkwGbAZMB8xmVq5qUXRsiVsQsFj9ih4BFj8ioc06SsH2bTaXFEa1nwzQ9rcXyHzk4eeWiQPOHXM+eo9Zd1v2L2rLkLf1j80AOPqM0zTzvHXvPKy6/a7btz+89HNWvVpPRQebbyikeXD60il5nPLfQJZeXHNBQ8YoeCR4zJqzCx8+nUz6+7+oYr/n712DHj5V6dhwc8enjT5nVq1T/9lLPmz937T9bUMDu27Tqh40klqbphnbBAQsEjdih4xJi8ChMSUSh4xA4FjxiTV2FCIgoFj9ih4BFj8ipMSESh4BE7FDxiTF6FCYkoFDxih4JHjMmrMCERhYJH7FDwiDF5FSYkolDwiB0KHjGmrrnyQkxIFKHgETsUPGKMgie5Ce2OOKLgEW90PIk6tDtiioIHACCBKHgAABKIggcAIIEoeAAAEoiCBwAggSh4AAASiIIHACCBKHgAABKIggcAIIEoeAAAEoiCBwAggSh4AAASiIIHACCBKHgAABKIggcAIIEoeAAAEoiCBwAggSh4AAASiIIHACCBKHgAABKIggcAIIEoeKBY9Ojey467G0CyUPBAEaHggeJBwQNFhHYHigcFDxQXCh4oEhQ8UFxod6BIUPBAEVHtvnX9LhVqHkg8Ch5IPtPrTni7HkgwCh5ILL9el6HpgeSh4IGkyb7XZah5IDEoeCAhatLrMjQ9EHcUPBBv+t112dBhhaYHYoqCB2Ip6l6XoemBeKHggTjJcal7hqYHYoGCBwpd7m/Wsw9lDxQsCh4oUIXc6zI0fU2oP73yn38hxZwo/gZR8EDBiVGvy0RxnUo22p3ohP53h4IHCkWse91J6JeqpKLdiZ1w/+JQ8EA+JanU/aK/1uB+5KhAwRM74f5NoeCBPCiGXpeh6SUKntgJ9y8IBQ/kTnH2uky4V7FYo+CJnXD/alDwQOT0navsORLu5SzHSktLzX+rjYIndsL9G0HBA5Gg0auacC9tEWncuPGmTZvat2+fpuBJBAn3bwEFD4SJXq95wr3Ghejmm2/Wg9J99LjSoiqi4ImdcF/8FDwQAt6EjyLhXuxq7pBDDtEDu9opeBJiwn3NU/BAjdDrUUd/8uT+uefDiSeeqAcUPIko4b7UKXigOuj13CfvTT958uS5c+emeYueRJZwX+EUPFAF9HqBJNzrYPZ0r69Zs0aPzX+rjYIndsJ9YVPwQGb0emEmv/f0FDwJPeG+nil4wFd+e733zX3tzZ/WbF+9ZIPZTP2tRB4i5/2W+cWs14MeV9007YPP9cy/xr0t1wekqg9dk+Sl6eNY8O+89Z6cdDL1o0/WrFwn5z0zcfybcrKq+XrmnNEjx8j5jNm8aaucrGG+mfednHQy48uv5WQoCfdlTMEDleS+1Jf9sHrwwCdVVCPqgZ43BT9mxLjWR7XV41uuv00PZH2qGSeey5xDgg+3Cz7jqar00NEl3Etk1CIqePl/08z7rRk08HEz/8brE4LPaRbYJ5TnNFGfNMgTmgwaOLhBvYZyPmOcR1epV9JAP2L7Nh1L6zfWY3mgSruyDk70/O297/Rbc/+9A/S83zlrnnBfvRQ8sFfue93J2mWbUpUb0RR89wsve3fSB3q8Zd3OlYvWbvWpzxuu6akucD2v721m7GXysitP8s3XP9gHZl/wdg7cv06V1keUcK+VEYmo4HWOOfrYEzqeZM/4NVO3iy61XxWy4NXk2DHj5Xn8TugcKwvefjgZZ/GHH0xRk2Ut25qYk9jL2rZuf3jT5s6x9/V7QJ7QOdxeYBe8mvd8z8PvhDVPuC9aCh5FTb+1K7sh9znikKNS+xpRX+Pst+jta5+Zcc7Qoc1xG1ZuUYN1K36yl/W7o7/fIXa+m72wTq36Sxas1MvMw9kFL9P80BbyVMOeelGvl7vyknAvmjXkPJlIC17/P3Jm5DJnPuVT8J6bfid0FsuCN3n/3f/c0eeu2269ffzYiXKvji54Oe9MtmxedmSzls6aRx4a5Hmsfbi9gIIH4q1wet1EXTJmfT43ZZWi8zV4uT5gxoydyaGPPeNk+rRZem/J/nX1oGnDw8yB9h18lmnfpqMepCp/RlIICffqWT36tWeeSXQFv3bVev3n/+m0L8ykZzOpSbsXU14Fr+6Mjzn6WD3evnWnOY9zQvM/3YlnwW/etFXtGjr4qSWLlq1euXbMqNfVZu1UPbkyy4JXUYfrRzyu/QlNGx2mx/JA53B7jT5k3pxv9div4IPPXO2E+xKl4FGkCqfjp/3ni9S+Fnx6yDAz1gU/b8Z35mqi0qr50UMeeWqrV8HrSZ1xoyeaGbnMM/prBJPGvqUP0f81Bf/ck8P98sP8JeYkW9btvfTrcfYPnZtU6dJpajgg7jHZcc7QI7KCT1nttXL5amdSZ8K4SWqm07EnqvF5Z1+gk/IqeJUObTuaF5iZVOMG9Rqar6A75w+OWtzvznvlpFwZUPDND2+hIndlE/uPyExyBw8kR4/CqHkZeQef2le9OvKQgMhi9mxok47HnLC16nfw6lmpjjdjuSD3CfeKWXPyEwV5la9hFv24RP3h7971XzMjm+znneVq3PjgQ+ThKZ+CV1m7eu+7As7igM3gzJoxW61ftWKN3tyxbZf6RMHzDAEFLyezj/xjKRcF37ThYYc2blZrv9op65ObGj5uQMJ9uVLwwJ/y2/QpUYd2wb814T1njVzvOenMqHOqRvfcpXPpxVf4HXvNZT3kepOxoyao9fqbADwPz3HCvVCGyL5915vyKp+zrF659vgOnWXef/c/zkpTb3b0fMZlZrFnrr2yR6MGTRvUbdj1gu5+P56XTcHb34LnGfvAm6+/RT7Di7t0K69c8H7xfDKhJNzXLQUPuPLS9KnAOgzeay+TsRdkLHg1uXHVVjNeMHeRvUuuN2lYv4kzE7w+ooR7fYyC8wyjK/jUvi8kO5NypVzjdwcvI0+oPjkwkz98v0guqEZkwZuvkcvFNQwFDxSLnJV9yus74IL3Lvp2uTxJ8ExwwdepVV/dgqv58WPe1HtLDqjboc1xZr18DvaTdCLPH1HsG+LYibTgPWOv+Xln+XVX3+Ak5VPwzrF+k9Uo+Iw/B68LXr0+j2rW6uorrjP/Ko5z8q4XXqLiHDt71t7vXXUmPQ/XeXn4SHtz4/q9P5Mil0WUcF/JFDyQlZw1fU3y9JBhMvaC22/9xwtPv6zH9UoaOIevW75JDy656HIzOeuLec6yLHNYkyPkZIiJda8bkRZ8xjv4XTt2Z99eGVfqTyD80v+eB+QhOhkL3i/OUwql4J1Q8EBxiUXZJzLJKHVbpAXf6sijnX+LzekqXfDya/Aqnif0jFxZ1YRY8A3qNnQ+5JbNy/yepPxY5EekC17mww+myBPWPOG+vCl4oPpo+twkeb1uRFfwMcprr46Td97Z5LyzL5CTsU64r3MKHggBTR9Rwr3eFSAKntgJ9wVPwQOh0TeasqVIVRPuZa6QUfDETrivfAoeCB81X72Ee3WLBQqe2An3rwAFD0SIe/psEu5FLV4oeGIn3L8LFDyQIzS9nXAvZPFFwRM74f69oOCBXCvmpg/3+pUAFDyxE+5fEAoeyJviaXr9pQr34wcFTyon3L8mFDyQf0lteno9Iwqe2An37wsFDxSQZDQ9vZ49Cp7YCfcvDgUPFKKYNn24l6diQMETO+H+DaLggcKlb4VljxZawr0qFRUKntgJ968SBQ/EQGE2fbgXo6Ilr/KkaOO+OGqGggdiJr9lT6lHgft4EsXfLAoeiKtcNn0UVx/A4AUWBQoeiL3omp7LLnKDV1oUKHggOcKqea62yCX9GSqvutBR8EACVbvpucgix0y789oLHQUPJFk2Tc+1FXlkFzyvw3BR8EBRkE3vdz2dPXu2OwVEzPOliBqi4IEku/LKK0srmBm/sTF69Gh7Ux+uderUyd4FhIWCjwIFDyRWnz59nnnmGT02XV6lgpcL5AxQcxR8FCh4ILE8u9xz0mYKftq0af3796+0z+cQoIYo+ChQ8EBieXa5frNdb3q2NXfwyD0KPgoUPJBYfgUvJ212wX/66adqzdVXXz158uQWLVp4rgdqjoKPAgUPJNaxxx47f/58Pa5ewaezWA/UHAUfBQoeSDJVyb/88ov67yeffGJm7L22Ll26pCl45AMFHwUKHki43r1779mzx2xmLGkKHrlHwUeBggeKS8aS1gW/adOmZ/y5xwA1Q8FHgYIHikvGgp8wYYI7BUSMgo8CBQ8AyDMKPgoUPAAgzyj4KFDwAIA8o+CjQMEDAPKMgo8CBQ8Uix784m0UKl6QUaDggSJCwaMw8YKMAgUPFBHaHYWJ12QUKHiguFDwKEC8JqNAwQPFhSspChAvyyhQ8ACQBLHuyFg/+YJFwQNAEsS6I2P95AsWBQ8ASRDrjoz1ky9YFDwAJEGsOzLWT75gUfBIvtTfStwpIeOajAsyqtIZqrTYk3OGLE+ol13S7bK77rjb3VdZlidU/ve//2VcnPHhqifj415w3kV+cZcWvFh3ZKyffMGi4JEc6mpux54345NPONXEb4096Zfet/ZxFk8cP0nv6tO7r7NL83wI5bSTzzSnPbpVOz3pt/jyS6+UT0Zl1Mi9v8Q9bTWWmrSLyu+E+vAGdRuazXTlgpePZa/0pHbNn/+N2XQK3vNsAQXf7JAj3al9zElOP+VMd1+FgCfpeGjAw9kvLkyx7shYP/mCRcEjOewL9NVXXHvuWV10/C7cel6WfTVMfvMtc4Y6teqPfPkVPXY+n7A3zbH2Qx/apNmA+x90Jm2q4N0pH84ZPE+oJleuWKkGb7/1Tt2Sg/RM2usO/tLul61bt96e8TyhlgoseGPPb3vMfDUKft68+Wbc8KAmtfarrcd+f+bjxr5h1js+/M9HauVLL47wfJ5xEeuOjPWTL1gUPJLDvjqrgvecT3ndPjprbKNfeVXtOq7DCb169j71pDOco4y+fe48ufNpevz8cy94vsHreaDS/eJLzfjhAQPbtj4m7b84y4L/97vvOWeQH7KetMfmEx1Z8Gq+zoH11WDJkqWep7KpXeozBrPpV/BqcuGPC/VYPZzfOf0K3vbkE0/JY9X/CDnpUK1vP+7Xs2anKt7P+PijKZUXxkCsOzLWT75gUfBIDvtqrgre9JA9L6/4co1Nzrdo3vqSrn93JtPWSjX49ddfK+9M1ytp0Pywo84963xnPi1advnyFc6kLcuCV4c/98ww9aD2jLXfY1KP9X+dgv9k2qf6j+jzzz531ntSu448vIXZ9Cx4NdOpY2ezWY07eJs627Bnn5eTfn/mxrVX9XCn0un16zece1YXd7bgxbojY/3kCxYFj+TQJWRiz3uO582bf/99A+S8Tc2Xl5c7M9O/+NKe0aZN/UTt6ti+U1nLts4uNd+oQVM9MF/tNu67537znC/tfpmelB+FpgpefjuYyj1332fWqKN63XKbGtRO1TNnkKdyJvVY/9cu+OHPv2ifxNSt5wk155k7Ba/fJxj+wktmJi0KXv0xmqiPwt5UsVfqx3p80GB7Us/ru/CU15+5dtrJZwbkxeEvuwcUtlh3ZKyffMGi4JF8To2p1K99cOsWbdTd2/vvfSDXONTNqD5KpU6t+jt27HRXVNBnOK7DCeZUuthU5s2dZ5a9NmasnjQzmjMjF0iea9Tku+/822y+MmKUmTeThj2ZqvjsRM+Ygt+8eXPJAXXNmnTFFyPMenveeHjAwBXLV6hb599//13P2AX/2aefe940V/sO/o8//lBPSZ1/zeo1ekb+8fr9mTsyLihwse7IWD/5gkXBI4F+/fXXRwc+dluv2+2qCzZzxix3qipUN6ga02P1CYH+crUjuD+cvcGLtWzWGL/9+ps7VXGGDRs2qsGUj6ceVKdUz6TFW/Sa+iO6+85+6k/1nbffdXYZWzZvMc/KDDzfos9ecMFr9vfr2Twn/VRpcQGKdUfG+skXLAoeSaMu00cd0Uq10caNG0e+/IrelGtSFV+g/Xv3yw9t0kyNXx01xllzcL1Geplf7MXBmwGT6YrqVTfKqYrv4u5+8aX6jtlvsc1vjXyq8gkbzl49kAWv5vvdde/Spcs2bdr0yohRavOoZi3tBel9P+9nvqIx46uZ+mxOwX89a7b9je466v+CWeDIpuDTPn8anpMO509J5crLrnYXxQEdCQcFj0RRXd7/3gecSecqf8qJpx9/7An2TFqskYIXqL2qt/R4xEsjPRd7Tp54/Mkd2h13xqln3XR9T1XwC77/Qc97LnZks8YIWLx48RIz1sucgs/mTzVANnfw8g0Dw6/ga6fqmfHQIU96PoTnpE19hqffw7Cpo77/boEzWfhyX/Bbt25dbFmyZO8LqbR071tBWuk+6vNCZ5dhT95zzz3mE0RzrM2sRDYoeCTKt998p67Of/zxh5m5/74BzlVe3aw7M8uWLc/YBBkXmC/VX9Slq7uv6jI+XFVleULPgtd/qn8tSqcH3P9glidM17jg1SdA7lSF33//Xf+Bq5TWb+zuzs5XX85oeWSZPbNt67aMz7Yw5b7gtZ9//rlXr78e2tSw3cd6nLGh7YKXMh4OBwWPpNnz2546B9Y3l/4br7/ZXZFOT50yzSxIVXzPnbtCyPFFP/SHq9IJ5Vv0+ovcwX+qfmpY8FGb8MZE+0NrV9beXRET+Sr4Rx55xPS3fZ89c+bMsrIyVdi33377KaecohfYB+qZLVu2jBgx4pBDDklXFLw5g/rvb4JzOIJR8ACQBPkqeNXEbdq00e/P683K+/8id7Vo8ee/l6B3OW/R2+//a+ZAZIOCB4AkyH3Bq8Y1nd24cePWrVun91X1+++/r+/FlS5durzwwgu///67LPizzz5br/n3v/f+wEvwW/SoKgoeAJIg9wUv/8XGtLhN15vTp0/XRW7vUnbv3m1vLliwQP+46ciRI//87KCyc845x16PYBQ8ACRB7gteK/VqdDN2Ns1YW7Zsmb3p3MH37dvXfN29ceNqfh9lMaPgASAJCqTgbQG7NFXwmy19+vSh4ENEwQPRmjpl2p49e9zZsGX8NvWMC5TpX3y5ZMlSd7aybL4lPl3xg+l6kHFx44Ob/vPJZ9xZVF0eC96RzS5NFfwqy6233krBh4iCB6KlGm7b1m3ubGU3Xd9TLVu0cJHe/PSTz9Tm3DlzK6/6y/z53zjFaTYnjp9k/9CXmZfr5TK74M86/RzPNZ4F/9ijg+1f05K2Hs4M9L9z55wtTcGHJ18Fj4JFwQPRSmVR8LIy/Sa1gIJ3yKI1Vq9arX/HXXrfXs87eLVr6pRpZtOz4G+8/mbn96sGPG668kko+LBQ8HBQ8EC0zA2rE2fNrJlf2zOPDxrc/LCj7Bnb22+9I89gb5rJhT8uNGPncXv17H1br9vN3rR/wevfu6qFUvBnn3HuFX+/So8p+LDEuuBj/eQLFgUPFATnd9uccNyJ7gpL29bHZCx4NdOpY2d709r558yuXbvMOO1f8L1v7WM2Qyl4e5KCD0usOzLWT75gUfBAhOzOlqn2v8+qD7ff+bcr8+QTTlWbw194ycykvYrWzAx5/An9C/dUweszO8vsmewLXv+aOLl47Ovj7EkKPiyqI2Md9+NBjVHwQJ7pBvWLuzqdfnjAwFr71XZ+A7oZq8G1V/Uw84Y8Vd2Sg/RA7dq5c2fa6w6+64Xde954q1qwbu06PZN9wTsDw5mh4MNCR8JBwQMxYwry8KbNA3o0mOcnB07B259DmEENC15tfvPNt/YMBR8WCh4OCh6Ilmo+HdVtTRsdZjbtNfVKGtibfnTj2j8o3KT0EN2gdo9+PWu2fnvcjr3gqsuvsQv4icFD9dgu+BXLV6hd27fvsDfVE/Ar+JT1rsPzz71gn18P9Pv/zgeepuDDQ8HDQcEDOaLL75dffnF3iNvcash4Br8F9m9Dtwt+8+bNZt7mWfCSLPinn3r2r90WCj4sFDwcFDwQuVNPOsMuvDq16lfe79u+2ct4howL0uItek/VLng/FHxYKHg4KHggWqrhxr4+zp5Zt3Zd7VS9ieMnmRn5jrqOdVAGGXs044J0qAVvZFxMwYeFgoeDggeAJKDg4aDgASAJKHg4KHgASAIKHg4KHgCSgIKHg4IHgCSg4OGg4IEwyW8a/3rW7OeeGbZjx95/C1aTaxxV/U71jNTZxo19w50VPB/0gvMu8sz4Nya4SyvzPJuyc+fO++65/7VXXzczt9zUS/8WedQEBQ8HBQ9UR8rnH423W2379h1q8+233tm2dVuHdsddeP7Fco3eTFX+4Xi/gpcPqnPP3feZNWvXrJUnTFkFP3PGLOdwe5kZS85iR5a/ov69f7+vnti6tev0r5xRH2naq+CdZ6gzdMiT9ho4KHg4KHig+h7o/2CThofaM05frlm9Ru6y16h72VdHjUlX3CjXTtXTkwEF/+F/PnImzzr9HLvgzYH2CVOi4PVY77Jj5qXgBVkWvD25eNFivSkLXmp11NEUfDAKHg4KHqi+Rg2a6or6cvpXqqJU7ALreeOt9WsfbDbNLqdf5bjaBX90q3bqMwazy37EgIL3HNuWLFmqd61csVINRo0c7a5Ip7/4/AvncM+z2ZObN2/WmxR8KCh4OCh4oPpSFTe1+n1mM2PtT781+W295sD965hJv05V4127dqVrUPCyYvUJU9Ut+G4XXaImzTsBWod2x+kPyp68+YZb5KPbm9qro8Ycc3QHNVi6dJla8Ouvv6YrCt45od50QsEHo+DhoOCBakpVfPG7vLzcaSZryZ/2/LZnwfc/PP3Us+efc2Hav1PVWC1Lh1rw+oSpwIK3Y+arSh7udzb9NsCA+x80M/IOvm+fO+1NZIOCh4OCB6pD3ZHrO1Flw4aNpszM4KROp9SpVb9Rg6atW7RRkwMfenThjwudNX7jgIL3jF3wu3fvttebQfW+yU4+lh1VzHqZ/j22E8dPqltykH2sGTtSFd+dMOXjqf3vfeCoZi0p+FBQ8HBQ8ECYPFtNT3726ef/fPIZFXuN6mb9A2MXdelackBdPelX8Da/BWbePmFK/Jjczp1//die4XfOYGUt25oDZ38924yzP5ss+HTlw7M/VTGj4OGg4IFqWr9+gywe+xva0/uayfnisXNUquJu2P46d00KftXKVfKEsuA9D3/5pRHulM9vugto302bNpkv/Nvzhpyn4ENBwcNBwQPV5FnwjowLPHkWvK7tgDjrbansCj572Rzut8ZvXju582kd23dSUcv0wBm7B2AfCh4OCh6ophwXfE3EqOBRbRQ8HBQ8UE2bN2+W712r2GvkXp2vZ822lzlyUPDyKcknHyCbp+e3Rj5olR4afih4OCh4AEgCCh4OCh4AkoCCh4OChy91vSCExCju32EUNwoeAIAEouABAEggCh4AgASi4AEASCAKHgCABKLgAQBIIAoeiIHS0lJnpnHjxmbcqlUr/ftdbKVeHnvsMWdZDZXue2Jjx459vTJ7rx4bQ4cOdQ4HEDoKHigsdhGa/rMHju3bt3sWvFHqU6IvCn4rNedxzWTlVW6pW3v+1Lx58yeeeOL9Cp4LAISCggcKV0CP2gIKvn379ulMhxvByzyb2zlkz549nstsGRcACAUFDxSoV1555d5779Vjz1I87bTTBg0alK4oeDV50kknmTVLlixRMz179jTrZ86cqQaNGjUya0q9mL2SvdeMnUPU5m+//Xbdddd57jUzWT4igJqg4IEC5VmoSt++fZ35gDv4tFfLaqNHj3anKisrK7M3PZ+PGdx9991q/H//93/pircN9LzZO2PGDF3nf/zxh57R/J4bgJqj4IFCtHHjxt69e5tNp1ynTp26evVqNVi1alXaq+B1m3r67rvv9Jobb7xxpOCcZM2aNfamHJvBggULzF5Df6sdgLyg4IGC8+OPP9ptmhZ3utu2bfv222/Npiz4LJnTPvjgg5MnT6680xVc8NlsjhacBQBCRMEDhaVJkyay9uwZdQverVs3a6dvwZdW9sEHH8gFepBlwdvMZMAaZ6+UcQGAaqPggRiwi1AWvCfZnWZGnaFyC1dS+aAMnPVV2gQQKQoeiAG7Gv3q2Vq+15dffqkmb7311o8++ujpp59WY3Wj76ypOedxnaeUcW+peNoAwkLBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPAAACUTBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPFAsenTvZcfdDSBZKHigWFDwQFGh4IEiQsEDxYOCB4oI7Q4UDwoeKC4UPFAkKHiguNDuQJGg4IFioap96/pdJu5uAMlCwQMJ5/S6E27ogaSi4IFkCu51z1D2QJJQ8ECiVKPXnVDzQDJQ8EBC1LzandD0QKxR8EC8hd7rTvixOiCmKHgglqLudRlqHogXCh6Ik9z3umcoe6DwUfBADBRIrzvh3XugkFHwQOEqzF6XoeaBAkTBA4UoLtXuhKYHCgcFDxSQmPa6E2oeKAQUPJB/yeh1GZoeyCMKHsinpFa7Hb4XD8gLCh7Ig2LodRmaHsglCh7IEV1vsvaKMzQ9EDUKHogcve4X7umB6FDwQFTo9exDzQOho+CBkPFWfE1C0wNhoeCB0NSk11N/KzHj554cbsdZoAZ29OTTQ4bJc5rYJ/fM37teMXbUBDlvZ+Znczoec4KctxP8QMF77VDzQM1R8EBNVbXXXx42Sja0LD9nRi7Qk6qYVW64pqeZcSKPrVfSQM2cePyp6r8rFq7d6lXw8iR2wauxs+CntdvtB1KD4c+M0LEn7YfIJjQ9UG0UPFB9Va12HVXwvW/uq8dvTXhPD2T5OTNyweLvlptJ5w5++rRZp514puexKxevKzmgrrPLs+CdsecdvN+TlIfLxdmH78UDqoGCB6qser1ukk3BP9T/0cceekL1rplxFqg7Zj3j3MHrvP/Wx+3bdPQ8dszIN7pd+HdnV84K/uH7BzlnqFJoeiB7FDyQrbC+ey5jwfe46ia9WadW/QZ1GzoLNq/dod9jt8/p3MGfeco5TsXqOKfqd0d/daqtWRe8Psm8md97LrM35eFycU1CzQMZUfBAkFAa3UnGgs8Y56v4Ombvj/OXluxfd8yIca2PaqtnMp48y4J37uD1vFzpl+C91Q639YAnCh7wEEWvm2RT8LIL5Ywd+w7erFSD22/9R8ZjVy1eX42CP/v08w9rcoQanHXqufVrl3oeJWP2RhFqHrBR8MBfwnoTPjhfT5+v+thET8qClzF7x49502+vGmxYucWsbFi/iXNy56hLL77iw3enyYK3n6F+knbBt2p+dJ1a9c3iB+995N//+o/zQDLBe0MMTQ+kKXhAy0GvB8cpP9mF9owq+Gsu6+EsCIg8mxNZ8DLyLXqZ4AcK3ht6qHkUOQoeRS03t+zZRBa8jNnreQd/xSXXyNN6nlwmkQVvQtOjOFHwKFIF0uskZ+F78VBsKHgUF3qdUPMoEhQ8igsFTyh4FAkKHkWKpi+28BY9ig0Fj6JWON9kR6ILvY7iRMEDe+W45hd/t/ykTqeV7F/3tJPOknt1Mn7PeZ+ed9avXXp33/v0pvxn3p0z2JsD7hkovw9fTTpnkAk4Zzap6vqahF5HkaPggb/k5oZeldy5Z1xgbw4d9LQevzZy/NDHntHZO79vrCJP8tnHX6nBO5Pe161ZpYKXGTzwSVnw6pBjju6o/nvx+Zd4niTgnPqTBpOM60MM1Q6kKXjAU6Q1L0tOzmSMfYgseKdcp0+b5Rzy9JBhG1ZucaJ/p7vJtVfc8FD/R+2HcE4iN/3id3i4odcBGwUPBImi6Z2SmzfjO2fm7NPOs+tZnsE5iR5X6Q5eFbz9EJ6PZW/eesNtclJvytgLnKM899YwfPcc4ImCBzIL96375T+usevQ/kfdVdq2at/vjv5mc/PaHZ6lOH/WAnOGpQtWbfUp+K+nz7c35XkCYq9/e+JfvxTHr8X90r5Nx5EvvGofLtdUL/Q6EICCB6omxKb3zB297y6t39hsjnrxNdmI9975wD9uu+eGq2++4NyuRx7eSremU/Drlm8qa9HWPtaMx4950y9LFqyU61Wuufx6Z/KDtz82ZS9jDrznzvv1r5yX56xeKHUgSxQ8UE3VaPosS/Gh+x71nJex9zoFr3epU836fK5c3OXsi+QTuOX62+wzqE8jZK8HPx8n9/d7yFlfpcPt8D48UFUUPFAj1Xv33u45/Svb7Qx6cEj/ux6UK2U8C/6nNdvV/IxP55g1x7Xv7CyWGTNinFPw+hCdJx//89v4sy/slPjqQ/B6v9DrQPVQ8EA4qlTzfgWvfyLuovO6n3vGBc4Py82ftSD4PPJr8AGLr7mshylvE1nwMtkX/OLvV8jJgPVO6HWghih4IGTZNL1fwVc1NSl4uSCbyIK3f1jf80f2Aw73DNUOhIKCByIRXPMN6jY047tuv1cuqEYeHzhUTtqxv9ltwbzFMz+bIyOPcpJNQwfksCZHyEkdeh0IFwUPRC647Is5fOscEB0KHsiR6n07XlJDrwNRo+CBPCjOpud+HcglCh7Ip2JoenodyAsKHsi/pNY8vQ7kEQUPFJBkND29DhQCCh4oRDFteqodKBwUPFC44lLz9DpQgCh4IAYKs+n57jmgkFHwQJwUSNPT60Dho+CBWMp901PqQLxQ8EC8Rd30vA8PxBQFDyRE6E1PrwOxRsEDiVLzmqfXgWSg4IFkqkbTU+1AklDwQJJlU/P0OpBIFDxQFGTT0+tAslHwQHHhu+KBIkHBAwCQQBQ8AAAJRMEDAJBAFDzi56mnnioV3EVADo0cOfLII4+cOXOmmeE1ibyj4FGI1MVxdGXBl8vgvUCk1Mvv8ccfV4MuXbocffTRZrLSIiDnKHgUInlxtGd03zustUBO2S8/M+Zlibyj4FGI5JXRnlEFP2TIEGsnkE9+BW8mgbyg4FGI5MXRKXh9e2RbvHixtRzInV69eqlX4MMPP6z+O2PGDD1ZKl7DQI5R8ChETnlrAbu06dOnVz4NkDebN292p4DcouBR0Eq5DULBGzlypP4Us1OnTvZnnO46ILcoeBQ0z6vkjBkz7Muo5i4Ccut///ufLni9yWsSeUfBo+C41W3RC1TB33TTTc4h9iaQS5MnT+7bt+/ixYtXrFhhXoq8JpF3FDzih4JHQVEFP3DgwI0bNw4ZMoSCR+Gg4COhf2EXqWrcP0cfGzZseFGwF8gzExJFzAvyo48+mjNnjhp/auEFmU3MnxJCR8EDAPKDgo8UBQ8AyA8KPlIUPAAgPyj4SFHwAID8oOAjRcEDAPKDgo8UBQ8AyA8KPlIUPAAgPyj4SFHwAID8oOAjRcEDAPKDgo8UBQ8AyDX+PbscoOABALlGu+cABQ8AyAPaPWoUPAAgPyj4SFHwAID8oOAjRcEDAFAQwv2Mh4IPE98XCgCotnCLg4IPEwUPAKi2cIuDgg8Z7Q4AqJ5wu4OCDx8FDwCohnC7g4IPX7j/hwAARSLc+oik4J0vRZMijPuaAABkEu7FM+SCV0+u/OdfCAn3ZQoAxSDcK2fIBS8v9KRoE+4rFQASL9zLZpgFz+07ceK+RAAA/ih4Epu4LxEAgD8KnsQm7ksEAOCPgiexifsSAQD4o+BJbOK+RAAUsBXLV6T+VuLO+pg7Z269kgbO5FdfzigvL7dnsj9hvrz/3geF8yQpeBKbuC8RADnR+OCmqrTsuCsqOPMBBW/O06HdcXpmzuw5cvGokaO3bd1mz9hrLul2mfOsMj49z2SzZuGPC+1lxswZs9TeyW++ZWacgpenCj5huCh4Epu4LxEA0VNtdOwxx9szjw58zO4ww5n0K3g1uWPHTj2+8PyL9Rq74JcuXXbCcSeqHNWsZcf2nfTYHKsHfr7/bkHGNTZncZWO7XbRJWr9rl271H+3btmqJ2XBm3HuUfDRRv3fzTjpucbJhx9Mmf7ZV3Je543XJ1x4ftdzzzx/+LCX5F6/ZPO4BRX3JQIgep4VFTyZsm5VKy/5c6/c9LyDnzh+UsAdvKeqFnyThofam1U61l5sxhR8VuJV8PYLWqXrhZeYeb81KldfcZ2zRqUkVVfvbdroMDPpV/Dbt+5UK3vecOu8Od8u/GHxwAcfVZuHN21uFlza7XL5uHqX87iFH/clAiB6FVeqa+2Zjz+akhK9dc/d96nJcWPfMDMBd/BmvGHDRr3pWfCvjhqzbu06e8Zec8tNveTFTcc6woO9oNctt1l7Kn1qYufll0bYy5TllT86M5YF7xl5wihQ8OEkZZWlqm3Pgi/f18c6nseq8e5d/9XjpYuWm11+Be+c30yqe3pnsmXzMv35RPCxhRz3JQIgJ+bP/8Yup1EjRzsLpk39JFXRauq/Tw19Wk/6FXzaqr1zz+qiZzwL/qA6pWaBZq9RBd+xfSdrZ7bkA0nPPTPs4HqN3FnLY48Obt2ijdk05wz4Jju/+ehQ8OEklV3B25tm7DnpbFa14L/6YqYz2eKI1lddfm3GYws57ksEQAG46fqednWpca39aqcDC96xaOEiz4JPVXwS4MyYcZUKXp/KL9msMcs0eacu5+UZAk4YBQo+nKTyVPB6jUrdAw9q1ODP73Sd8tE0v2XOjFxWyHFfIgAipq8bfnl80GC1ZuWKle5hFTwLvuFBTQ5pfHinjp3VrgvOu+iLz7/Q87Lg1eaENyb69agy4P4H27Rqp6PmzVjFrImU5xMLuIPPPQo+nKSyK3iV7hf/XU12Pv5kz2NXr1yrNt9+893XRo9VA3PPHVDwWSZFwQOogYcGPNzskCPd2X1kq3kWvKF2PffMMLPpFHzJAXUP3L+OHpe1bGt2+Z3Qb962eNFifRm04y5Kp++/b4C9QL8b4emGHjepBcuW7f1a6s8//6wnZcE7J1RRa+wF0aHgw0kqi4Lfveu/27fuXLVijZocP3bixV266b32GpXFC5ce2aylek1v+WmbmaxhwZ90wqkXnHfx2aef175NRzPpPG7hx32JAMihqhZ8sJR/wU9+8y37y9vpim+n1wO/R/Gbt8k1a1avadG8tT2j6vzIw1vYMzt27P3GKXvGtnTpsgZ1G07/4ksz4xS8PGHa65lEhIIPJ9kUfMf2nTofd9KF53e9sUfPF557ae2q9XKNXzwLXn0SoD+3bVJ6qB6XtWijNvX4uaef18uWLd77ebQe24+VzeMWVNyXCIAcylnBBzBr1DNJidtxO5WP2yubSb81q1etdmd9BHxNwRj9yqvZn7AmKPhw4ry2PAs+y8yeNVce5VnwOmrxoIGD9XjXjt3OscOHvaRmdmzbZa8fPGioHsizFXLclwiAHMpY8J5x1+2TqlnBV0PJAXt/AvnF4S+vXbN248aNr782Tp5t6pRpavL6625ct3bdls1b3n7rneCPQnIK3pxwwfc/7NmzR32Y6k6vSiesCQo+2qQql+gpnU+XcdZ4FnxAUoEF/8xTz8lDzIFyspDjvkQA5NC4sW/ceP3N7mxIlixZekm3y9xZwfmJ/GoY+/o4dZIrL7t65MuvuPv2mTd33jVXXnf5pVe+MGy4uy+Tr76ccdbp5ziT6oS33NTroi5d776zn/k373KAgo822ZRopAUfkOxXFkjclwgAwB8FH22yKVHPgveMPFYfTsEDABwUPIlN3JcIAMAfBU9iE/clAgDwR8GT2MR9iQAA/FHwJDZxXyIAAH8UPIlN3JcIAMAfBZ85qb+VzJvzrZy388P3i+RvaJWZNGHymxPfKs/iO9inf/ZV8Jphzw5X/61X0kDucpLy+u2xMi8+/7JZL/faeeap5zKuiSLuSwQA4I+Cd3PEoUc1KT101/bdZsYu+I3rf9LNqjJx/Jum57Is+Bt79Ox5496PK2NByoJvV9bhkq6XmU29Vxb8siUrylq2tWeyLHjzcPbj6p+70xk/dqKepOABoPBR8JWiemvCuEmfTPnMLrBqF/w/+vYz7ahXyoK/o89dHdodZ2LmnYI/qE7pgP4P639qXs/ogSz4xQuXOu2b8ip4+4mpbN+69xcqmF32Mnv83jsflFPwABAHFPxfcUrr0MbNzLxd8E416nm/gm/a6DB7Rha8EzPvFLzdvuvXbjQzsuC/+2aBc/KUV8GbXV989qUZO4OzTj/X/KP69i4KHgAKHwX/V2QvmkHAHbz+1W1HNWslS7SqBb9713/NvF3w6r7ZjC88v+vtve80Z5AFP+SxoXqXeujUvs9C5HPTUbtGjxxjxs7g0Ycfq7VfbbNYnUTvouCBQqAukoQEx33R1EAxFrweZ38Hb0rXWayi2vTuO+/RY7vg/zn02Y7tO+mxKl37DLLg5clTgQV/eNPm6k5d/9pZM2kv+NfEyWrw/LMvpireyS+n4AEUpHD7DI6iKPjmh7U4vkNnlZbNy8ya7Ave7w5+wbc/2pN2wb8z+d9m3PWC7n169TVn8Cz4QQMHH9mspT0jn5vZZX+YzkCnfu2DUxW/ct7MUPAAChAFH6nYF/zkSW/r8Yplqwb0f8jMV+/H5LIv+NNOOsOZCfgavD1wCl5Nqk9B9EDfbeuxfG56fteO3ff1e+DrmXPMCe2BvdLepOABFCAKPlLxLvjyiiYb9uzwiW/8yylXp+BlvfkVfK39aqf2vaPeoW1Hz4LXe51jnYKve+BBe7+LXryRbhe8mjm582n25v33DtAD+dwOaXx4nVr1zUr7W/Pkk1EzH3849bFHhpx0wqlqTMEDKEAUfKRiX/AqnY49sV1ZB3smlUXBZxlZ8Gpw7ZU95Er5c/BtWh/T7aJLzaYs+H533uucxKyUBW+fXN3H2zNmoD47qZ2q17J52aknnnHXHf0mjJuk5yl4AAWIgo9UEgpeJtKC94sseCey4P3iWfAysuD9QsEDKEAUfKSKqOA9I4918tILI0a8NEqfQe61k7HgL7/0qvJQC978yHvw45ZT8AAKEgUfqWQWPCmQuC8RALBQ8JGi4EmEcV8iAGCh4CNFwZMI475EAMBCwUeKgicRxn2JAICFgo8UBU8ijPsSAQALBR8pCp5EGPclAgAWCj5SFDyJMO5LBAAsFHykwiz4NB1PrPBXF0AwrhKRCrng03Q8qah2/t4CyIgLRaTCL3gAALJBwUeKggcA5AcFHykKHgCQHxR8pCh4AEB+UPCRouABAPlBwUeKggcA5AcFHykKHgCQU/onae24KxAGCh4AkGu0ew5Q8ACAPKDgo0bBAwDygHaPGgUPAMgPCj5SFDwA5Jr8LjNS5HFfImGg4AEgd3rw67iIT0KveQoeAHKEdifBcV8xNUPBA0COyAs6IXbCvYmn4AEgR+QFnRA7FDwAxJK8oBNih4IHgFiSF3RC7FDwABBL8oJOiB0KHgBiSV7QCbFDwQNALMkLekEl9bcSz8m1q9ebzcObNr+9951ymV/U4dM/+8qeefH5l50H8nzcKqUaJ2xQr6GczHsoeACIJXlBz0valXVQFWinXkmDctGLzhq9N6Dg9ZqSA+qq/15w3sVmstoFL5+AeRo6x3forKMm9eD1MW/4nXDtqvXqAzeptV9te1Ouz0soeACIJXlBL4QM6P9Q88NblPv0opr8euYcs6kKvkHdhnq9HdXrA/o/bB+1fetOPTAFL6v6vLMv0PPO2cx6OekZZ2X2BxZaKHgAiCV5Qc9j1P3ulI+mqcGJnU656vJry316UTex2fS7g6+dqndfvwfso3Zu/1kPnDv4114dl2Uf+8072bxxqzyh87RN1JM3e3U+nfaFXJavUPAAEEvygp7HqG6bPOltPZj60Sd64PSiGu/e9d/Ox5/csnmZnvEreL24V88+P3y/qE6t+uYkKVHw/+jbz6lezyYOmJfLPpny2cMDHs3mQLlLzuQxFDwAxJK8oOcxpuDtGTPWX9jufcvterOsRRu9N6DgVe65q3/7Nh31GwM6suBT4t7ar2L95p01RzVrpQfHHH1sxgOdXcuXrgxYnPtQ8AAQS/KCnvusWrFGV6yTclF+nlH3yt99s0DO79q++6eNW35csOjdt99//NEnLu7STZ1t0Y9LUl4F3+rIox97ZIg9I0+o0qHdcTqHNm524P51zKZ9oPkUROX0k88cO2Z8wAlVNm3YbH/UJQfUlWvyGAoeAGJJXtALKrIX7S40kXfwaqZJ6aEtjmh9cufT1IJxr41ft2aDOYNd8GrztdFj9WDblh1m0jmhk0EDB1fpp9rmzflWTsYiFDwAxJK8oOcr8jvdyrMoWpWylm1lwdtxTmIXvBq/+spr9i69OOPjBhS8+bTDiVypnrmdWvvVdmbkIbkPBQ8AsSQv6PmKZ8HLyOJMed3BO4c4m6bgt/y0zVm8dtXef0In4zMJKHjPZDxhOf/QDQAgRPKCnq9kX/ByMjgBBe+XajxKcLI5IQUPAAiNvKDnK5Mnve28Qa1z+iln2cvk7buOPKHJwAf/+nE1fQYKPvtQ8AAQS/KCTogdCh4AYkle0AmxQ8EDQCzJCzohdih4AIgleUEnxA4FDwCxJC/ohNih4AEgluQFPfRs37pz4/qf5HwNM+SxoXqQ8RvU1YIZX34t5/0S/FP1Ouqcb7w+Qc57JpvFwR+F/c/fZrM++zUZQ8EDQCzJC3pVk/HH1Z584mnPec9c0vWy4MVmrxzMnjXXeTJ6V8qn4O0HSu37/XXllQveOZv5FXapTJ39zuR/jxrxqlwsn2S9kgZ6jTyJidzrPHk7nmuqHQoeAGJJXtCrGtUi77/7HxM9eeH5XZ3WMfnwgynyJPbZgmvJ7JUDGb0r5V/wdjwL3ln/xONPmnFwwddO1bNP7rf4sUeGNKi798ffPT8K/Qv0nOhdnutVTul8uhn7ralSKHgAiCV5Qa9qMrZI1wu6Z1yjc+D+dZo2OmzYMy/4rVf9ajec03kyelfKv+DtsSl4fU7562HUpPnnd1L+nW0W22O/xf3+cV/zw1s462XkXjmjQ8EDAPaSF/QqZfOmrRlbRPelnLejbmTVmpNOOFVvTnzjX56lqCbr1Kpvxs5ARu9KVbHgPe/gVy5f3fjgQ+z18unprFuzQe2dP/db/YHr+C3ueuEl553VRZ9Q7jWRe+WMDgUPANhLXtCrlCkfTbNrTC5Qk2Ut2ox8ebTnXrOmTetj5Hxp/cbNDjnSbNZO1dObav3PO8vNCZ0zf/jBFGdXquLTgnolDT6Z8pm90n7mqcCCX7FslfMoKa/OVp9G6FPt2LYreLGamT1rrhocVKdU/0u6qYovxuuvx8vYH5GJvddvXp6qqqHgASCW5AW9qnl5+Eg9UO1rN0qvnn3U5qXdLtebH384VW0++89h8gzZJGXdu6uebtSgqd159kpT8KpT9SDlcwfvl6GDn7I3mzY6TJ1B/9654LfoD23czKl2nf73PDBn1jx7JrWv4NVg8cKleiAP1Gl15NH/HPrswfUaOWfwHPutqXYoeACIJXlBr0lMoxy4f52br79Fj9VnAOabz887q8tl3a+QRwXkxedfdtbrtCvrYA635+07eLPAr+Dlw6Uq//JZtSl/RE3Py4JXWbZkhTyhXJzaV/D2jDybvUv9d9GPSzzX+x3rN1+lUPAAEEvygl6TqEbZvGmrM1mlH5NTN7uHN20u53VUqXvGXlOlgveMfIteRnZ2QOTi7Av+iEOP0l9Z0N/usH7tRrlej7dt2aE+A5g0YfI5Z5xvPieQJ6xqKHgAiCV5Qa9S3pz4lhlvXP+TZ6OEWPCeMSc/cP869UoaND+sRefjT77p+p7Dh720asUavcCv4FPiVjtV+Q7eeYiM2bVjtzxhyip49SQb1G1Y1qLNGaee3e/OeyeMm6TnPR9CtfvTTz5rz5hv9PNc7ySbNRlDwQNALMkLepWiKqTWfrX/NXHyoIGPq/E/+vZTk6qhZcPZUS0uT6VTk4L3Syqw4OWkTJbLyvcVvJzPmKoelc36bNZkDAUPALEkL+hVzadTP2/bur26JfX8/rKqJvcFf3yHzjJymWfkCXXByxPKc8qHkJMByWZ9NmsyhoIHgFiSF/T8ZsRLo674+9VyPiDXXX2DnLRz+aVX/bhgkZwvqKgnKScDcuH5XeWkE/MjDDUJBQ8AsSQv6ITYoeABIJbkBZ0QOxQ8AMSSvKATYoeCB4BYkhd0QuxQ8AAQS+ryLa/phJhQ8AAQV3Q88Uu47Z6m4AEgx9R1nBAZ94VSYxQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPAAACUTBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPAAACUTBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPAAACUTBAwCQQBQ8AAAJRMEDAJBAFDwAAAlEwQMAkEAUPAAACUTBAwCQQBQ8AAAJ9P8Be2uMMdy6/dQAAAAASUVORK5CYII=>

[image3]: <data:image/png;base64,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>

[image4]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAqAAAAGRCAIAAAA1kJYHAAAsMklEQVR4Xu3diZsVxb3G8fs3qDnIvgq4gSiiIFeCWxKN+4oY9Zq474gaNYnXNbijBI0brgRFVIgiSCKuaIyoMUbFbUADKJvIFtnk3HLqUtRUdfecOd1d09X1/Ty/Z57u6urq4XRR7/SZGfivKgAAKJ3/MhsAAID/CHgAAEqIgAcAoIQIeAAASoiABwCghAh4AABKiIAHAKCECHgAAEqIgEfZnH78BRRVR5kzCfAcAY+SsNdriqqvzLkF+ImARxnIdXnNqu8oKn0R8ygHAh7eI9qpzIuMRwkQ8PAb6U7lVAQ8fEfAw2O8M0/lWmQ8vEbAw2OkO5VrEfDwGgEPjxHwVK7Fd+LhNQIevuL9ecpBEfDwFwEPXxHwlIMi4OEvAh6+IuApB0XAw18EPHxFwNdYN1x/k90YWZWt2tqNdp1y0ql2Y1mLgIe/CHj4KqiAH37ciSJ9I0vvJlv67NjPaLQH1BvF9gt/edHuGXehyAHLWgQ8/EXAw1dBBXxc6VnbqV3XD//1sdjo0rH7Hv0HRfbRG/WKDHijf+R26YuAh78IePiKgF8Tn7tx25GNesDLMjpPe/Z5/asBu0OJi4CHvwh4+CrYgJ//xcK/vf7WU5Mmjxk9VmXtkq+XxYV6ZB7HBbzdU7avWL4q8tzSFwEPfxHw8FVQAa8enXtvt+OhBx1x8Yhf33f3uIkTJqmsfXnmq0Zm65UwoKyEgBeNvzz5NKPF7lbWIuDhLwIevgoq4CPrhutvalfpKLdb+gQfWZeOvFzfHX3rGHGu+iH83fvtIav2AUtQBDz8RcDDV2EGvJHc4iE+7lDktl7q8V0vdfSTOZ+Jss+SJ9qNZS0CHv4i4OErAt6oe/94f9eOPT7+6FPR543X3qzlFKO6dOhutOzZfy+7W+0DlqAIePiLgIevCHi7Rl1347bbtF+2ZHktp9iP73ZPuyW0IuDhLwIevgon4Pvs0C+57FP0igvpuPaW9il3EfDwFwEPX4UT8CkrLqTtx3dRg/fcu9k+cQOWsgh4+IuAh68IeMpBEfDwFwEPXxHwlIMi4OEvAh4eI+CpXEt+EWlOO8ATBDw8RsBTuRbpDq8R8PAYAU/lWgQ8vEbAw29kPJVTke7wHQEP75HxVObFd99RAgQ8yoCfqKcyLNId5UDAoyTkokzMU3WXmkLm3AL8RMCjVNQaTVH1lTmlAG8R8EAoCDAgKAQ8EAoCHggKAQ+EgoAHgkLAA6Eg4IGgEPBAKAh4ICgEPBAKAh4ICgEPhIKAB4JCwAOhIOCBoBDwQCgIeCAoBDwQCgIeCAoBD4SCgAeCQsADoSDggaAQ8EAoCHggKAQ8EAoCHggKAQ+EgoAHgkLAA6Eg4IGgEPBAKAh4ICgEPFB+Mtr1MnsAKB0CHggC6Q6EhoAHgkDAA6Eh4IFQkO5AUAh4IBQEPBAUAh4IBQEPBIWAB0JBwANBIeCB8tN/wo6YBwJBwAOlpSf6soUr9SLpgdIj4IFSSQj1uCLsgVIi4IEyqCPXjdJHMEcH4CECHvBbyly3i6QHyoGAB7yUea7bRdIDXiPgAZ84yHW7SHrARwQ84IFWyXW7SHrAIwQ8UEQFSfTkIu+BIiPggQLxItftIumBAiLggdbnaa7bRcwDxUHAA61Gf/C1w9LrIumBVkfAA66VONftIumB1kLAA44EletG6X9283UBkA8CHshRyKEeV4Q94AYBD+SCXG+2SHogVwQ8kBme1+srkh7IAwEPZCDvXK9s1dZuzORo7aWPI7btsk9paRHzQIYIeKB+eeR6ZGrqGwlHLx1xRcLRhKvo/UXNeGam0RI3To1HW1o80wPpEfBAi+WR65Flh3RkjiY3Rh61S+8mtxfPX243qm277DHTFzEP1I2AB2qiP1PaOZRT2SEdmaPJjZFH5308f9bMNyc+8tRtN4y56NxLjztyuOr2yxNPe/7PL9jntkrA60XYAy1CwANJWiXU7dS0N0Rde+UoWXa4dm7f7d2/va8PaHS45re/f+zhSaqPqI5tu8iNgf0HL5n/rdwevMcQ+5Np9SLpgVoQ8ECEVnlelxWZo5EBbx+V9c4b7+stkafIunP0PWr71JPPlBtnnXre5IlT7XP1zyGu9MEdFEkPJCDggS1aMddVRcakHq4JR2vZ1Us86Kvt92fPsU/RzzW27VJH3RdJD9gIeKAQua5Kj8y223Q485fnyEb7qKgzmh69/66H7KC1W1TpAa/Xzr13keO/OP1V1ZgwTrNHnRVJDygEfDkN6DnELrMTWuNb7PVVcnyqo9f+blTCUb0lruzTI8exT0w+132R9AAB76X9Bxw849kXjBKNZj8NAS8V6mG99kqOzzRHay8j4PceONQo+5QilH7HzdkAlBoB76XILNcb7cd3At7HXKcyLJIeoSHgs6GvHTmVfrlaAn75N98apfV18Qm3btl/THvFp8Ise54ApUTAZ8BOlzxKv6LI8huvGm1UZOrHsccvWel/RnuJpyhjtgDlQ8BnoGhrhP3mfODv0pP0lCpyHeEg4DNQwMXiyT9NscvsFCTCPsAi1BEmAj4DBVw47Id1uyVk+opv50HBK6ufir/g7JF2Y4017KgT7MaiFbmOwBHwGXC8gnw5799XXHBVXMk+dpzbLZAKnvRtf9TB+EVzI+Bvuv42o+6780FjkPEPTJQj7NS7r2o0fu3NOEWvyROnynpywpQP3vm42f6tXuQ6UCXgM1HApcT+7jsBn6yYMT/yvEvP/NW5alfGarPhOmjA3kZLm63byY1nn5we+YVC8pgv/+V1WUccfPTUp55vtn9rFbkO6Aj4DLCmlIkKiSKEfd8dd5s7Z77aVQEv/xM5u7+sHw/ez2j5at5StV1HwNvdauzvpsh1IBIBnwEWl1IqQswv+vIbFaV7Dxy6794HLKshXI885Bij5ajDjlPbkSH9yP0T7DIGiTu3tYpcB5IR8BlglSk9lSWtm/eyLr/4d3aj/t/G3H7TWOOo/A9kRR17xHDVqEJ65rRX4kofZMJDT6j/ca61Al6/EeZNAtAUAZ8BlptwuI/5xf9eLrNZL7ubHvDzP19kd0ioGc++GFeqzyf/mqtfN/JzyK8IdaAOBHwGWHoC5D7pVUX+H6+RjUbpqTzujw/rh4Za37bX66xTzzMS3U3AqxeZv19AHQj4DLAAhcxB0ic8wb816x+yOrbtorZlGYP8esRv1Il33X5v7YE94aEnDjzgEKMxoX/6ItSBTBDwGWAxgv6saSdWykpIU/u75pHfPn/3bz98D37KE89t32MnOeBOvfuefsrZtVwislrav5bSX0Pz9QXQcgR8BliSoOSR9PYTvP2b7sml3sC/8vJrVDafd8YI9aA/dPB+dtnjqMow4Ml1ICcEfAZYmxApj7AvRxHqgAMEfAZYp5CMpFdFrgPOEPAZYMFCjcJMev1Pbb4iAHJDwGeAlQstFUjSk+tAKyLgM8AShrqVMunJdaAICPgMsJYhvRLEPLkOFAoBnwEWNWTLo7BXnyp/BYCiIeAzwOqG/BQw7Al1wAsEfAZY6eBAqyc9uQ74hYDPAEsenNFT1s7gPIpcBzxFwGeAtQ+tItekJ9cB3xHwGWARRCvSk9jO6ZYWuQ6UBgGfAVZDFETdYU+oA+VDwGeAlRFFU2PSk+tAiRHwGWCJRGFFJj25DoSAgM8AayWKT0968xiAMiLgM8CiCS8wUYGgEPAZYN2EF5ioQFAI+AywbsILTFQgKAR8Blg34QUmKhAUAj4DrJvwAhMVCAoBnwHWTXiBiQoEhYDPAOsmvMBEBYJCwGeAdRNeYKICQSHgM8C6CS8wUYGgEPAZYN2EF5ioQFAI+AywbsILTFQgKAR8Blg34QUmKhAUAj4DrJvwAhMVCAoBnwHWTXiBiQoEhYDPAOsmvMBEBYJCwGeAdRNeYKICQSHgM8C6CS8wUYGgEPAZYN0EABQNAZ8BAh4AUDQEfAYIeABA0RDwGSDgUXBvvvbWv+fNV7sDeg7ZcgxASRHwGSDgUWQizud88MnER55SuU7AAyEg4DNAwKOwTh12zltvvC23xcZ5p4ysNgb8/WMf/vD9OU26AigXAj4DBDwK63+OPuO9d96X22Lj7JMurPIED4SBgM8AAY8iE3G+ZPHSF6a/xFv0QFAI+AwQ8PDLXbfdZzYBKB0CPgMEPIpMPK/LMrYBlBsBnwECHsWnAl59BFBuBHwGCHgUmfEErz4CKDcCPgMEPIpMxvm/v1jwwT8/IuCBcBDwGSDgUWQizvfcfuivjj17yaIlfA8eCAcBnwECHgBQNAR8Bgh4eIGJCgSFgM8A6ya8wEQFgkLAZ4B1E15gogJBIeAzwLoJLzBRgaAQ8Blg3YQXmKhAUAj4DLBuwgtMVCAoBHwGWDfhBSYqEBQCPgOsm/ACExUICgGfAdZNeIGJCgSFgM8A6yYKTk5RvcweAEqHgM8AKyaKj3QHQkPAZ4BFE8VHwAOhIeAzwKIJLzBRgaAQ8KnoT0Wsnig4pigQFAI+LdIdvmCWAkEh4NMi4ItDvxcUVV+ZswrwFgGfFutCcdiLNUW1tMxZBXiLgM8A60JBcCOQEvMHZULAZ4BcKQhuBFJi/qBMCPgMkCsFwY1ASswflEkuAS/XWarcZd71AijsJwZfMH9QJnkFvNmEcinmLSbgkRLzB2VCwKMexbzFBDxSYv6gTAh41KOYt5iAR0rMH5QJAY96FPMWE/BIifmDMiHgUY9i3mICHikxf1AmBDzqUcxbTMAjJeYPyoSARz2KeYsJeKTE/EGZEPCoRzFvMQGPlJg/KBMCHvUo5i32OuC///57s6nAVq9evX7derM1Z5s2bdqwYYPZmil/5w9gI+BRj2Le4rwDfvZbs+0S7S+/9Irqs2D+ggvOG6GXOnTi8JPVtu3Wm28zm+Jdf+2oylZtjcYHxz0kGlVde/V1Rgfpyt9etWrVKrn91Vdfi125bQ+YQHS+cdTN+m5kaWc0oR+S24sWLWq2/3NTpyX0yUSu8wdwjIBHPYp5i/MO+HPPOl+WiBm1XW0a8OLRdtZrs2RNfnqKHkgq4O0gFA+mcQFv9Pzyiy+rUQEvdrt06K63DNx9r8g4FI0iTeX2+//8l+oT2bna9BPQG42AV9u1MIaqNg14/YqyUX4k4IEWIeBRD2e3+KgDTjCb4uUd8IoRM3rA67bvuVPPbr0HDRgsK+4JXo6WEPBmU0zA67vJjVdc9lsxgqgLz79Ij9WmHX9w1OHH9OjSS24/cP+Dj014XG5X6gr4jz78SMW2LPGayHPtJ3h9V24T8ECLBBfwYoF4cNxDZmtT9lqj+/TTz+Y2zK02t6jN+WhOtbk+/kp5iwf0HGI2bWYc2nP7ofpuMjcB//XXX4vbKu+vFBnwS5csNe5+3gHfuX03vSXhCV78FXj6ycmibr9tjOpTaUzcd9/9h9E5crdiBfyiKKpDAjnmIusvnb4rtwl4oEXKHPBywVKlGlXAb9iwIbKPsdbMmD6j0vioIXc/+eTThoa5VWvtM4jFqBrTJ65x1muz1O4B+/xUPfkZpZ3UatLc4rmffyFS/N233jMPNKo94O04t1vyIG+f+Lhu7TrZYgf88m+Wy25dOnRXsyvXgBfu/MNd6lqiLrvkcqODVGnJW/RGo95ZD3h9fopDCXN106ZN6jOUL6Ac0/hLt3HjRrmrOlcJeKCFyhzwOn1h0gNePMFs6bSZsdb07NZbfHx91huyMTngVWPKgI8Tea57dd/iKy686pifnSg2Tjv+XPs5fthBJ+8/4GC9pdmAV6VazH7ZUUkjbde1l9zVA172mfbcdNWiGAGvz8lqY8DLc1evXh3ZTRcZ8DWSV9FLtTftGNGod9YDXhc5jqIfldv2Z1Jt/O5Gh7ad//Of/+g9CXigRQj4ZgL+5F+cotplYyYBH1l+BXwtpZ8y54NPRKI/N/l5vVG0zP7bO/qu+igZAW9fwi69f7bEY6XZFMN+30XUpRdfpvdRt/KpSU9X0z3BD9htYEK99OLL2qmxIi9kNKrdihbw+w/9SUJtObmRPqDclh+Nr6rF9tq1a/XLVQl4oIXKHPDG24aysRIf8OKJoWI9TNjrUULAj7l97C477Sq3kwPebGpsLHfAJxMxr3J96ZJlartQAS/Zt0D+slyz4p7gpRoDXsTe+nXr0zzBK+Pue+BnB/x8772Giq884n4L/7ijj+/WaTu5fc8f75008Um5XYl6gp8xfcYxRx43ZPA+d991j3FIUZ+2/iZ81Qr4Iw49Wh66Y/QfVB8CHmiRMgd85FpQiQ94RV9r9EHEtnge6rPDLg0Nc41DUkX7+iCrgNc7NzTMjTzXvTpuscjsuBJHV69q8r60Ustb9EaL1iUXFe17zLL69elvdooiA158gdi+TSd9qshKCHi92mzd7u9vvmUHvD2gLL2PIr8Rfu3V1y1bukx8ufDsM1Mrjb/4Z/ZrFDlapWnAvzjzJdFy8i9OmTd3nhjwphtuFrt77DZQdVBq+R68cSHx9YdsIeCBFiHgmwn4dpWOql02xj3Bi9VNb0kO+MhqNuDluqkdbDUpb7H93XfpiP2GGS3JAW+32I2Zs2+B/QRfsb4IsN+iN8QFfKTIgNd3ExqFtj/qYDbFd45UsX6K3v7+RY0Dym76Xzoxz5v02NyHgAdahIBvJuAnPz1l8eLFcls2Rgb8n6c8Y1wuIeCVu++6J7lDtekI7bftdNGFF2sHW03KWxwX8HHtNSpUwBstzXIZ8Pf88d7DDj5Sb2lomBvXOVKlacB3atfV+CW9yy+9osYBZTf71+R08hABD7RIyQPeKNmY8GtyYp2qWmuNOrpg/oJqVMB3aNu57479VH8pj4AvjpS3OC7IRfvcz78wyuwUz1nA21VLH7ubLn3AR5bex3DFr3/To0sv8TR/xKFHL12y1DycqBL1PfixY+7s16d/+zadRo64xDjUrOSAlwh4oEXKHPCRKqn/oRs74O03J6vxAa+/Z9urxw6Vpu/lyh9ispdpo4wx3Ut5i+MC/tWZr9tldornJuBRYswflAkBH6GlAR8pLuC//XZFQql/O6XginmLCXikxPxBmRDwEZIDXsmqj4+KeYsJeKTE/EGZBBfwyEQxbzEBj5SYPygTAh71KOYtJuCREvMHZULAox7FvMUEPFJi/qBMCHjUo5i3mIBHSswflAkBj3oU8xYT8EiJ+YMyIeBRj2LeYgIeKTF/UCYEPOpRzFtMwCMl5g/KhIBHPYp5iwl4pMT8QZkQ8KhHMW8xAY+UmD8oEwIe9SjmLZYBT1FpypxVgLcIeNSDW+wjAgwISl4BT5W+zLuOwuPGAUHJJeABFBABDwSFgAdCQcADQSHggVAQ8EBQCHggFAQ8EBQCHggFAQ8EhYAHQkHAA0Eh4IFQEPBAUAh4IBQEPBAUAh4IBQEPBIWAB0JBwANBIeCBUBDwQFAIeCAUBDwQFAIeCAUBDwSFgAdCQcADQSHggVAQ8EBQCHggFAQ8EBQCHig/Ge16mT0AlA4BD5QfAQ8EiIAHgkC6A6Eh4IEgEPBAaAh4IBSke3HY3zShwixzZmSKgAdC4WBBQY24EajmPw0IeCAUBHxxcCNQzX8aEPAoD/vtL4pqaZmzKh/OLoQiy3saEPAoD5cLNErJ2fxxdiEUWd7TgIBHeRDwSMnZ/HF2IRRZ3tOAgEd5EPBIydn8cXYhFFne04CAR3kQ8EjJ2fxxdiEUWd7TgIBHeRDwSMnZ/HF2oVZx/bWjKlu1NVtjNDTMffrJyWZry9V+xeLIexoQ8CgPAh4pOZs/zi6UhojMuDJ6nnn62YcdfITajQv4YccMl+0rVqwQG8OHnViNCnj7cpEXTegp6vbbxpi9iyfvaUDAozwIeKTkbP44u1BWIvNVqTHgjUa522AFfKTIAX2X9zQg4FEeBDxScjZ/nF0oK222brd9z53M1s1Euu++655qNyHgb77xFrktNuIC3n4cl6X3UUR72x91ePWVVzds2PDRhx8NGjA4rmcB5T0NCHiUBwGPlJzNH2cXyoSMTPFx1767m8caGQEcF/BCt07byc7dO/eULQ0Nc43T486NZHcWLSNHXGI0FlPe04CAR3kQ8EjJ2fxxdqE0vv/++87tu4m8XL78W9ny0/0PErsjLhjZtGN0wIsHa1Far/8nDr377j/UbkPUE7y+m0x07tl9+4ULvxLb69etP/7YE1p0euvKexoQ8CgPAh4pOZs/zi6UxrbbtP/7m2/JbT01f3/dDWpb6NGl1wXnjZj23HTVJ/IJXnyVMGjA4H59+suvBlQ1RAX8jr362KX30X34wYfiKw/x2Q7cfa/JT00xDxdY3tOAgEd5EPBIydn8cXahrNiBLd0x+g/qkNj47LPPqzEBH6fBCvhq41Azps9Q2w+Oe6jp8R989OFHyWWeUDx5TwMCHuXhacDHrV868YAyfdrzZqvmnbffue/ecWZrOrfefJvZlMJ77/1zwp8eM1sLxtn8cXahrEQGdoe2nY12uZsQ8PsP/YnR0lBvwPfdsZ9eopvRYp5QPHlPAwIe5VHwgK/E/FSwvn4ZHWa9Nku1P/rweLltrGIy128cdbMa0GBfV7+69MD9D9qNkQPa46gyfrKpxl+dKhRn88fZhdKwb7FeZm9Nwr2Oa5eOPOxo+0I1XjT5aDHlPQ0IeJRHwQPeoNajStOA39JDU9ECXvfZZ58/P+2HB52EgI+kd/5i3hf6J6Pamx3wf3939eLFi83WzQj4BM4u1CoS7rVo/+STT+0y+7Vc3BWLLO9pQMCjPAIM+LPOOEdupAl4sb1o0SK1/cG/PrT7RBId9v3x/mbrZmecdtahPz9c7SYs+sWRx/yJHDOysTQS7vXIEZdEltmv5eKuWGR5TwMCHuURYMCr/rUE/HfffScelWa+8OJDDzxsBLzaFuMM3Xtfu9324syXKo3vmq5cudI81miXnXdrV+modhMW/eLIY/7IaWmMnMeF4J28pwEBj/Kwl9FCkXGol2qPC3j9B4jsgF+/fr3qLwNe1OrVq/U+9907Tra3b9NJPG2ffca5Y24f+5cZf9UvpG/LQ8ZnaBs+7ET987/26uuaHv//dn0EGfCVpr8DXTQqjB2UeW2EJ+9pQMCjPAq+bsblZSU+4JVKVMCLxuemTpPbtTzB60Teq239xGaf4Ht26y3aBw0YrDf22WEX0dh+2y1jbtiwITLg1W4x2TGcX5nXRnjyngYEPMqj4OtmXLzVEfCbNm0SLfq/6dFswI8dc6fq8O47705+esu5Ipiv/O1VclsfJHlA4e677jHeMFDUuWrDl4A3m1KLTPQ8LgTv5D0NCHiUh72MFoqIN/H427FtF/loK0u26wG/7Tbt9Q7r1q6T7Srgr7ryGrH7wl9nbh74By0KeJs49I9/vHfRhRfrT+EJ/aW4gBcnvvzSK3Jb/tOhq1atCjngzaaYRoQm72lAwKM8Ch7wcfSAj6MCXv5H2ubh1AEvdO3Y48jDjtZbkvtX4wPe+N25BQsWVgN+go/k7EJekzMnvchZWgR5TwMCHuURQsDHiQz4Y448zvhXcYwy+hvsAffsP6hHl14JZfwr5ToCXpfVhcY/OmHQgMFGiRsxaeKTZtemmv2CT2f0FLs1Rq99or4b529vvCk3jP6vvPxq+zad9t5r6Hfffacaaxkzso/9usmXzuwXZe3atWZTXbKaBnEIeJQHAW+2ppPtgAS8LtcLiddZ/3eLB+w2UJW6BZEBL3Kron17SHUwelaaC3hjEDWUfcWq9gsgqltkwLfZul3/fntUN/8Aivj87T7nnnW+Gkf+A1B2n2RGz8iXrkrAA+55GvAoDmfzJ9cLiSh64vFJZmsjPUqbjT3VwehZaS7g40ReUQS80RIZ8Pq2/B0Nu72W7WQJPfXXjYAHXCPgkZKz+ZP5hUTq3H3XPWpbfxNbqWj/s3vkE7whMkTlbmTpb0Rt17WXaDnkoMMqjT83qk5UHZQaA/6usX8Uu+efc8GufXcXG99//73RR4TuQw88rPoffOChp/7ydKNPJP1oXM+K9tJVeYIH3CPgkZKz+ZP5hSpawNtGjrhEdNi4caP69wlkmf00e/YftH79erldS3+DkZofz/nYaFQiA772K6o+Tzw+SaW+8PhjE9Wh5HGSjy7/ZnnFeukIeMA1Ah4pOZs/mVxI5U1cVRt/o0FsdO/c0zg3+Qle/0cSq1ZaJ5T61kDk6ZFXjAx4uaH679S7r32tO/9wl95nzkdz3vr7bLktXHrxZfsM2U9u29e1R9NL/idJ113ze7lrnFvlCR5wj4BHSs7mj7MLCUuXLDWbmgt4ceidt9/Rd7WDNVm48CuVl+ofJ44cp5aAV0SL/M/jH5vw+PXXjjJ+crNLh+56z08//Uxtq/baJfxyHQEPuEbAIyVn8yfDC4kHWRWlsjZt2mT0MTqoMroJb89+W7SPf+RPeqPd8w93jNXH2Xn7XYwOkexxqo0BL/Jy2dJl4tJX/vYq0Scu4OUP+cuAV/Q+Ylt8YSE2nn1mqtGutnXGS9epXdcaXzoCHnCNgEdKzuZPVhcaMnifBfMXGI0igUQAG42GyCf4XfvufvghRxmNVSsgxe577/1Tb5GNzbbUKC7gaxnw99fd0GbrduMfnaA3Rp4oGiNfOqMlEgEPuEbAIyVn8yerC+2y8272D8yLhLv+2lFGoyEy4OPYQTtv7jy9RTYaj7+1j29IE/CRIk+sRP2ugXjpjJZIBDzgGgGPlJzNnwwvVGl807hj2y4Dd99L/kcGO/bqY3aypAn4jRs3yovustOu8pfWRJ1y0q/0PtXNn5hRP93/IKOb7e3Zb8sN47r2aLL0PpEi+6xbu06evvP2u/Tvt4fcruWlqxLwgHsEPFJyNn+cXQhFlvc0IOBRHgQ8UnI2f5xdCEWW9zQg4FEeMuApKk2Zsyofzi6EIst7GhDwAOBa3is7vJD3NCDgAcC1vFd2eCHvaUDAA4Brea/s8ELe04CABwDX8l7Z4YW8pwEBDwCu2T/cR4VZ5szIFAEPAHDNQbyBgAcAuEbAO0DAAwBcI+AdIOABAK4R8A4Q8AAA1wh4Bwh4AIBrBLwDBDwAwDUC3gECHgDgGgHvAAEPAHCNgHeAgAcAuEbAO0DAAwBcI+AdIOABAK4R8A4Q8AAA1wh4Bwh4AIBrBLwDBDwAwDUC3gECHgDgGgHvAAEPAHCNgHeAgAcAuEbAO0DAAwBcI+AdIOABAK4R8A4Q8AAAd2S0qzIPIzsEPADAHQLeGQIeAOAUAe8GAQ8AcIp0d4OABwC4RsA7QMADAFwj4B0g4AHANeMHzahgy5wZmSLgAcC1vFd2eCHvaUDAA4Brea/s8ELe04CABwDX8l7Z4YW8pwEBDwCu5b2ywwt5TwMCHgBcy3tlhxfyngYEPAC4lvfKDi/kPQ0IeABwLe+VHV7IexoQ8ADgWt4rO7yQ9zQg4AHAtbxXdngh72lAwAOAa3mv7PBC3tOAgAcA1/Je2eGFvKcBAQ8AruW9ssMLeU8DAh4AXMt7Zc/Khg0bzKYYCxYsNJssq1ev3rhxo9kasLynAQEPAK7lvbIb1q5dW9mqrd4idme/NVtvMcj+xlkJaukp+rz80itma7W6YsWKPfsP6tKh+/hH/iRbGhrm1jJgMvuPrO9GEp9e8suSrbynAQEPAK7lvbIbvv76azvtVJL13bGf2NVLdlAfdUZPUbfefJvR8/1//svuJvvYAb9p0ybR/u23K8T2PkP2kz0brIC3B1TDGuw+slvCgF9+8aVsJOABAKnkvbIbZIyJKNVbjCQbftwvTv3l6Wo3MhQl0bh27Vq5oTdu6dHU+EcnqNHsgG+zdTt9V/ZssAI+UlyfIYP3UeH9/LQZstH4bJcuWarvyg0CHgCQSt4ru0EE2OLFi42EM5Ls5z875NijhqldFclbemxWaWHAj77tjp7dt6/GBPyzz0y9+cZb1K4cp8EKeJnWdul9pDVr1kR+Yvop9uByg4AHAKSS98quU+l1y023iu2HH3xENhpJpodfXBBKlZiA37FXH1GqZePGjbLbL44/6cjDjpZ97IAXunXaTl1U/lhfQ2PAi+q7Yz/ZJ/IziSM6r1+/XmxMn/a8OtH4bBctWqTvyg0CHgCQSt4ru3L2GededsnlesuF519UTQx4tas+GioxAb+lR6M/T3lGDfLow+PlRmTACy/8daY+QkNjwG85HDV+smHHDBenXHf19apFH+GbZd+I3WuuulZ8PmJj772GynYCHgCQSt4rex3abN2uZ7feTzw+Se5mGPCrV6+WLXbAL1/+7V57/He/Pv07tO1c0d54b4gKePkOgVGvvfqa3m3Dhg0LF341b+48+YN+o2+9/YTjT9x2m/ZyBL2ncPghR4kRRH/VQsADAFLJe2U3DBow2KhK0yd4+51wuWGHomxsUcArFSvg4zRYAS/pjQ+Oe0g7ssXxx55wykm/GnHByFtuuvX1WW+oHy2MHNBAwAMAUsl7ZTdUtIdjVSrJDtj3Z8uWLlOdzznzPHmK+qj7/PMG8ci7bu06o90Ie1m9euww7Jjht958m3ielu2RAW9fZeXKlRP+9JjRWK0t4Ksxf177KnaLY3lPAwIeAFzLe2U31JFk8pTaT6ylZ+0Bb7CjWq8Th59snhDFvord4lje04CABwDX8l7ZDXYoyjL7aeTR5D66WnpW4gM+ssx+6dgD2lfM47oJ8p4GBDwAuJb3yg4v5D0NCHgAcC3vlR1eyHsaEPAA4FreKzu8kPc0IOABwLW8V3Z4Ie9pQMADgGt5r+zwQt7TgIAHANfyXtnhhbynAQEPAK7lvbLDC3lPAwIeAFzLe2WHF/KeBgQ8ALiW98oOL+Q9DQh4AHAt75UdXsh7GhDwAOBa3is7vJD3NCDgAcC1vFd2eCHvaUDAA4BrYmWnqNMJeABAyTiINxDwAADXCHgHCHgAgGsEvAMEPADANQLeAQIeAOAaAe8AAQ8AcI2Ad4CABwC4RsA7QMADAFwj4B0g4AEArhHwDhDwAADXCHgHCHgAgGsEvAMEPADANQLeAQIeAOAaAe8AAY+SkOsFRTkrcwqiJXgNHSDg4Te12q5Z9R1FuSySPg1eOgcIeHiMXKdavQiq+vC6OUDAw1ekO1WcIqtaioB3gICHl0h3qlBFXLUUr5gDBDz8I5cGe5GlqFYsEqtFeLkcIODhH9KdKmaRWLUj4B0g4OEfAp4qZpFYtSPgHSDg4Rnen6cKW4RW7XitHCDg4RkCnipsEVq147VygICHZwh4qrBFaNWO18oBAh6eIeCpwhahVTteKwcIeHiGgKcKW4RW7XitHCDg4ZlCBfyMaX+tbNVWbMiPoho+m/faK68bJQ+pPqLOPfN8VScN/x/VYfhxJ9pXqaUeGz9RH7+OEp+nPsLu/faIK/tcWeL00bfcoe8aRyPrmclTI4eqsdGuGrvlUYRW7XitHCDg4ZlWDHg7nOyAf2bKc2eedo4o0SI3RKnT1VAv/OVFVaq9EhPw+hUPPvAwfVf1aVHA6yOIWvTVkjVWwKtavfI/ke2qJj3+lF1rrKBNHsQo0XnP/nsZZQ9olGq3B3RThFbteK0cIODhmVYMeFXqmdsOeFWiZcXyVUaL0UfUsiXLVZhV4gNebYuAF2X3aVHA66XOigv49m06ta10OOTAw+1Dqm64/uZmgzZy8LiK7BzZKOuO28bGXddlEVq147VygICHZ4oQ8McddbzcSA74tj/qYLQYfWTjh+/PUdt1B/xD4x4R3Z6eNMWo99/7wO6slxo8MuA7tu2i/oDiSxD7dGMQfbti5f28hi/tWvz10uQBkxtlnXPGeXv0H9Rst7zLDi27BRKvjAMEPDzT6gG/euV/OmzbWW7HBbzosGrFGtF42ilnqEY7eETLqOtulBuy6g74d2e/d93Vo+x68a8v251V/eX5mWpwI+DbVTqK3Usuukxsy++sy+8O9Oqxgz1O9849t92m/fhHHht96xg1iPHnVe+079S7b0V7B/7Wm0bbA6oXxCi7p6xO7bpe+Zur1Ll2Bzelh5bcJsbi8Mo4QMDDM60e8IMHDlERIgNexJtqad+mk9hWsXrf3ePE7srlq9dowXPgAT8X24cedIQxciU+4FXFfQ++vtJHiHyCt7slVKXxh+xm//2d31x2ZcJZT0+aEnfIrhuuvymy8yknnSpeq8N+fsSQvfbp2W17/QWJ7O+m9FCnainzrzcyRcDDM3JdsNdWZ1VpfO99+tQZa+Kf4Gsp+4fIRF10wcV2z2ZLxVtc2afIs6763TVqVw94e4SEAcWrIR6ge3XfYeDug4cdc8ID9z+sBlF99h2yf0IZD/H25fQSkb+m8WcOnpk89ZWXZhmfjHFdx6WHVo0xZmdeOGW+FsgaAQ/PyKXBXlvdlHiA3q5r7zWbUyQu4CMzJrLR6BD5BG/0sRvrKDHOAfv+TG9JeIJvtpYvWzFm9Fi77J4ilfcb+pP/HvTjC88b+e03K+0OLS37c7ZbnJWRWyQZWhcBD8+0YsAfcuDhKjzks2NCwNtP580GT5qAv+Dci/rs0M9ut2v8wxPEIPI32fQyAl4+KKcp41OdPnWGaPnFsJMXfbVERPuUp56pWG8G6OdGVmTPZlucVWSc2y2AGwQ8PNOKAa9+3F1VQsDbp9t97C8C7Lfox937oF7iLKNFdqs94MXT9spvf/iZAKNSBrwdxvaf176uaPzz08/ao0VWLa9qZIuzigx4oLUQ8PBMKwa8XSkD3u5j16UjL08u2a32gI+r9AFvN+rVqV3Xzu276S3iUb7Zs/SK7Gw32i3OioBHoRDw8EyhAl6VHd6R1dI+tdfC+V9//slcu+Z/sdDuHFlGwE94dOJRhx9rV+Rvta2J+ePs0HNno9voW+7o2rFHu0rHQw48fM6Hn9jjJFSNL06N3fIoAh6FQsDDM8UMeIpaQ8CjYAh4eIaApwpbBDwKhYCHZwh4qrBFwKNQCHh4hoCnClsEPAqFgId/CHiqmEW6o1AIePiHgKeKWQQ8CoWAh394l54qYPH+PIqGgIeXCHiqUEW6o4AIePiKjKeKU6Q7CoiAh8d4r55q9eLZHYVFwMNvcnmlqFYsc1ICxUDAAwBQQgQ8AAAlRMADAFBCBDwAACVEwAMAUEIEPAAAJUTAAwBQQgQ8AAAlRMADAFBC/weEBXeX36A53wAAAABJRU5ErkJggg==>

[image5]: <data:image/png;base64,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>