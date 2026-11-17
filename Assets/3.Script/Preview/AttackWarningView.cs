using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 공격 소유자의 정수 진행 시간을 관찰합니다. 예고는 판정·상태·피격 섬광을 변경하지 않습니다.
    [DefaultExecutionOrder(1200)]
    public sealed class AttackWarningView : MonoBehaviour
    {
        [SerializeField] private MonsterRuntime _monster = null;
        [SerializeField] private BossRuntime _boss = null;
        [SerializeField] private SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
        [SerializeField, Range(0, 10000)] private int _intensity = 9000;
        [SerializeField, Min(1)] private int _pulseMilliseconds = 200;
        [SerializeField] private Color _parryColor = Color.white;
        [SerializeField] private Color _unparryColor = Color.red;
        private MaterialPropertyBlock _properties;
        private static readonly int _amount = Shader.PropertyToID("_WarningAmount");
        private static readonly int _color = Shader.PropertyToID("_WarningColor");
        private void Awake()
        {
            if ((_monster == null) == (_boss == null) || _renderers.Length == 0 || _pulseMilliseconds <= 0)
                throw new InvalidOperationException("공격 예고의 적 하나와 스프라이트를 연결해 주세요.");
            _properties = new MaterialPropertyBlock();
        }
        private void LateUpdate()
        {
            int elapsed = 0, windup = 0; bool parryable = false;
            var pattern = _monster != null ? _monster.Actor?.CurrentPattern : null;
            // 물러서기는 공격이 아니므로 번쩍이지 않습니다.
            if (pattern != null && !pattern.IsRetreat) { elapsed = _monster.Actor.AttackElapsed; windup = pattern.Attack.Windup; parryable = pattern.IsParryable; }
            else if (_boss != null && _boss.Combat != null && _boss.Combat.CurrentPattern != null)
            {
                var combat = _boss.Combat; elapsed = combat.HitElapsed;
                windup = combat.CurrentPattern.Hits[combat.HitIndex].Windup; parryable = combat.IsParryable;
            }
            int pulse = Math.Min(_pulseMilliseconds, Math.Max(1, windup / 5 * 2));
            bool on = windup > 0 && elapsed < windup && (elapsed < pulse || elapsed >= windup - pulse);
            Apply(on ? Mathf.Clamp(_intensity, 0, 10000) / 10000f : 0f, parryable ? _parryColor : _unparryColor);
        }
        private void Apply(float amount, Color color)
        {
            if (_properties == null) return;
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_properties);
                _properties.SetFloat(_amount, amount); _properties.SetColor(_color, color);
                renderer.SetPropertyBlock(_properties);
            }
        }
        private void OnDisable() => Apply(0f, Color.white);
    }
}
