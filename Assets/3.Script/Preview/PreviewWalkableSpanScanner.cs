using System;
using System.Collections.Generic;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 몬스터가 선 바닥의 끊기지 않은 보행 구간을 찾습니다(S05: GameplayPreview 조립자에서 분리, 2026-11-18).
    // 입력은 정수 발 위치·몸 크기, 출력은 정수 구간입니다. 조립자는 결과를 드라이버에 넘기기만 하고 탐사 규칙·물리 조회는 이 클래스가 소유합니다.
    // 맵 준비 때 한 번 만들어 쓰고 버립니다. 상태는 조회 결과 목록 재사용과 측정용 조회 횟수뿐입니다.
    // [정수화 미적용 이유] Physics2D.OverlapPoint·OverlapArea는 float 좌표만 받으므로 각 호출 순간에만 월드 좌표로 바꿉니다.
    internal sealed class PreviewWalkableSpanScanner
    {
        private const long _spanStep = IntegerPosition.UnitsPerTile / 4;
        private const long _surfaceScanStep = IntegerPosition.UnitsPerTile / 96;
        private const long _footProbeBelow = IntegerPosition.UnitsPerTile / 10;
        private const long _surfaceProbeBelow = IntegerPosition.UnitsPerTile / 100;
        private const long _maxFloorDrop = IntegerPosition.UnitsPerTile / 2;
        // 영역 조회가 점 조회보다 넓게 잡히도록 두는 여유입니다(1/1000칸).
        private const long _slopeAreaMargin = IntegerPosition.UnitsPerTile / 1000;
        private const int _maxSteps = 800;

        // [정수화 미적용 이유] 정수 좌표(IntegerPosition)와 Unity 월드 좌표를 오가는 변환 계수(Inspector 타일당 화면 거리)입니다. 좌표 상태는 정수입니다.
        private readonly float _worldUnitsPerTile;
        private readonly ContactFilter2D _filter;
        // 몬스터마다 수백 번 검사하므로 결과 목록 하나를 재사용합니다.
        private readonly List<Collider2D> _hits = new List<Collider2D>();

        // 물리 조회 횟수입니다. 편집기 비교 모드의 측정에만 씁니다.
        public long QueryCount { get; private set; }

        public PreviewWalkableSpanScanner(float worldUnitsPerTile)
        {
            if (worldUnitsPerTile <= 0f || float.IsNaN(worldUnitsPerTile) || float.IsInfinity(worldUnitsPerTile))
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerTile));
            _worldUnitsPerTile = worldUnitsPerTile;
            _filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            _filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        }

        // 몬스터는 낙하하지 않으므로 배치 높이가 그림에 맞춰 낮춘 바닥보다 조금 높으면 바로 아래 바닥까지의 거리를 돌려줍니다.
        // 정수 단계로 내려가며 실수 누적 없이 0.5칸 안을 1/96칸 간격으로 찾습니다. 없으면 0입니다.
        public long FindFloorDrop(long feetX, long feetY)
        {
            for (long drop = 0; drop <= _maxFloorDrop; drop += _surfaceScanStep)
                if (SolidAt(feetX, feetY - drop - _surfaceProbeBelow) != null) return drop;
            return 0;
        }

        // 발 중심 기준 보행 구간입니다. 몸 절반 폭만큼 안쪽으로 줄이고, 몸체보다 좁은 지형은 최초 배치점을 유지합니다.
        // useShortcut=false는 편집기 비교용 기존 경로입니다.
        public void FindSpan(long feetX, long feetY, long halfHeight, long halfWidth, bool useShortcut, out long min, out long max)
        {
            min = checked(FindSpanEnd(feetX, feetY, halfHeight, -1, useShortcut) + halfWidth);
            max = checked(FindSpanEnd(feetX, feetY, halfHeight, 1, useShortcut) - halfWidth);
            if (min > max) min = max = feetX;
        }

        // 0.25칸 간격으로 훑고, 한 칸에서 따라갈 높이 한도는 이동과 같은 GroundSlopeRule(최대 45°)에서 만듭니다.
        // useShortcut: 현재 바닥이 평지이고 다음 지점의 높이 탐색 범위에 경사 콜라이더가 없으면 높이 스캔을 건너뜁니다.
        // 그 경우 원래 조건(현재 또는 다음 바닥이 경사)은 반드시 거짓이므로 결과가 같습니다.
        private long FindSpanEnd(long feetX, long feetY, long halfHeight, int direction, bool useShortcut)
        {
            long reach = GroundSlopeRule.MaxRise(_spanStep);
            long x = feetX, y = feetY;
            Collider2D floor = SolidAt(x, y - _footProbeBelow);
            for (int i = 0; i < _maxSteps; i++)
            {
                long next = checked(x + direction * _spanStep);
                bool slopePossible = !useShortcut || PreviewCollision.IsGroundSlope(floor) || HasSlopeInScan(next, y, reach);
                // 경사 지면은 이동의 경사 추종과 같은 기준(GroundSlope_)으로 표면 높이를 따라갑니다.
                if (slopePossible && TryFindSurface(next, y, reach, out long surface, out Collider2D nextFloor) &&
                    (PreviewCollision.IsGroundSlope(floor) || PreviewCollision.IsGroundSlope(nextFloor)))
                {
                    if (SolidAt(next, checked(surface + halfHeight)) != null) break;
                    x = next; y = surface; floor = nextFloor;
                    continue;
                }
                // 평지: 앞이 막히거나 발밑이 비면 그 앞에서 멈춥니다.
                if (SolidAt(next, checked(y + halfHeight)) != null) break;
                Collider2D flat = SolidAt(next, y - _footProbeBelow);
                if (flat == null) break;
                x = next; floor = flat;
            }
            return x;
        }

        // TryFindSurface가 점 조회할 세로 범위([y-reach-probe, y+reach], x 고정)를 감싸는 영역에 경사 콜라이더가 있는지 한 번에 봅니다.
        // 영역은 점 조회의 상위 집합이라 경사를 놓치지 않습니다. 과하게 잡힌 경우에는 기존 높이 스캔을 그대로 실행합니다.
        private bool HasSlopeInScan(long x, long y, long reach)
        {
            QueryCount++;
            _hits.Clear();
            Vector2 low = new Vector2(World(x - _slopeAreaMargin), World(checked(y - reach - _surfaceProbeBelow - _slopeAreaMargin)));
            Vector2 high = new Vector2(World(x + _slopeAreaMargin), World(checked(y + reach + _slopeAreaMargin)));
            Physics2D.OverlapArea(low, high, _filter, _hits);
            foreach (Collider2D hit in _hits)
                if (IsSolid(hit) && PreviewCollision.IsGroundSlope(hit)) return true;
            return false;
        }

        // x 위치에서 기준 높이 ±reach 안의 가장 높은 지면 윗면을 찾습니다. 시작점이 이미 막혀 있으면 가파른 벽으로 봅니다.
        private bool TryFindSurface(long x, long y, long reach, out long surface, out Collider2D floor)
        {
            surface = y; floor = null;
            if (SolidAt(x, checked(y + reach)) != null) return false;
            for (long height = checked(y + reach); height >= y - reach; height -= _surfaceScanStep)
            {
                floor = SolidAt(x, height - _surfaceProbeBelow);
                if (floor == null) continue;
                surface = height;
                return true;
            }
            return false;
        }

        private Collider2D SolidAt(long x, long y)
        {
            QueryCount++;
            _hits.Clear();
            Physics2D.OverlapPoint(new Vector2(World(x), World(y)), _filter, _hits);
            foreach (Collider2D hit in _hits)
                if (IsSolid(hit)) return hit;
            return null;
        }

        // 지형만 지지면으로 봅니다. 트리거·캐릭터 자신의 충돌체는 제외합니다.
        private static bool IsSolid(Collider2D hit) =>
            !hit.isTrigger && hit.GetComponentInParent<MonsterRuntime>() == null && hit.GetComponentInParent<PlayerRuntime>() == null;

        private float World(long units) => SpatialRuntime.ToWorld(units, _worldUnitsPerTile);
    }
}
