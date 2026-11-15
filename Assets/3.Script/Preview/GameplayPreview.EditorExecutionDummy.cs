#if UNITY_EDITOR
using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        private readonly MonsterRuntime[] _editorExecutionDummies = new MonsterRuntime[2];
        private readonly GameObject[] _editorExecutionDummyRoots = new GameObject[2];
        public bool HasEditorExecutionDummies => _editorExecutionDummies[0] != null || _editorExecutionDummies[1] != null;

        public void SpawnEditorExecutionDummy(int monsterId)
        {
            if (monsterId != 3001 && monsterId != 3002) throw new ArgumentOutOfRangeException(nameof(monsterId));
            if (!CanEditorSpawnHitDummy) return;
            MonsterRuntime prefab = _content.GetMonster(monsterId);
            if (prefab == null) throw new InvalidOperationException("처형 테스트 몬스터 프리팹을 연결해 주세요.");
            int index = monsterId - 3001;
            if (TryGetEditorDummyPosition(prefab, 2f + index * 2f, out Vector3 position))
                PlaceEditorExecutionDummy(prefab, position, index);
        }

        private void PlaceEditorExecutionDummy(MonsterRuntime prefab, Vector3 position, int index)
        {
            ClearEditorExecutionDummy(index);
            var holder = new GameObject(index == 0 ? "ExecutionTestHusugwiScope" : "ExecutionTestJigegwiScope");
            _editorExecutionDummyRoots[index] = holder;
            holder.SetActive(false);
            SceneManager.MoveGameObjectToScene(holder, gameObject.scene);
            try
            {
                MonsterRuntime dummy = Instantiate(prefab, holder.transform);
                _editorExecutionDummies[index] = dummy;
                dummy.name = index == 0 ? "ExecutionTestHusugwi" : "ExecutionTestJigegwi";
                dummy.ConfigureSpatialScale(_worldUnitsPerTile);
                dummy.SetPosition(position);
                dummy.Initialize(_catalog);
                dummy.FreezeForEditorTest = true;
                if (!dummy.Actor.ReceiveHit(0, false, dummy.Actor.Poise) || !dummy.Actor.IsExposed)
                    throw new InvalidOperationException("테스트 몬스터의 처형 노출 상태를 만들지 못했습니다.");
                if (_audio != null) dummy.GetComponent<MonsterAnimationAudio>()?.Connect(dummy, _audio.PlayEffect);
                dummy.GetComponent<MonsterPreviewLabel>()?.SetDisplayName(index == 0 ? "처형 테스트 허수귀" : "처형 테스트 지게귀");
                holder.SetActive(true);
                RequestInteractionRefresh();
            }
            catch
            {
                ClearEditorExecutionDummy(index);
                throw;
            }
        }

        public void ClearEditorExecutionDummies()
        {
            SubscriptionScope.Cleanup(() => ClearEditorExecutionDummy(0), () => ClearEditorExecutionDummy(1));
        }

        private void ClearEditorExecutionDummy(int index)
        {
            MonsterRuntime dummy = _editorExecutionDummies[index];
            GameObject holder = _editorExecutionDummyRoots[index];
            _editorExecutionDummies[index] = null;
            _editorExecutionDummyRoots[index] = null;
            SubscriptionScope.Cleanup(
                () => { if (dummy != null && _executingRuntime == dummy)
                { _executingRuntime = null; _executionRemainingMilliseconds = 0; _actor?.CancelExecution(); } },
                () => { if (dummy != null) dummy.GetComponent<MonsterAnimationAudio>()?.Disconnect(); },
                () => { if (holder != null) holder.SetActive(false); },
                () => { if (holder != null) { if (Application.isPlaying) Destroy(holder); else DestroyImmediate(holder); } },
                RequestInteractionRefresh);
        }
    }
}
#endif
