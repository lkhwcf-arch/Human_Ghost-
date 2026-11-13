# NPC 데이터 배포 준비 설계

기준일: 2026-11-13. 사용자 협의의 [플랫폼 내부 배치 수정안](../../../DataSheet/Docs/NPC%20플랫폼%20내부%20배치%20구조%20수정%20제안.md)을 기준으로 DataSheet 도구만 준비했습니다. 기획자가 공급한 Npc·Dialogue 시트와 대사 JSON 4개를 검사·배포했습니다. Unity NPC 생성·대화 실행·이벤트 소비·저장은 아직 연결하지 않았습니다. 공통 구조는 [데이터 자동 발견과 Unity 배포 경계 설계](데이터%20자동%20발견과%20Unity%20배포%20경계%20설계.md)를 우선합니다. [기획자 준비 안내](../기획자용%20안내문서/NPC%20데이터%20작성과%20배포%20준비%20안내.md)를 함께 봅니다.

## 1. 왜 먼저 배포 경계를 준비했는가

기획자가 새 엑셀 파일과 JSON을 올린 뒤에도 같은 실행파일로 검사·배포할 수 있어야 합니다. 기존 신규 파일·탭 자동 탐색과 B1 인라인 규칙을 유지하고, JSON 자동 탐색·검사 클래스 연결·공통 경로와 배포로 통합했습니다. 실제 예시 시트나 배포 데이터를 미리 넣어 확정 데이터처럼 취급하지 않습니다.

```mermaid
flowchart TD
    A[기존 엑셀 자동 탐색·B1 규칙] --> B[Npc·Dialogue가 공급되면 CSV·최초 정의 생성]
    C[spawn JSON] --> D[기존 몬스터 최대 수 검사]
    D --> E[Placements 내부 Objects 검사]
    F[dialogue JSON] --> G[파일·대화·화자·대사 참조 검사]
    B --> H[모든 검사·컴파일 성공]
    E --> H
    G --> H
    H --> I[공용 배포와 소유 목록 갱신]
    I --> J[CSV·JSON 갱신 / 기존 정의 보존]
    J -. 후속 개발 .-> K[Unity의 NPC 생성·대화 소비]
```

## 2. 기존과 달라진 점

| 책임 | 이전 | 준비 후 |
| --- | --- | --- |
| 신규 Npc·Dialogue 탭 | 기존 자동 탐색·생성 기능 사용 가능 | 그대로 재사용. 실제 파일은 아직 없음 |
| 스폰 오브젝트 | 몬스터 수 검사만 수행해 Objects가 검증되지 않음 | 중첩 Objects·양수 ID·스테이지 내 ObjectID 중복·Npc 참조·영문 앵커·지원 Type 검사 |
| 구형 배치 | 최상위 Objects 등이 별도 확인되지 않음 | 최상위 Objects와 ParentPlacementID를 전환 안내와 함께 거부 |
| 대사 JSON | 독립 dialogue 폴더가 배포 대상 아님 | 검사 후 Assets/Data/Json/dialogue로 배포 가능 |
| 변경·삭제·실패 | 기존 소유 목록·원복 정책 | 대사도 같은 정책 사용. 기존 정의 C# 덮어쓰기 금지 유지 |
| 원본 미공급 | 기존 배포 가능 | Npc·Dialogue·dialogue 폴더가 없어도 기존 배포 유지 |

## 3. 코드 위치와 재사용 이유

| 위치 | 역할과 선택 이유 |
| --- | --- |
| DataSheet/Tools/DataExporter/Validation/DialogueJsonRule.cs | 대사 파일의 의미 검사와 수집 연결. 기존 JsonAssetCollector가 UTF-8·문법·스냅샷을 처리하므로 파일 입출력 중복 없음 |
| 같은 프로젝트 Validation/SpawnObjectValidator.cs | 플랫폼 내부 Objects의 데이터 의미만 검사. Unity 객체와 프리팹 생성 책임 없음 |
| Validation/SpawnJsonRule.cs | 기존 최대 수 검사 다음 오브젝트 검사 호출. 실제 몬스터 배치 값 보존 |
| Infrastructure/OutputCatalog.cs | 공통 상대 경로·소유권 판단. 새 기능의 경로 조건을 별도로 추가하지 않음 |
| Infrastructure/JsonAssetFields.cs | 기존 정수·문자열·배열·참조 검사 재사용. 배열의 부모가 객체인지 공통 경계에서 확인 |
| Program.cs | JsonAssetPipeline과 UnityCodeOutput을 한 번씩 호출한 뒤 공용 배포로 전달 |
| Tools/DataExporter.Checks/NpcDeploymentChecks.cs | 실제 원본을 바꾸지 않는 임시 정의·JSON으로 계약 검사 |

기존 DeploymentService의 신규 생성·수정·소유 파일 삭제·실패 원복과 `.meta` 보존을 그대로 사용합니다. 별도 NPC 배포기·별도 JSON 로더·전역 이벤트 버스는 추가하지 않았습니다.

## 4. 입력 계약

### 플랫폼 내부 Objects

최상위 Objects는 허용하지 않습니다. 각 Placements 항목 안의 Objects만 사용하며 없거나 빈 배열이면 NPC를 생성할 데이터가 없는 것으로 처리합니다. 내용이 있으면 부모 PlacementID가 양수여야 합니다. 전달된 스폰 파일 안에서 플랫폼 ID와 NPC ObjectID의 중복을 확인합니다.

