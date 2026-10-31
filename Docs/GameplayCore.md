# 게임플레이 코어·프리팹 연결

## 현재 범위

| 간트 항목 | 구현·검증 | 남은 연결 |
| --- | --- | --- |
| 이동·점프·카메라 | 공통 상태·이동/점프 요청·New Input System·데이터 저장소 수신 | 실제 좌표/물리·접지 센서·카메라 |
| 공격·콤보 | 지상3타/공중1타·선입력/콤보 창·소모·판정 창·대상 중복 방지·보스 HP/체세 적용 | 히트박스·애니·이펙트 |
| 구르기·스태미나·피격·무적 | 후딜 캔슬·자원/회복·구르기 무적·Hit/CritHit/Dead·행동 잠금 | 실제 구르기 이동·피격 표현/HUD |
| 체세·급소·해체 | 보스만 체세/회복·노출/시간 초과·E 상호작용이 호출할 해체 API·무적/정수 피해 | 범위/대상 선택·상호작용 연결·연출 완료 신호·종료 정책 |

표의 네 항목은 게임 전체로는 진행 상태입니다. 코어 검증을 실제 장치·물리·카메라 플레이 완료로 보지 않습니다. 일반 몬스터 체세·해체는 데모 범위 밖입니다.

## 기획 수치 수정

