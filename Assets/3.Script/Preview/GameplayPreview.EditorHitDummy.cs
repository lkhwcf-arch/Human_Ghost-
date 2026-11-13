#if UNITY_EDITOR
using HumanGhost.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        private GameObject _editorHitDummyRoot;
        private MonsterRuntime _editorHitDummy;
        private PreviewMonsterDriver _editorHitDummyDriver;

        public bool HasEditorHitDummy => _editorHitDummyRoot != null;
        public bool CanEditorSpawnHitDummy => CanEditorTravel && _actor.Locomotion.IsGrounded &&
            _actor.Locomotion.CanMove && _content != null;

        public void SpawnEditorHitDummy()
        {
            if (!CanEditorSpawnHitDummy) return;
            MonsterRuntime prefab = _content.GetMonster(3001);
            if (prefab == null) throw new System.InvalidOperationException("허수귀 프리팹을 연결해 주세요.");
            if (TryGetEditorDummyPosition(prefab, 2f, out Vector3 position)) PlaceEditorHitDummy(prefab, position);
        }

        private bool TryGetEditorDummyPosition(MonsterRuntime prefab, float distance, out Vector3 position)
        {
            Vector3 scale = prefab.GetComponentInChildren<SpriteRenderer>(true).transform.lossyScale;
            var halfSize = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) * 0.5f;
            position = _player.transform.position + Vector3.right * (_actor.Facing * distance * _worldUnitsPerTile);
            if (_stage != null) position.x = Mathf.Clamp(position.x, _stage.Area.min.x + halfSize.x, _stage.Area.max.x - halfSize.x);
            position.y += 3f * _worldUnitsPerTile;
            var collision = new PreviewCollision(_useMapCollision, _groundY - _playerHalfSize.y + halfSize.y, halfSize);
            if (!collision.FindFloor(position, 6f * _worldUnitsPerTile, out float gap))
            {
                Debug.LogWarning("테스트 몬스터를 놓을 바닥이 없습니다. 바닥이 있는 곳으로 이동해 주세요.", this);
                return false;
            }
            position.y -= gap;
            return true;
        }

        private void PlaceEditorHitDummy(MonsterRuntime prefab, Vector3 position)
        {
            if (_editorHitDummy == null)
            {
                ClearEditorHitDummy();
                _editorHitDummyRoot = new GameObject("EditorHitDummyScope");
                _editorHitDummyRoot.SetActive(false);
                SceneManager.MoveGameObjectToScene(_editorHitDummyRoot, gameObject.scene);
                try
                {
                    _editorHitDummy = Instantiate(prefab, _editorHitDummyRoot.transform);
                    _editorHitDummy.name = "EffectTestHusugwi";
                    _editorHitDummy.transform.position = position;
                    _editorHitDummy.Initialize(_catalog, true);
                    _editorHitDummyDriver = new PreviewMonsterDriver(_editorHitDummy, _player, _playerBody, _attackWindow,
                        _firstMonsterTargetId + _monsterDrivers.Length, _playerTargetId, _worldUnitsPerTile);
                    if (_audio != null) _editorHitDummy.GetComponent<MonsterAnimationAudio>()?.Connect(_editorHitDummy, _audio.PlayEffect);
                    _editorHitDummy.GetComponent<MonsterPreviewLabel>()?.SetDisplayName("이펙트 점검용 허수귀 (체력 무제한)");
                    _editorHitDummyRoot.SetActive(true);
                }
                catch { ClearEditorHitDummy(); throw; }
            }
            _editorHitDummy.SetPosition(position);
            _editorHitDummy.Actor.Release();
            _editorHitDummyDriver.Sprite.flipX = _actor.Facing < 0;
        }

        // AI 관측·이동·일반몹 목록 등록 없이 실제 플레이어 타격만 확인합니다.
        private void CheckEditorHitDummy()
        {
            if (_editorHitDummy == null || _editorHitDummyDriver == null) return;
            _editorHitDummyDriver.Sprite.color = Color.white;
            HitOutcome outcome = _editorHitDummyDriver.CheckPlayerHit();
            _feedback?.OnPlayerHit(outcome, _editorHitDummyDriver.Sprite, _editorHitDummyDriver.Position, _actor.Facing);
        }

        public void ClearEditorHitDummy()
        {
            _editorHitDummyDriver?.Dispose();
            _editorHitDummyDriver = null;
            if (_editorHitDummy != null) _editorHitDummy.GetComponent<MonsterAnimationAudio>()?.Disconnect();
            _editorHitDummy = null;
            if (_editorHitDummyRoot != null)
            {
                _editorHitDummyRoot.SetActive(false);
                if (Application.isPlaying) Destroy(_editorHitDummyRoot);
                else DestroyImmediate(_editorHitDummyRoot);
            }
            _editorHitDummyRoot = null;
        }
    }
}
#endif
