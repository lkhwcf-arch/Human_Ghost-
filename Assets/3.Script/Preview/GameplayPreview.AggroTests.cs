#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        // 시트 정의의 정수 속성을 사용합니다. 시험값·몬스터 ID를 별도로 고정하지 않습니다.
        private static readonly PropertyInfo[] _aggroTestProperties = typeof(MonsterAggro).GetProperties()
            .Where(property => property.Name != nameof(MonsterAggro.ID) && property.Name != nameof(MonsterAggro.MonsterID)).ToArray();
        private readonly DefinitionMapper<MonsterAggro> _aggroTestMapper = new DefinitionMapper<MonsterAggro>();
        private readonly Dictionary<string, string> _aggroTestFields = new Dictionary<string, string>();
        private IReadOnlyList<MonsterAggro> _aggroTestRows;
        private MonsterAggro _aggroTestSource;
        private MonsterAggro _aggroTestApplied;
        private MonsterAwareness _aggroTestSession;
        private int _aggroTestIndex;
        private bool _aggroTestExpanded, _aggroTestTargetActive = true, _aggroTestProtected, _aggroTestDirty;
        private int _aggroTestFacing = 1;
        private string _aggroTestX = "0", _aggroTestY = "0", _aggroTestSpawn = "0", _aggroTestMs = "0";
        private string _aggroTestReport = "";

#if UNITY_EDITOR
        public static bool ShowAggroGizmos
        {
            get => UnityEditor.EditorPrefs.GetBool("HumanGhost.AggroGizmos", false);
            set { UnityEditor.EditorPrefs.SetBool("HumanGhost.AggroGizmos", value); UnityEditor.SceneView.RepaintAll(); }
        }