- 공격 시작값 원본: [전투 R2 C02](https://docs.google.com/document/d/1Rb3cszutAFQ7jgRUUXLay5NroVIJaHlV/edit). 조작: [코어 R2 K03](https://docs.google.com/document/d/1HL1SX7PY3UCCTe6RToyM1wDOfcObx68w/edit).
- `Assets/2.Model/Prefabs/Config/PlayerCombatSettings.asset`을 Inspector에서 편집합니다. 배열은 지상1·2·3타, 공중1타 순서입니다. Windup/Active/Recovery/ComboWindow는 ms, ExtraDamage는 ATK에 더하는 정수, PoiseDamage와 StaminaCost도 정수입니다. 공중 추가 피해 -2를 허용합니다.
- 초기값: `120/80/250/200/0/10/12`, `120/80/250/200/0/10/12`, `200/100/400/0/+10/20/12`, `100/80/200/0/-2/10/12` 순서입니다. 값 결정·튜닝은 기획 담당입니다. 변경은 다음 런타임 초기화 때 반영됩니다. 실행 중 스냅샷을 임의 교체하지 않습니다.
- HP·이동·스태미나·구르기·피격 시간은 제공 `Player` 정의, 보스 HP/체세는 `Boss`, 회복/노출/해체 피해는 선택한 `BossState`에서 읽습니다. 해당 값은 DataSheet에서 수정·공식 배포합니다. 제공 생성 클래스·Bootstrapper는 수정하지 않습니다.
- 버선칼 보정은 현재 UnitStat에 이미 포함됐으므로 다시 더하지 않습니다. 공격 비용12를 자원 코어에 별도 고정하지 않습니다. `InputBuffer`와 콤보 창은 별개입니다.

## 프리팹·초기화

- `Assets/2.Model/Prefabs/PlayerGameplay.prefab`: `PlayerRuntime` + `PlayerLocomotionInput`. 전투 설정과 Move/Jump/Attack/Roll 참조를 연결했습니다. `BossGameplay.prefab`에는 `BossRuntime`을 둡니다. 렌더러·충돌체·좌표·앵커는 다른 담당이 연결할 위치입니다.
- 제공 `GameDataInitializer`의 초기화가 끝나면 완료 소비자가 `playerRuntime.Initialize(catalog, currentStamina, grounded)`를 호출합니다. `currentStamina`와 최초 접지는 호출자가 실제 초기 정책/관측으로 명시합니다. 호출 전에는 게임 입력이 전달되지 않습니다. 초기화는 한 번만 가능합니다.
- 이미 초기화된 `GameDataInitializer.Data`도 확인하고 완료 이벤트 구독은 비활성화 때 해제합니다. 이 프리팹은 데이터 공급자/저장소를 새로 만들거나 씬을 자동 검색하지 않습니다.
- 보스는 `bossRuntime.Initialize(catalog)`로 설정한 보스 ID·단계를 조회합니다. 기본 ID는 현재 데이터의1006, 단계는0입니다. 네 단계 예시를 자동 페이즈 기획으로 확정하지 않으며 페이즈 전환은 아직 없습니다.
- 생성 후 `PlayerRuntime.Actor`/`BossRuntime.Actor`에서 현재 값을 읽고 `Initialized` 이벤트로 소비자를 연결합니다. 런타임 컴포넌트는 프레임의 소수 ms를 보존해 정수 Tick을 전달합니다. 순수 코어는 시간·입력·파일을 직접 읽지 않습니다.

## 입력·이동 경계

- 기존 `Assets/10.Input/InputSystem_Actions.inputactions`를 재사용합니다. 방향키/왼쪽 스틱/D-pad, 점프 C/A, 공격 X/X, 구르기 Space/B입니다. 기존 Crouch Action은 실제 구르기 코어 연결을 위해 같은 ID의 Roll로 정리했습니다. UI 맵과 다른 Action은 보존했습니다. 스킬·약초·상호작용·Pause 구현은 아직 없습니다.
- 입력 어댑터는 원본 ActionAsset을 개체별 복제합니다. 자신이 소유한 복제본의 Action만 켜고 끄며 원본/다른 어댑터를 비활성화하지 않습니다. 두 입력 소유자에 같은 장치를 연결하면 같은 장치 명령이 둘에 전달되므로 단일 플레이어 프리팹에는 입력 어댑터 하나만 둡니다. 복제 수명 독립과 장치 페어링은 별개입니다.
- Move의 처리된 x 부호를 -1/0/1로 전달하고 취소·해제 시0을 전달합니다. 데이터 기반 행동 잠금 중에는 방향을 기억하되 실제 이동 요청 속도는0입니다. Jump 한 번 누르기는 한 번 승인되며 공중 재점프를 거부합니다.
- `JumpAccepted`와 `RollRequested(direction, distance, duration)`는 실제 물리 실행을 담당할 연결 신호입니다. 속도는 tile_x100/초, 거리/높이는 tile_x100 원본 단위입니다. 그리드 한 칸1을 프레임별 좌표 절삭/강제 스냅으로 해석하지 않습니다.
- 접지 센서는 새 실제 관측을 `PlayerRuntime.SetGrounded`로 전달합니다. 점프 승인 전의 오래된 grounded=true를 재전달하지 않습니다. 실제 Rigidbody 위치/중력·카메라 방식을 임의 확정하지 않았습니다.

## 전투·해체 연결

- `PlayerActor.TryAttack`으로 시작/선입력을 요청하고 `CurrentAttack`·`IsAttackActive`·`AttackStarted`·`AttackWindowChanged`를 애니/히트박스에 연결합니다. 다음 타 비용은 실제 다음 타 진입 때 소모합니다. 후딜 시작 후 콤보 창을 지난 입력은 거부하며, 공중/3타 뒤에는 콤보가 없습니다.
- 충돌 판정자는 양의 개체 식별자와 보스 런타임을 `TryHitBoss(targetId, boss.Actor, dodged, critical)`에 전달합니다. Unity InstanceID는 음수일 수 있으므로 그대로 넣지 않습니다. 무적/사망 거부 → 창당 대상 한 번 → 회피 시 무피해 → 정수 방어/치명 피해와 체세 피해 순서입니다. 회피/치명 난수와 충돌 위치는 외부 판정자가 공급합니다. 고정 ID를 여러 대상에 재사용하지 않습니다.
- `TryRoll`은 선딜/판정 중 거부, 후딜에는 공격을 취소합니다. 부족하면 상태/자원이 바뀌지 않습니다. `ReceiveHit(finalDamage, critical)`는 외부에서 확정한 HP 피해를 받으며 피격 시점부터 제공 HitIFrame을 적용합니다. Dead가 되면 행동/자원 진행을 멈춥니다.
- 보스 HP0이 체세0보다 우선합니다. 체세0이면 Exposed, 노출 기회를 놓치면 최대 체세로 복구합니다. `TryExecute(boss.Actor, inRange)`는 실제 범위 조건을 호출자가 전달하며 일반 몬스터를 받지 않습니다.
- 해체 중 플레이어/보스 무적, 타이머만으로 자동 종료하지 않습니다. 연출 종료 시 `CompleteExecution(restorePoise)`를 한 번 호출합니다. 피해는 최대HP×ExecuteDamage/10000, 나머지는 버림입니다(420HP·1800→75). 해체 후 체세 회복 여부는 회의 결과에 따라 호출자가 명시하며 기본값으로 확정하지 않습니다.
- HP/체세/스태미나 UI는 변경 이벤트 구독 + 현재 값 조회 + 비활성화 때 해제를 사용합니다. 이벤트를 물리 명령과 혼동하지 않습니다.

## 검증

검사 자동화는 [검사 자동화와 오류 경계](AutomationAndFailures.md)를 따릅니다.
데이터·코어를 함께 검사할 때는 `Tools/Verify-Project.ps1`을 실행합니다.
기존 검사 소스를 .NET 10으로도 실행하며 GitHub에서 같은 명령을 사용합니다.
아래 명령은 기존 Unity Roslyn·Mono 기반 실행 경로로 유지합니다.

설치된 Unity의 Roslyn/Mono를 사용하며 SDK/패키지 추가 설치는 없습니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Locomotion.Checks/Run-Checks.ps1 -UnityEditorPath 'C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe'
```

현재 코어123개 검사(기존60+전투63) 통과. 실제 제공 Bootstrapper·ID 저장소·프리팹·설정 에셋을 사용한 Unity 메모리 입력/연결 검사18개도 통과했습니다. 가상 키보드/패드와 임시 Preview Scene을 사용했고 테스트 후 정리했습니다. Editor 테스트 중 게임용 입력 업데이트를 임시로 허용한 상태와 원래 장치/설정도 복원했습니다. 패키지 testables 변경이나 테스트 씬 저장은 없습니다.

실제 물리·카메라·히트박스/애니·연출/HUD·실제 장치 Play·보스 AI·페이즈 전환은 별도 연결/검증 대상입니다.
