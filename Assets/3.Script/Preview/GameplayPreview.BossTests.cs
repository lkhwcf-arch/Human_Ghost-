#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        private enum BossTestCommand { None, Travel, Kill, Expose, Stop, Pattern }
        private BossTestCommand _bossTestCommand;
        private int _bossTestPattern;
        private bool _bossTestOpen, _bossTestCursor;
        private string _bossTestStatus = "";
        private Rect _bossTestRect = new Rect(20, 40, 560, 620);
        private Vector2 _bossTestScroll;
        private GUIStyle _bossTestWindowStyle, _bossTestButtonStyle, _bossTestLabelStyle;
        private bool CanUseBossTest => _subscribed && !_modalBlocked && !_saveLocked && !_leaving && !LoadingScreen.IsActive
            && _actor != null && !_actor.IsDead && _actor.CurrentState != HumanGhost.Data.Definitions.StateKey.Execute
            && (_textbox == null || !_textbox.IsOpen) && (_pause == null || !_pause.IsOpen);
        private void StepBossTests()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                if (!_bossTestOpen) { _bossTestCursor = Cursor.visible; _bossTestOpen = true; Cursor.visible = true; }
                else CloseBossTests();
            }
            BossTestCommand command = _bossTestCommand;
            _bossTestCommand = BossTestCommand.None;
            if (command == BossTestCommand.None || !CanUseBossTest) return;
            try
            {
                if (command == BossTestCommand.Travel)
                {
                    if (_progress.HasDefeatedBoss(BossStage)) { _bossTestStatus = "이미 처치한 진행입니다. 새 게임으로 시험해 주세요."; return; }
                    string arrival = GetUniqueArrivalAnchor(BossStage);
                    CloseBossTests(); GoToStage(BossStage, arrival); return;
                }
                if (!_bossActive || _boss == null || _boss.Actor == null || _boss.Actor.IsDead) return;
                if ((command == BossTestCommand.Stop || command == BossTestCommand.Pattern) && _bossDriver == null) return;
                if (command == BossTestCommand.Stop)
                { _bossDriver.AutomaticPatterns = !_bossDriver.AutomaticPatterns; _boss.Combat.Release(); return; }
                if (command == BossTestCommand.Pattern)
                {
                    _bossDriver.AutomaticPatterns = false; _boss.Combat.Release();
                    _bossTestStatus = _boss.TryStartPattern(_bossTestPattern) ? "선택한 패턴을 실행합니다." : "현재 노출·무적 상태에서는 패턴을 시작할 수 없습니다.";
                }
                else
                {
                    _boss.Combat?.Release();
                    _bossTestStatus = _boss.Actor.ReceiveHit(command == BossTestCommand.Kill ? _boss.Actor.Health : 0,
                        command == BossTestCommand.Expose ? _boss.Actor.Poise : 0) ? "시험 요청을 적용했습니다." : "현재 상태에서 요청이 거부됐습니다.";
                }
            }
            catch (Exception error) { _bossTestStatus = error.Message; ReportCutsceneError(error); }
        }
        private void OnGUI()
        {
            if (!_bossTestOpen) return;
            if (_bossTestWindowStyle == null)
            {
                // 시험 화면의 표시 크기입니다. 게임 규칙·콘텐츠 데이터와 무관합니다.
                _bossTestWindowStyle = new GUIStyle(GUI.skin.window) { fontSize = 22, padding = new RectOffset(16, 16, 38, 16) };
                _bossTestButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fixedHeight = 48, wordWrap = true };
                _bossTestLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            }
            Matrix4x4 previousMatrix = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            try
            {
                float scale = Mathf.Max(0.1f, Screen.height / 720f);
                float width = Screen.width / scale, height = Screen.height / scale;
                _bossTestRect.width = Mathf.Min(560, width);
                _bossTestRect.height = Mathf.Min(620, height);
                _bossTestRect.x = Mathf.Clamp(_bossTestRect.x, 0, Mathf.Max(0, width - _bossTestRect.width));
                _bossTestRect.y = Mathf.Clamp(_bossTestRect.y, 0, Mathf.Max(0, height - _bossTestRect.height));
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                _bossTestRect = GUILayout.Window(GetInstanceID(), _bossTestRect, DrawBossTests, "보스 시험 · F8 닫기", _bossTestWindowStyle);
            }
            finally { GUI.matrix = previousMatrix; GUI.enabled = previousEnabled; }
        }
        private void DrawBossTests(int id)
        {
            _bossTestScroll = GUILayout.BeginScrollView(_bossTestScroll);
            GUILayout.Label("시험 조작은 임시 진행을 변경합니다. 저장하면 기록될 수 있습니다.", _bossTestLabelStyle);
            GUI.enabled = CanUseBossTest;
            if (GUILayout.Button("보스방으로 바로 이동", _bossTestButtonStyle)) _bossTestCommand = BossTestCommand.Travel;
            GUI.enabled = CanUseBossTest && _bossActive && _boss != null && _boss.Actor != null && !_boss.Actor.IsDead;
            if (GUILayout.Button("보스 처치 · 엔딩 흐름 확인", _bossTestButtonStyle)) _bossTestCommand = BossTestCommand.Kill;
            if (GUILayout.Button("체세 붕괴 · 해체 확인", _bossTestButtonStyle)) _bossTestCommand = BossTestCommand.Expose;
            if (_bossDriver != null && _boss != null && _boss.Combat != null)
            {
                if (GUILayout.Button("패턴 자동 선택: " + (_bossDriver.AutomaticPatterns ? "켜짐" : "꺼짐"), _bossTestButtonStyle)) _bossTestCommand = BossTestCommand.Stop;
                for (int i = 0; i < _boss.Combat.PatternCount; i++)
                    if (GUILayout.Button("패턴 " + (i + 1) + " 실행 · " + _boss.Combat.GetPattern(i).Key, _bossTestButtonStyle))
                    { _bossTestPattern = i; _bossTestCommand = BossTestCommand.Pattern; }
            }
            GUI.enabled = true;
            GUILayout.Label(_bossTestStatus, _bossTestLabelStyle);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, _bossTestRect.width, 32));
        }
        private void CloseBossTests()
        {
            if (_bossTestOpen) Cursor.visible = _bossTestCursor;
            _bossTestOpen = false; _bossTestCommand = BossTestCommand.None;
        }
    }
}
#endif
