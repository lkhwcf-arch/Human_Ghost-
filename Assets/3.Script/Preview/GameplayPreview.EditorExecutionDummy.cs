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
                if (holder != null) { if (Application.isPlaying) Destroy(holder); else DestroyImmediate(holder); }
                throw;
            }
        }

        public void ClearEditorExecutionDummies()
        {
            for (int i = 0; i < _editorExecutionDummies.Length; i++) ClearEditorExecutionDummy(i);
        }

        private void ClearEditorExecutionDummy(int index)
        {
            MonsterRuntime dummy = _editorExecutionDummies[index];
            _editorExecutionDummies[index] = null;
            if (dummy == null) return;
            if (_executingRuntime == dummy)
            {
                _actor?.CancelExecution();
                _executingRuntime = null;
                _executionRemaining = 0f;
            }
            dummy.GetComponent<MonsterAnimationAudio>()?.Disconnect();
            GameObject holder = dummy.transform.parent.gameObject;
            holder.SetActive(false);
            if (Application.isPlaying) Destroy(holder); else DestroyImmediate(holder);
            RequestInteractionRefresh();
        }
    }
}
#endif
