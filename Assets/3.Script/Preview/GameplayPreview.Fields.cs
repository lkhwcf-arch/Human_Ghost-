using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        private FieldObjectController _fields;
        private GameObject _fieldRoot;
        private Sprite _fieldSprite;
        private MillisecondClock _fieldClock;

        private void ConnectFields(IReadOnlyDataCatalog data)
        {
            if (_stage == null) return;
            var areas = new List<FieldObjectArea>();
            foreach (var placement in data.GetTable<FieldObjectPlacement>().All)
            {
                if (placement.StageID != _stageId) continue;
                FieldObject definition = data.GetTable<FieldObject>().Get(placement.FieldObjectID);
                if (!definition.ContentEnabled) continue;
                Vector3 origin = _stage.GetPlacementOrigin(placement.PlacementID);
                float width = placement.Width / 100f * _worldUnitsPerTile;
                // 높이는 임시 접촉·표시 두께입니다. 데이터의 Y는 플랫폼 바닥선입니다.
                float height = 0.2f * _worldUnitsPerTile;
                Vector3 center = origin + new Vector3(placement.X / 100f * _worldUnitsPerTile + width / 2f,
                    placement.Y / 100f * _worldUnitsPerTile + height / 2f, 0f);
                areas.Add(new FieldObjectArea(placement.ID, new Bounds(center, new Vector3(width, height, 1f)), definition));
            }
            if (areas.Count == 0) return;
            _fields = new FieldObjectController(areas);
            _fields.Bind(_player, _playerBody);
            foreach (var monster in _monsters)
                _fields.Bind(monster, monster.GetComponentInChildren<SpriteRenderer>(true));
            _fieldClock = default;
            try { ShowFieldAreas(areas); }
            catch (Exception error)
            {
                Exception failure = error;
                try { ClearFieldViews(); }
                catch (Exception cleanupError) { failure = new AggregateException("필드 표시와 정리에 실패했습니다.", error, cleanupError); }
                if (_shrineModalBinding != null) _shrineModalBinding.ReportUnavailable(failure, 30333, this, "필드 영역 임시 표시");
                else Debug.LogException(failure, this);
            }
        }
        private void ShowFieldAreas(IReadOnlyList<FieldObjectArea> areas)
        {
            _fieldRoot = new GameObject("FieldObjectScope");
            _fieldRoot.SetActive(false);
            _fieldRoot.transform.SetParent(_stage.MapRoot, false);
            _fieldSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            int sortingOrder = _playerBody.sortingOrder;
            foreach (var renderer in _stage.MapRoot.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sortingLayerID == _playerBody.sortingLayerID) sortingOrder = Math.Max(sortingOrder, renderer.sortingOrder);
            foreach (var area in areas)
            {
                var view = new GameObject("FieldObject_" + area.ID);
                view.SetActive(false); view.transform.SetParent(_fieldRoot.transform, false);
                view.transform.position = area.Bounds.center;
                view.transform.localScale = new Vector3(area.Bounds.size.x, area.Bounds.size.y, 1f);
                var renderer = view.AddComponent<SpriteRenderer>(); renderer.sprite = _fieldSprite;
                renderer.sortingLayerID = _playerBody.sortingLayerID; renderer.sortingOrder = Math.Min(short.MaxValue, sortingOrder + 1);
                renderer.color = area.Kind == FieldKind.Ice
                    ? new Color(0.75f, 0.9f, 1f, 0.65f) : new Color(1f, 0.3f, 0.05f, 0.75f);
                view.SetActive(true);
            }
            _fieldRoot.SetActive(true);
        }
        private void StepFields()
        {
            if (_fields != null && _fields.NeedsTick) _fields.Tick(_fieldClock.Advance(Time.deltaTime));
        }
        private void DisconnectFields()
        {
            var fields = _fields;
            _fields = null;
            SubscriptionScope.Cleanup(() => fields?.Dispose(), ClearFieldViews);
        }
        private void ClearFieldViews()
        {
            var root = _fieldRoot; var sprite = _fieldSprite;
            _fieldRoot = null; _fieldSprite = null;
            SubscriptionScope.Cleanup(
                () => { if (root != null) root.SetActive(false); },
                () => { if (root != null) Destroy(root); },
                () => { if (sprite != null) Destroy(sprite); });
        }
        public int ResolveFieldKnockbackDistance(SpatialRuntime runtime, int distance)
            => _fields != null ? _fields.ResolveKnockbackDistance(runtime, distance) : distance;

        private void ApplyMonsterHitEffects(int monsterID, StateKey state, int direction)
        {
            foreach (var skill in _catalog.GetTable<MonsterSkill>().All)
            {
                if (skill.MonsterID != monsterID || skill.StateID != (int)state) continue;
                var effect = _catalog.GetTable<StatusEffect>().Get(skill.EffectID);
                switch (StatusEffectKindKeys.Parse(effect.Category))
                {
                    case StatusEffectKind.Knockback:
                        int distance = ResolveFieldKnockbackDistance(_player, effect.Value);
                        _motion.StartKnockback(direction, distance, effect.Duration);
                        break;
                    default: throw new InvalidOperationException("상태 효과의 실행 담당이 없습니다: " + effect.Category);
                }
            }
        }
    }
}
