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
                if (_shrineModalBinding != null) _shrineModalBinding.ReportUnavailable(failure, 30333, this, "필드 그림 표시");
                else Debug.LogException(failure, this);
            }
        }
        private void ShowFieldAreas(IReadOnlyList<FieldObjectArea> areas)
        {
            _fieldRoot = new GameObject("FieldObjectScope");
            _fieldRoot.SetActive(false);
            _fieldRoot.transform.SetParent(_stage.MapRoot, false);
            int sortingOrder = _playerBody.sortingOrder;
            foreach (var renderer in _stage.MapRoot.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sortingLayerID == _playerBody.sortingLayerID) sortingOrder = Math.Max(sortingOrder, renderer.sortingOrder);
            sortingOrder = Math.Min(short.MaxValue - 1, sortingOrder + 1);
            foreach (var area in areas)
            {
                var view = new GameObject("FieldObject_" + area.ID);
                view.SetActive(false); view.transform.SetParent(_fieldRoot.transform, false);
                // 표시의 바닥만 맞춥니다. FieldObjectArea.Bounds는 변경하지 않습니다.
                if (area.Kind == FieldKind.Ice)
                {
                    AddFieldSprite(view.transform, "Middle", _content.FieldIceMiddle, area.Bounds.min.x,
                        area.Bounds.min.y, area.Bounds.size.x, sortingOrder, true);
                    Sprite left = _content.FieldIceLeft, right = _content.FieldIceRight;
                    if (left == null || right == null) throw new InvalidOperationException("빙판 양끝 그림을 연결하세요.");
                    float leftWidth = Mathf.Min(left.bounds.size.x, area.Bounds.size.x);
                    float rightWidth = Mathf.Min(right.bounds.size.x, area.Bounds.size.x);
                    AddFieldSprite(view.transform, "Left", left, area.Bounds.min.x, area.Bounds.min.y, leftWidth, sortingOrder + 1, false);
                    AddFieldSprite(view.transform, "Right", right, area.Bounds.max.x - rightWidth, area.Bounds.min.y, rightWidth, sortingOrder + 1, false);
                }
                else AddFieldSprite(view.transform, "Debris", _content.FieldFire, area.Bounds.min.x,
                    area.Bounds.min.y, area.Bounds.size.x, sortingOrder, false);
                view.SetActive(true);
            }
            _fieldRoot.SetActive(true);
        }
        private void AddFieldSprite(Transform parent, string name, Sprite sprite, float x, float y, float width, int order, bool tiled)
        {
            if (sprite == null) throw new InvalidOperationException("필드 그림을 연결하세요: " + name);
            var child = new GameObject(name); child.SetActive(false); child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingLayerID = _playerBody.sortingLayerID; renderer.sortingOrder = order; renderer.color = Color.white;
            float height;
            if (tiled)
            {
                renderer.drawMode = SpriteDrawMode.Tiled; renderer.tileMode = SpriteTileMode.Continuous;
                height = sprite.rect.height / sprite.pixelsPerUnit;
                renderer.size = new Vector2(width, height);
                child.transform.position = new Vector3(x + width / 2f, y + height / 2f, 0f);
            }
            else
            {
                float scale = width / sprite.bounds.size.x;
                height = sprite.bounds.size.y * scale;
                child.transform.localScale = Vector3.one * scale;
                child.transform.position = new Vector3(x + width / 2f - sprite.bounds.center.x * scale,
                    y + height / 2f - sprite.bounds.center.y * scale, 0f);
            }
            child.SetActive(true);
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
            var root = _fieldRoot;
            _fieldRoot = null;
            SubscriptionScope.Cleanup(
                () => { if (root != null) root.SetActive(false); },
                () => { if (root != null) Destroy(root); });
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