각 오브젝트는 ObjectID·Type·NpcID·Anchor를 사용합니다. Type은 현재 `NPC`이며 `SHOP`은 이 배치 Type으로 지원하지 않습니다. 향후 Npc 표의 기능 종류인 NPC/SHOP과 배치 처리 종류를 혼동하지 않습니다. NpcID는 공급된 Npc 표의 ID여야 합니다. 표가 없는데 오브젝트만 들어오면 배포 전에 실패합니다.

Anchor는 영문·숫자·밑줄 식별자이며 첫 글자는 영문 또는 밑줄입니다. 실제 앵커 존재·이름 유일성·비활성 조립·수명은 Unity 후속 작업입니다. 같은 NPC 종류나 앵커 이름을 다른 플랫폼에서 재사용할 수 있습니다. 오브젝트 행에 ParentPlacementID가 남아 있으면 거부합니다.

### 대화와 대사 파일

- Dialogue.ID는 양수이며 LineFile은 `{ID}.json`과 정확히 같고 중복되지 않아야 합니다.
- TALK의 TriggerTargetID는 0, BOSS_STATE_ENTER는 BossState ID, BOSS_DEFEATED는 UnitStat ID를 참조합니다.
- `dialogue/{ID}.json`의 DialogueID·파일명·Dialogue.LineFile이 일치해야 합니다.
- Lines는 한 줄 이상이며 각 SpeakerTextID·TextID는 양의 정수이고 Language에 있어야 합니다.
- 대사 파일만 있고 Dialogue 행이 없거나, Dialogue 행만 있고 파일이 없으면 실패합니다. 아직 Dialogue 행이 없고 파일도 없으면 정상입니다.
- `_note`는 메모로 허용하고 원문을 배포합니다. 문구를 코드에 하드코딩하거나 원본을 재작성하지 않습니다.

기존 JsonAssetCollector를 통해 UTF-8/BOM·JSON 문법·경로를 확인하고, JSON 파일명 충돌은 기존 전역 이름 검사에 포함합니다. 서로 다른 폴더라도 같은 파일 이름의 로딩 키가 겹치면 배포하지 않습니다.

## 5. 아직 구현하지 않은 경계

Npc·Dialogue의 형식·범위·일반 참조 규칙은 공급된 시트의 B1 규칙을 사용했습니다. 현재 Npc 1행·Dialogue 4행을 CSV로 배포하고 자동 로딩 계획에 반영했습니다. 임의 예시 행을 추가하지 않았습니다. 기존 클래스가 있으면 보존하며 스키마 차이는 개발자가 확인합니다.

대사 파일을 복사하는 것과 클라이언트에서 대화 정의로 등록·실행하는 것은 다릅니다. 이번에는 대사 전용 런타임 리더나 자동 로딩 계획의 대사 정의 등록을 추가하지 않았습니다. Unity의 기존 텍스트 파일 수집 여부와 실제 소비 연결은 시트 공급 후 확인해야 합니다.

스폰 FormatVersion 정책도 바꾸지 않았습니다. 배포 도구가 중첩 Objects를 검사할 수 있어도 기존 클라이언트가 NPC를 생성한다는 뜻이 아닙니다. 실제 배포 전 클라이언트의 구조 지원과 버전 전환을 함께 맞춰야 합니다.

공용 수집은 동기 검사·파일 배포이므로 비동기나 게임 이벤트를 추가할 이유가 없습니다. ID·정의·배포 경로는 직렬화 가능한 값이며 게임 권한·네트워크 구현은 포함하지 않습니다.

## 6. 검증과 실행파일

공통 파이프라인·Unity 배포 경계 검사까지 포함해 전체 DataExporter.Checks 185개를 통과했습니다. 임시 자료로 시트 부재 허용, 누락·불일치 파일, Language·NPC·보스 대상 참조, 구형 배치 거부, 배치 ID 중복, 다른 플랫폼의 같은 종류 재사용, 신규·수정·삭제 배포, 실패 시 기존 파일 보존을 확인했습니다. 실제 플레이 검증 결과는 아닙니다.

Tools/Publish-DataExport.ps1로 `DeployData.exe`를 다시 만들고 새 실행파일의 검사 모드도 통과했습니다. 현재 결과는 31개 탭·별도 JSON 15개이며 대사 4개를 포함합니다. 실제 클라이언트로 배포했고 배포 전후 기존 정의 35개의 해시가 같았습니다. 별도의 Cutscene.cs 수정은 원본에서 SkipHoldMs가 삭제되어 수동으로 맞춘 작업이며 자동 덮어쓰기가 아닙니다. 도구 소스와 실행파일을 함께 공유해야 기획자에게 같은 검사 동작이 적용됩니다.

## 7. 다음 작업

기획자 공급 원본 검사와 배포는 완료했습니다. 다음에는 기존 공용 NPC·대화 모듈에 정의를 연결하고 직접 배치와 플랫폼 생성 경로가 같은 기능을 사용하도록 조립합니다. 원안의 대화·보스 시점·저장 미결 사항은 이번 배포 기반 작업으로 확정하지 않습니다.
