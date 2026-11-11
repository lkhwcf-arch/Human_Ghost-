using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Presentation
{
    // 표시 프레임 확정(10) 뒤 복사합니다. 전투 상태·애니메이션 시간은 소유하지 않습니다.
    [DefaultExecutionOrder(20)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteSilhouette : MonoBehaviour
    {
        [SerializeField, InspectorName("따라 그릴 렌더러")] private SpriteRenderer _source = null;
        private SpriteRenderer _shadow;
        private Sprite _sprite;
        private readonly Dictionary<Sprite, Sprite> _frames = new Dictionary<Sprite, Sprite>();
        private MaterialPropertyBlock _properties;
        private static readonly int _uvRect = Shader.PropertyToID("_SpriteUVRect");

        private void Awake()
        {
            _shadow = GetComponent<SpriteRenderer>();
            if (_source == null || _source == _shadow)
            {
                enabled = false;
                throw new InvalidOperationException("그림자가 따라 그릴 원본 렌더러를 연결해 주세요.");
            }
            // 운영 씬은 기본 Body 대신 별도 표시 렌더러를 사용할 수 있습니다.
            if (transform.parent != _source.transform) transform.SetParent(_source.transform, false);
            _properties = new MaterialPropertyBlock();
        }

        private void OnEnable() => LateUpdate();
        private void LateUpdate()
        {
            if (_shadow == null) return;
            if (_source == null) { _shadow.enabled = false; return; }
            _shadow.flipX = _source.flipX;
            _shadow.flipY = _source.flipY;
            _shadow.color = new Color(0f, 0f, 0f, _source.color.a);
            _shadow.sortingLayerID = _source.sortingLayerID;
            _shadow.sortingOrder = _source.sortingOrder - 1;
            _shadow.maskInteraction = _source.maskInteraction;
            _shadow.enabled = _source.enabled && _source.sprite != null;
            if (_sprite == _source.sprite) return;
            _sprite = _source.sprite;
            if (_sprite == null) { _shadow.sprite = null; return; }
            // 원본의 Tight 메시가 블러 가장자리를 자르지 않게 표시용 사각 메시만 재사용합니다.
            if (!_frames.TryGetValue(_sprite, out Sprite frame))
            {
                frame = Sprite.Create(_sprite.texture, _sprite.rect,
                    _sprite.pivot / _sprite.rect.size, _sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                frame.hideFlags = HideFlags.DontSave;
                _frames.Add(_sprite, frame);
            }
            _shadow.sprite = frame;
            Rect rect = _sprite.textureRect;
            Texture texture = _sprite.texture;
            _properties.SetVector(_uvRect, new Vector4(rect.xMin / texture.width, rect.yMin / texture.height,
                rect.xMax / texture.width, rect.yMax / texture.height));
            _shadow.SetPropertyBlock(_properties);
        }

        private void OnDestroy()
        {
            foreach (Sprite frame in _frames.Values)
                if (Application.isPlaying) Destroy(frame); else DestroyImmediate(frame);
            _frames.Clear();
        }

        private void OnDisable() { if (_shadow != null) _shadow.enabled = false; }
    }
}
