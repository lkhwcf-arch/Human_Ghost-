using System;
using System.Collections.Generic;
using System.Text;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Storage;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Preview
{
    // 에디터·개발 빌드 전용 검사 화면입니다. F9로 JSON 원본·읽은 테이블 행 수·Stage.SpawnFile 공급 여부를 보여 줍니다.
    // 게임 규칙을 바꾸지 않습니다. 플랫폼 배치 JSON은 형식 확정 전이라 파일 유무와 원문만 표시합니다.
    public sealed class DataLoadViewer : MonoBehaviour
    {
        [SerializeField, InspectorName("데이터 초기화")] private GameDataInitializer _data = null;
        [SerializeField, InspectorName("스테이지 리소스 목록 (선택)")] private HumanGhost.Gameplay.Adapters.StageContentSet _content = null;
        private bool _open;
        private string _report = "";
        private string _selected = "";
        private Vector2 _scroll;
        private readonly List<TextAsset> _jsonFiles = new List<TextAsset>();

        private void Awake() { if (!Debug.isDebugBuild) Destroy(this); }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f9Key.wasPressedThisFrame) return;
            _open = !_open;
            if (_open) Refresh();
        }

        private void Refresh()
        {
            _jsonFiles.Clear();
            var text = new StringBuilder();
            GameDataAssets assets = _data != null ? _data.Assets : null;
            if (assets == null) { _report = "데이터 초기화·에셋 목록을 연결해 주세요."; return; }
            text.AppendLine("[배포 파일] CSV " + assets.CsvFiles.Count + "개 / JSON " + assets.JsonFiles.Count + "개");
            foreach (TextAsset file in assets.JsonFiles)
            {
                if (file == null) { text.AppendLine("  JSON 참조 누락"); continue; }
                _jsonFiles.Add(file);
                text.AppendLine("  " + file.name + ".json  " + file.text.Length + "자  " + Describe(file.text));
            }
            IReadOnlyDataCatalog catalog = _data.Data;
            text.AppendLine();
            if (catalog == null) { text.AppendLine("[조회 저장소] 아직 초기화되지 않았습니다."); _report = text.ToString(); return; }
            text.AppendLine("[조회 저장소] 읽어서 변환한 행 수");
            AppendCount<Boss>(text, catalog); AppendCount<Elite>(text, catalog); AppendCount<Item>(text, catalog);
            AppendCount<Player>(text, catalog); AppendCount<Monster>(text, catalog); AppendCount<Skill>(text, catalog);
            AppendCount<Stage>(text, catalog); AppendCount<StageLayer>(text, catalog); AppendCount<Popup>(text, catalog);
            AppendCount<Language>(text, catalog);
            text.AppendLine();
            text.AppendLine("[Stage.SpawnFile] 스테이지 JSON 공급 여부");
            foreach (Stage stage in catalog.GetTable<Stage>().All)
            {
                TextAsset file = _content != null && _content.GetStage(stage.ID) != null ? _content.GetStage(stage.ID).Spawn : null;
                if (file == null) { text.AppendLine("  " + stage.ID + "  " + stage.SpawnFile + "  미공급"); continue; }
                if (!_jsonFiles.Contains(file)) _jsonFiles.Add(file);
                text.AppendLine("  " + stage.ID + "  " + stage.SpawnFile + "  " + DescribeLayout(file.text));
            }
            _report = text.ToString();
        }

        private static string DescribeLayout(string json)
        {
            try
            {
                StageLayout layout = HumanGhost.Gameplay.Adapters.StageLayoutReader.Read(json);
                int monsters = 0;
                foreach (StagePlacement placement in layout.Placements)
                    foreach (StageMonsterGroup group in placement.Monsters) monsters += group.MonsterCount;
                return "플랫폼 " + layout.Placements.Count + "개 / 몬스터 " + monsters + "마리";
            }
            catch (Exception error) { return "해석 실패: " + error.Message; }
        }

        // 원문을 직접 해석해 파일 자체가 올바른 JSON인지 보여 줍니다.
        private static string Describe(string json)
        {
            try
            {
                JToken root = JToken.Parse(json);
                return root is JArray array ? "배열 " + array.Count + "개" : root is JObject value ? "객체 항목 " + value.Count + "개" : root.Type.ToString();
            }
            catch (Exception error) { return "해석 실패: " + error.Message; }
        }

        private static void AppendCount<T>(StringBuilder text, IReadOnlyDataCatalog catalog)
        {
            try { text.AppendLine("  " + typeof(T).Name + "  " + catalog.GetTable<T>().Count + "행"); }
            catch (Exception) { text.AppendLine("  " + typeof(T).Name + "  없음"); }
        }

        private void OnGUI()
        {
            if (!_open) return;
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var area = new Rect(20f, 20f, 1000f, 1040f);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(area, "JSON 읽기 확인 (F9 닫기)", GUI.skin.window);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(_report);
            GUILayout.BeginHorizontal();
            foreach (TextAsset file in _jsonFiles)
                if (GUILayout.Button(file.name)) _selected = file.text.Length > 4000 ? file.text.Substring(0, 4000) + "\n… 이하 생략" : file.text;
            GUILayout.EndHorizontal();
            if (_selected.Length > 0) GUILayout.TextArea(_selected);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
