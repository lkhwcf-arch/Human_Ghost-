using System;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 컷씬의 넘기기·스킵은 키 배치와 무관하게 공통 Enter 입력(SkipKeyInput) 하나로 처리합니다.
    // 현재 장면이 다음 장 넘기기를 허용하면 다음 장으로, 아니면 범위 전체를 스킵합니다(대사 Enter 넘기기와 같은 규칙).
    public class CutsceneInput : MonoBehaviour
    {
        [SerializeField, InspectorName("컷씬 화면")] private CutsceneScreen _screen = null;
        [SerializeField, InspectorName("Enter 스킵 범위")] private SkipScope _scope = SkipScope.Cutscene;
        [SerializeField, InspectorName("스킵 안내")] private SkipHint _hint = null;
        private SkipKeyInput _skip;
        private bool _blocked;
        public CutsceneScreen Screen => _screen;
        public bool IsReady => _skip != null;
        public void SetScope(SkipScope scope)
        {
            if (IsReady) throw new InvalidOperationException("컷씬 입력을 비활성화한 뒤 범위를 변경해 주세요.");
            _scope = scope;
        }
        public void Validate()
        {
            if (_screen == null)
                throw new InvalidOperationException("컷씬 화면을 연결해 주세요.");
            if (_scope != SkipScope.Cutscene && _scope != SkipScope.Video)
                throw new InvalidOperationException("컷씬 입력의 범위는 컷씬 전체 또는 영상 하나로 설정해 주세요.");
        }
        public void ConnectHint(LanguageTextSource text, LanguageCode language)
        {
            if (_hint == null) throw new InvalidOperationException("컷씬의 공용 스킵 안내를 연결해 주세요.");
            _hint.Connect(_screen, _scope, text, language);
            _hint.SetBlocked(_blocked);
        }
        public void SetBlocked(bool blocked) { _blocked = blocked; if (_hint != null) _hint.SetBlocked(blocked); }
        protected virtual void OnEnable()
        {
            Validate();
            try { _skip = new SkipKeyInput(() => !_blocked && _screen != null && _screen.CanSkip(_scope), Skip); }
            catch { Release(); throw; }
        }
        private void Skip()
        {
            try { if (!_screen.Advance()) _screen.TrySkip(_scope); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
        protected virtual void OnDisable() => Release();
        private void Release()
        {
            if (_hint != null) _hint.Disconnect();
            _skip?.Dispose(); _skip = null;
        }
    }
}
