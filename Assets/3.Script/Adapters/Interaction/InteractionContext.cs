using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Presentation;

namespace HumanGhost.Gameplay.Adapters
{
    // 상호작용 대상에 조립자가 넣어 주는 서비스 묶음입니다. 대상은 전역을 찾지 않습니다.
    public sealed class InteractionContext
    {
        public PlayerRuntime Player { get; }
        public TextboxScreen Textbox { get; }
        public StageProgress Progress { get; }
        public IReadOnlyDataCatalog Data { get; }
        // 씬 이름·도착 앵커 이름. 로딩 화면을 거칩니다.
        public Action<string, string> GoToScene { get; }

        public InteractionContext(PlayerRuntime player, TextboxScreen textbox, StageProgress progress, IReadOnlyDataCatalog data, Action<string, string> goToScene)
        {
            Player = player != null ? player : throw new ArgumentNullException(nameof(player));
            Textbox = textbox != null ? textbox : throw new ArgumentNullException(nameof(textbox));
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            GoToScene = goToScene ?? throw new ArgumentNullException(nameof(goToScene));
        }

        public string Text(int id) => Data.GetTable<Language>().Get(id).KO;
    }
}