#endif
        private void DrawAggroTests()
        {
#if UNITY_EDITOR
            bool showGizmos = GUILayout.Toggle(ShowAggroGizmos, "Scene 뷰 선택한 몬스터 인식 범위 Gizmos", _bossTestToggleStyle);
            if (showGizmos != ShowAggroGizmos) ShowAggroGizmos = showGizmos;
#endif
            if (GUILayout.Button(_aggroTestExpanded ? "어그로 데이터 시험 접기" : "어그로 데이터 시험 펼치기", _bossTestButtonStyle))
                _aggroTestExpanded = !_aggroTestExpanded;
            if (!_aggroTestExpanded) return;
            GUILayout.Label("독립 판정 시험입니다. 시트·실제 몬스터·저장 파일은 변경하지 않습니다. 거리·높이는 100=1칸, 시간은 ms입니다.", _bossTestLabelStyle);
            if (_catalog == null) { GUILayout.Label("맵 데이터가 준비된 뒤 사용해 주세요.", _bossTestLabelStyle); return; }
            if (_aggroTestRows == null)
            {
                _aggroTestRows = _catalog.GetTable<MonsterAggro>().All;
                if (_aggroTestRows.Count > 0) LoadAggroTestSource();
            }
            if (_aggroTestRows.Count == 0) { GUILayout.Label("MonsterAggro에 시험할 행이 없습니다.", _bossTestLabelStyle); return; }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이전 데이터", _bossTestButtonStyle))
            { _aggroTestIndex = (_aggroTestIndex + _aggroTestRows.Count - 1) % _aggroTestRows.Count; LoadAggroTestSource(); }
            if (GUILayout.Button("다음 데이터", _bossTestButtonStyle))
            { _aggroTestIndex = (_aggroTestIndex + 1) % _aggroTestRows.Count; LoadAggroTestSource(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("주입 원본: MonsterAggro " + _aggroTestSource.ID + " / Monster " + _aggroTestSource.MonsterID, _bossTestLabelStyle);
            foreach (PropertyInfo property in _aggroTestProperties)
            {
                string before = _aggroTestFields[property.Name];
                string after = DrawAggroTestInput(property.Name, before);
                if (after != before) _aggroTestDirty = true;
                _aggroTestFields[property.Name] = after;
            }
            GUILayout.Label(_aggroTestDirty ? "입력 변경됨: 적용 버튼 전에는 이전 사본으로 판정합니다." : "현재 입력값이 시험 사본에 적용돼 있습니다.", _bossTestLabelStyle);
            if (GUILayout.Button("입력값 사본 검증·적용 / 시험 초기화", _bossTestButtonStyle)) ApplyAggroTestValues();
            if (GUILayout.Button("현재 배포 원본값으로 복원", _bossTestButtonStyle)) LoadAggroTestSource();
            GUILayout.Label("아래 시험 입력은 원본 데이터와 별개입니다. 양수 X는 월드 오른쪽, 양수 Y는 몬스터보다 위입니다.", _bossTestLabelStyle);
            _aggroTestX = DrawAggroTestInput("가로 위치 차이", _aggroTestX);
            _aggroTestY = DrawAggroTestInput("발 높이 차이", _aggroTestY);
            _aggroTestSpawn = DrawAggroTestInput("스폰 자리 이탈 거리", _aggroTestSpawn);
            _aggroTestMs = DrawAggroTestInput("진행할 정수 ms", _aggroTestMs);
            if (GUILayout.Button("바라보는 방향: " + (_aggroTestFacing > 0 ? "오른쪽" : "왼쪽") + " / 전환", _bossTestButtonStyle))
                _aggroTestFacing = -_aggroTestFacing;
            _aggroTestTargetActive = GUILayout.Toggle(_aggroTestTargetActive, "대상 생존·활성", _bossTestToggleStyle);
            _aggroTestProtected = GUILayout.Toggle(_aggroTestProtected, "공격·노출 중 복귀 보류 상태", _bossTestToggleStyle);
            if (GUILayout.Button("위치 관측만 판정", _bossTestButtonStyle)) RunAggroTest(false, false);
            if (GUILayout.Button("관측 후 입력한 ms 진행", _bossTestButtonStyle)) RunAggroTest(true, false);
            if (GUILayout.Button("피격 즉시 인식 시험", _bossTestButtonStyle)) RunAggroTest(false, true);
            if (GUILayout.Button("시험 결과 복사", _bossTestButtonStyle)) GUIUtility.systemCopyBuffer = _aggroTestReport;
            GUILayout.Label(_aggroTestReport, _bossTestLabelStyle);
        }

        private string DrawAggroTestInput(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _bossTestLabelStyle, GUILayout.Width(290));
            string edited = GUILayout.TextField(value, _bossTestLabelStyle, GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            return edited;
        }

        private static int ReadAggroTestInteger(string text, string name)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                throw new FormatException(name + "에는 범위 안의 정수만 입력해 주세요.");
            return value;
        }

        private void LoadAggroTestSource()
        {
            _aggroTestSource = _aggroTestRows[_aggroTestIndex];
            _aggroTestFields.Clear();
            foreach (PropertyInfo property in _aggroTestProperties)
                _aggroTestFields.Add(property.Name, ((int)property.GetValue(_aggroTestSource)).ToString(CultureInfo.InvariantCulture));
            ApplyAggroTestValues();
        }

        private void ApplyAggroTestValues()
        {
            try
            {
                MonsterAggro copy = _aggroTestMapper.Map((column, type) =>
                    column == nameof(MonsterAggro.ID) ? _aggroTestSource.ID :
                    column == nameof(MonsterAggro.MonsterID) ? _aggroTestSource.MonsterID : ReadAggroTestInteger(_aggroTestFields[column], column));
                // 검증이 끝난 새 판정기만 교체합니다. 잘못 입력하면 기존 적용값·시험 상태는 유지합니다.
                var next = new MonsterAwareness(copy);
                _aggroTestSession = next;
                _aggroTestApplied = copy;
                _aggroTestDirty = false;
                _aggroTestReport = "적용 완료. 인식·복귀 상태가 초기화됐습니다.\n" + FormatAggroTestDefinition(copy);
            }
            catch (Exception error) { ReportAggroTestFailure(error); }
        }

        private void RunAggroTest(bool advance, bool hit)
        {
            if (_aggroTestSession == null) return;
            try
            {
                int x = ReadAggroTestInteger(_aggroTestX, "가로 위치 차이");
                int y = ReadAggroTestInteger(_aggroTestY, "발 높이 차이");
                int spawn = ReadAggroTestInteger(_aggroTestSpawn, "스폰 자리 이탈 거리");
                int milliseconds = advance ? ReadAggroTestInteger(_aggroTestMs, "진행 시간") : 0;
                // 모든 입력을 먼저 확인해 잘못된 시간으로 관측 상태만 바뀌지 않게 합니다.
                if (spawn < 0 || milliseconds < 0 || x == int.MinValue || y == int.MinValue)
                    throw new ArgumentException("스폰 거리·시간은 0 이상이고 위치 차이는 유효한 정수 범위여야 합니다.");
                if (_aggroTestTargetActive) _aggroTestSession.Observe(x, y, spawn, true, _aggroTestFacing);
                else _aggroTestSession.LoseTarget();
                if (hit) _aggroTestSession.NotifyHit();
                _aggroTestSession.Advance(milliseconds, _aggroTestProtected);
                _aggroTestReport = "원본 행 " + _aggroTestSource.ID + " / 시험용 적용 사본\n" +
                    "X=" + x + ", Y=" + y + ", Facing=" + _aggroTestFacing + ", Spawn=" + spawn + ", 진행 ms=" + milliseconds + "\n" +
                    "인식·추적=" + _aggroTestSession.IsEngaged + " / 복귀=" + _aggroTestSession.IsReturning + "\n" +
                    FormatAggroTestDefinition(_aggroTestApplied);
                if (_aggroTestDirty) _aggroTestReport += "\n주의: 편집 중인 미적용 입력이 있습니다. 이전 적용 사본으로 판정했습니다.";
            }
            catch (Exception error) { ReportAggroTestFailure(error); }
        }

        private void ReportAggroTestFailure(Exception error)
        {
            _aggroTestReport = "시험 입력 적용 실패. 마지막으로 적용한 사본·상태는 유지됩니다.\n" + error.Message;
            if (!(error is ArgumentException) && !(error is FormatException) && !(error is OverflowException)) ReportCutsceneError(error);
        }

        private static string FormatAggroTestDefinition(MonsterAggro applied) => string.Join("\n",
            _aggroTestProperties.Select(property => property.Name + "=" + property.GetValue(applied)));

        private void ClearAggroTests()
        {
            _aggroTestRows = null; _aggroTestSource = null; _aggroTestApplied = null; _aggroTestSession = null; _aggroTestFields.Clear();
            _aggroTestIndex = 0; _aggroTestExpanded = false; _aggroTestTargetActive = true; _aggroTestProtected = false;
            _aggroTestDirty = false;
            _aggroTestFacing = 1; _aggroTestX = _aggroTestY = _aggroTestSpawn = _aggroTestMs = "0"; _aggroTestReport = "";
        }
    }
}
#endif
