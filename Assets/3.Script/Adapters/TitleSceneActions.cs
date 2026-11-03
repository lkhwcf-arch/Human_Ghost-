using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 요청별 실행 담당만 연결합니다. 메뉴 규칙·저장·화면 표현은 각 소유자에게 둡니다.
    public sealed class TitleSceneActions : MonoBehaviour
    {
        [SerializeField] private TitleSceneHost _host = null;
        private readonly Dictionary<TitleAction, Func<bool>> _commands =
            new Dictionary<TitleAction, Func<bool>> { { TitleAction.Quit, Quit } };

        public void Connect(TitleAction action, Func<bool> execute)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            if (!Enum.IsDefined(typeof(TitleAction), action))
                throw new ArgumentOutOfRangeException(nameof(action));
            if (_commands.ContainsKey(action))
                throw new InvalidOperationException("이미 실행 담당이 연결된 타이틀 요청입니다: " + action);
            _commands.Add(action, execute);
        }

        // 연결한 소유자만 해제해 이전 화면이 새 연결을 끊지 않도록 합니다.
        public void Disconnect(TitleAction action, Func<bool> execute)
        {
            if (action != TitleAction.Quit && _commands.TryGetValue(action, out Func<bool> current) && current == execute)
                _commands.Remove(action);
        }

        private bool Execute(TitleAction action) =>
            _commands.TryGetValue(action, out Func<bool> execute) && execute();

        private void OnEnable()
        {
            if (_host == null) throw new InvalidOperationException("타이틀 요청을 전달할 호스트를 연결해 주세요.");
            _host.ConnectOperations(Execute);
        }

        private void OnDisable()
        {
            if (_host != null) _host.DisconnectOperations(Execute);
        }

        private void OnDestroy() => _commands.Clear();

        private static bool Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return true;
        }
    }
}
