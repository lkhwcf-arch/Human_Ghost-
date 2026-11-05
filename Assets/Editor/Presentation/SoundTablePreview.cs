#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    // 시험 창이 만든 재생기만 소유합니다. 게임의 원래 음악과 씬 객체는 변경하지 않습니다.
    public class SoundTablePreview : EditorWindow
    {
        private IReadOnlyList<Sound> _rows = Array.Empty<Sound>();
        private DataCatalog _catalog;
        private TableSoundPlayer _player;
        private Vector2 _scroll;
        private string _status = "표를 갱신한 뒤 Play 중 소리를 선택해 주세요.";
        private int _generation;
        private readonly SoundRequestScope _requests = new SoundRequestScope();

        [MenuItem("인귀/사운드/테이블 재생 시험")]
        public static void Open() => GetWindow<SoundTablePreview>("사운드 재생 시험");
        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ReleasePlayer();
        }
        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) ReleasePlayer();
        }
        private void ReleasePlayer()
        {
            ++_generation;
            _requests.Close();
            if (_player != null) DestroyImmediate(_player.gameObject);
            _player = null;
        }
        private void Reload()
        {
            ReleasePlayer();
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Csv/Sound.csv");
            if (file == null) throw new InvalidOperationException("배포된 Sound.csv가 없습니다. DataSheet에서 먼저 배포해 주세요.");
            _rows = new CsvTableMapper<Sound>(new CsvDefinitionMapper<Sound>()).Map(new CsvParser().Read(file.text));
            _catalog = new DataCatalog();
            _catalog.Register(new DataTable<Sound>(_rows, row => row.ID));
            _status = _rows.Count == 0 ? "Sound 표가 비어 있습니다. 원본 엑셀 7행부터 작성·배포해 주세요." : $"사운드 {_rows.Count}개를 읽었습니다.";
        }
        private void EnsurePlayer()
        {
            if (_player != null) return;
            var root = new GameObject("SoundTablePreview");
            root.hideFlags = HideFlags.DontSave;
            root.SetActive(false);
            try
            {
                _player = root.AddComponent<TableSoundPlayer>();
                _player.Connect(_catalog);
                _player.Apply(GameSettingsStore.Load());
                if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
                    root.AddComponent<AudioListener>();
                root.SetActive(true);
                _requests.Open();
            }
            catch { DestroyImmediate(root); _player = null; throw; }
        }
        private async void Play(int id)
        {
            TableSoundPlayer owner = null;
            int generation = _generation;
            try
            {
                EnsurePlayer();
                owner = _player;
                var request = _requests.Begin(id, _catalog.GetTable<Sound>().Get(id).Category == 1);
                _status = $"사운드 {id}를 준비하고 있습니다.";
                await owner.PrepareAsync(new[] { id });
                if (this == null || generation != _generation || owner != _player) return;
                if (!_requests.IsCurrent(request)) { _status = "정지 또는 더 최신 요청으로 이전 재생을 취소했습니다."; Repaint(); return; }
                owner.Play(id);
                _status = $"사운드 {id} 재생을 시작했습니다.";
                Repaint();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (this == null || generation != _generation || owner != _player) return;
                _status = "재생에 실패했습니다. 주소 등록·음원 타입과 Console 상세 로그를 확인해 주세요.";
                Debug.LogException(new InvalidOperationException($"사운드 {id} 시험 재생에 실패했습니다.\n{error.Message}", error));
                Repaint();
            }
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("기존 게임 소리와 별개인 시험 재생입니다. 먼저 표를 배포하고 Addressables에 같은 주소로 음원을 등록해 주세요.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("주소 설정 준비·등록 창 열기"))
                {
                    var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                    settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
                    _status = "주소 설정을 준비했습니다. 로컬 그룹에 음원을 등록하고 표와 같은 주소를 입력해 주세요.";
                }
            if (GUILayout.Button("배포된 표 갱신"))
            {
                try { Reload(); }
                catch (Exception error) { _status = error.Message; Debug.LogException(error); }
            }
            EditorGUILayout.HelpBox(_status, MessageType.None);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || _catalog == null))
            {
                if (GUILayout.Button("시험 소리 모두 정지"))
                {
                    _requests.Close();
                    if (_player != null) _requests.Open();
                    _player?.StopAll();
                }
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (Sound row in _rows)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{row.ID} · {(row.Category == 1 ? "배경음" : "효과음")} · {row.Address}");
                    if (GUILayout.Button("재생", GUILayout.Width(60))) Play(row.ID);
                    if (GUILayout.Button("정지", GUILayout.Width(60))) { _requests.Stop(row.ID); _player?.Stop(row.ID); }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            if (!Application.isPlaying) EditorGUILayout.HelpBox("실제 재생은 Play 상태에서만 실행합니다.", MessageType.Warning);
        }
    }
}
#endif
