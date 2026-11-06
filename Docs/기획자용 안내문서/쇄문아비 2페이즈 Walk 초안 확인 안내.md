# 쇄문아비 2페이즈 Walk 개선본 확인 안내

2026-11-06, 기존 Idle 리그를 보존한 별도 사본에서 Walk 가중치를 개선했습니다. 큰 메시 뒤집힘과 부위 연결부의 틈은 재생 검사에서 해소됐습니다. 다만 관절의 국소적인 도트 늘어남은 남아 있으며, 픽셀이 전혀 변형되지 않는 완성 원화로 판정한 것은 아닙니다. 실제 보스 코드에는 연결하지 않았습니다.

## Walk 확인

1. `Assets/5.Animation/Swaemunabi_Phase2/Scenes/Swaemunabi_Phase2_Walk_Refined_Test.unity`를 엽니다.
2. Play를 누르면 Idle → 오른쪽 Walk → Idle → 왼쪽 Walk → Idle을 약 16.3초 동안 재생합니다.
3. 한 주기 1.3초, 이동 속도 초당 2유닛, 배율 2.3입니다. 좌우 각각 약 3.5주기를 재생합니다. 다시 보려면 Play를 종료하고 재실행합니다.

개선 프리팹은 `Prefabs/Swaemunabi_Phase2_WalkRig.prefab`, 가중치가 수정된 이미지 사본은 `Source/Boss_Phase_2_WalkRig.png`입니다. PNG 픽셀은 원본과 같고 Sprite 데이터의 가중치만 다릅니다. 편집할 클립은 기존에 만든 `Clips/Swaemunabi_Phase2_Walk.anim`이며 Animation 창에서 수정할 수 있습니다. 시험 컨트롤러는 `Controllers/Swaemunabi_Phase2_WalkPreview.controller`입니다.

`Scenes/Swaemunabi_Phase2_Walk_Test.unity`는 개선 전 초안 기록입니다. 현재 확인에는 이름에 `Walk_Refined_Test`가 있는 씬을 사용합니다.

## Idle 회귀 확인

`Scenes/Swaemunabi_Phase2_Idle_Regression_Test.unity`에서 Play를 누릅니다. 왼쪽은 개선 리그, 오른쪽은 기존 Idle 리그이며 같은 Idle 클립을 동시에 재생합니다. 기존 Idle 원본 프리팹·클립·시험 씬은 변경하지 않았습니다.

실제로 2.28주기 비교했고 뼈 포즈와 1.8픽셀 호흡은 같았습니다. 가중치 변경으로 개선 사본의 일부 정점은 기존 Idle과 최대 약 2.45원본 픽셀 차이가 납니다. 원래 Idle과 완전히 같은 스킨 변형을 요구한다면 이 차이를 검토해야 합니다.

## 확인 결과와 남은 차이

- Walk 좌우 각각 약 3.5주기, Idle 복귀 2회 확인.
- 관측한 메시 뒤집힘 0건, 부위 경계 공유 정점 간 틈 0.
- 접지 중 손 하단 정점 이동 오차 약 0.00045원본 픽셀. 전환·착지·이륙 경계는 이 수치에서 제외됩니다.
- 모든 삼각형 변을 검사한 최대 늘어남 약 1.75배. 관절의 일부 도트는 늘어나며 고정 원본과 완전히 같지 않습니다.
- 원본 오른팔 끝은 왼손처럼 손가락이 펼쳐진 형태가 아닙니다. 새 손가락이나 가려진 신체면은 그리지 않았습니다.

실제 Play 화면은 `Validation/walk_refined_right_support.png`, `walk_refined_right_swing.png`, `walk_refined_left_support.png`, `walk_refined_left_swing.png`입니다. Idle 비교는 `idle_refined_exhale.png`, `idle_refined_inhale.png`입니다. JSON 측정 기록과 자세한 제한은 [설계와 검증](../개발자용%20설계문서/쇄문아비%202페이즈%20Walk%20초안%20설계와%20검증.md)을 참고합니다.
