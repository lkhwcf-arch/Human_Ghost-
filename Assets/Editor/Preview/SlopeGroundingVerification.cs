using System;
using System.Collections.Generic;
using System.Reflection;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.EditorTools
{
    // 비탈길 설계(2026-11-17) 검증: 일반 몬스터의 중앙 발 지지·최대 경사 45°·프레임률 독립을 합성 지형에서 확인합니다.
    // 지형 높이를 식으로 알고 있는 합성 Collider만 사용하고, 열린 씬은 바꾸지 않도록 추가 씬에서 만든 뒤 닫습니다.
    // 편집 모드·Play 모드 모두 메뉴로 실행할 수 있고, 배치 모드에서는 결과로 종료 코드를 돌려줍니다.
    public static class SlopeGroundingVerification
    {
        private const BindingFlags _flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        // 허용 발 간격(0.001칸)입니다. Physics2D 접촉점 float 오차와 정수 변환 오차만 허용합니다.
        // [실수 사용 이유] 비교 대상인 합성 지형 높이 식과 Physics2D 관측이 Unity 월드 float 좌표입니다.
        private const float _gapTolerance = .001f;
        private const float _speed = 3f;
        private static readonly int[] _frameRates = { 30, 60, 144 };
        private static readonly string[] _monsters = { "HusugwiGameplay", "JigegwiGameplay", "OnggwiGameplay" };
        private static int _checks;
        // 몸체(Body) 아래변이 지면보다 낮게 놓인 정도입니다. 현재 프리팹은 그림 보정으로 0.2칸 낮습니다(11/16 30589c84).
        // 0과 0.2 두 경우를 모두 시험해 이동 판정이 그림 보정에 의존하지 않는지 확인합니다.
        private static readonly float[] _bodySinks = { 0f, .2f };
        private static float _sink;
        // 합성 지형 기준점입니다. 열린 맵(편집·Play 모두 같은 기본 물리 공간)의 콜라이더와 겹치지 않도록 멀리 둡니다.
        // 실제 맵 좌표 규모에 가깝게 두어 float 관측 오차(이 좌표에서 약 0.00006칸)까지 함께 시험합니다.
        private static readonly Vector2 _origin = new Vector2(1000f, 1000f);
        private static readonly long _originX = SpatialRuntime.ToInteger(_origin.x, 1f), _originY = SpatialRuntime.ToInteger(_origin.y, 1f);

        [MenuItem("인귀/검사/비탈 접지 검증")]
        public static void Run()
        {
            Scene previous = SceneManager.GetActiveScene();
            // 편집 모드와 Play 모드의 장면 생성 API가 다릅니다. Play 중에는 EditorSceneManager.NewScene을 쓸 수 없습니다.
            Scene scene = Application.isPlaying ? SceneManager.CreateScene("SlopeGroundingVerification")
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool success = false;
            try
            {
                SceneManager.SetActiveScene(scene);
                _checks = 0;
                foreach (float sink in _bodySinks)
                {
                    _sink = sink;
                    foreach (string monster in _monsters)
                    {
                        Vector2 half = ReadBodyHalfSize(monster);
                        string name = $"{monster}(Body {sink:F1}칸 낮음)";
                        VerifyFlat(name, half);
                        VerifyRamp(name, half, .5f, true);
                        VerifyRamp(name, half, 1f, true);
                        VerifyRamp(name, half, 1.2f, false);
                        VerifyValley(name, half);
                        VerifyLowStep(name, half);
                        VerifyWallAndCeiling(name, half);
                    }
                }
                success = true;
                Debug.Log($"SLOPE_GROUNDING_DONE: 검사 {_checks}개 통과");
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                if (Application.isPlaying) SceneManager.UnloadSceneAsync(scene);
                else EditorSceneManager.CloseScene(scene, true);
                if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            }
        }

        private static void Check(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            _checks++;
        }

        // 실제 프리팹 Body의 반크기를 씁니다(PreviewMonsterDriver의 Body bounds와 같은 값: 스프라이트 크기 × 누적 Scale).
        // Play 중 프리팹을 생성하면 몬스터 런타임 스크립트가 실행되므로 생성하지 않고 에셋에서 계산합니다.
        private static Vector2 ReadBodyHalfSize(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/MOP/" + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("몬스터 프리팹이 없습니다: " + name);
            foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name != "Body") continue;
                if (renderer.sprite == null) throw new InvalidOperationException("몬스터 Body 스프라이트가 없습니다: " + name);
                Vector3 extents = Vector3.Scale(renderer.sprite.bounds.extents, renderer.transform.lossyScale);
                return new Vector2(Mathf.Abs(extents.x), Mathf.Abs(extents.y));
            }
            throw new InvalidOperationException("몬스터 Body가 없습니다: " + name);
        }

        // 평지만 왕복합니다. 11/17 회귀(평지에서 제자리 걸음) 재발 방지입니다.
        private static void VerifyFlat(string monster, Vector2 half)
        {
            var terrain = new List<GameObject> { Box("Flat", -8f, 0f, 16f) };
            try
            {
                foreach (int fps in _frameRates)
                {
                    Walk(half, -4f, 4f, fps, x => 0f, $"{monster} 평지 {fps}fps", out bool forward);
                    Walk(half, 4f, -4f, fps, x => 0f, $"{monster} 평지 역방향 {fps}fps", out bool backward);
                    Check(forward && backward, $"{monster} 평지 {fps}fps: 양방향 이동");
                }
            }
            finally { Clear(terrain); }
        }

        // 평지(y=0) → 기울기 slope 오르막(길이 4) → 고원. 45° 이하는 오르고, 초과는 비탈 앞에서 멈춰야 합니다.
        private static void VerifyRamp(string monster, Vector2 half, float slope, bool climbable)
        {
            const float length = 4f;
            float Surface(float x) => x < 0f ? 0f : x < length ? x * slope : length * slope;
            var terrain = new List<GameObject>
            {
                Box("Flat", -8f, 0f, 8f), Box("Plateau", length, length * slope, 8f),
                Slope("GroundSlope_Test", new Vector2(0f, 0f), new Vector2(length, length * slope))
            };
            try
            {
                foreach (int fps in _frameRates)
                {
                    string label = $"{monster} 기울기 {slope} {fps}fps";
                    Vector3 end = Walk(half, -3f, length + 3f, fps, Surface, label, out bool reached);
                    if (climbable) Check(reached, label + ": 오르막을 끝까지 오름");
                    else Check(!reached && end.x <= half.x + .05f, label + ": 45° 초과 면 앞에서 멈춤");
                    if (climbable)
                    {
                        Walk(half, length + 3f, -3f, fps, Surface, label + " 내리막", out bool back);
                        Check(back, label + ": 내리막을 끝까지 내려옴");
                    }
                }
            }
            finally { Clear(terrain); }
        }

        // V자: 내리막 0.5 → 바닥 → 오르막 0.5. 양쪽 비탈에 몸통이 걸치는 바닥에서도 중앙 발이 붙어야 합니다.
        private static void VerifyValley(string monster, Vector2 half)
        {
            float Surface(float x) => x < 0f ? 2f : x < 4f ? 2f - x * .5f : x < 8f ? (x - 4f) * .5f : 2f;
            var terrain = new List<GameObject>
            {
                Box("Left", -8f, 2f, 8f), Box("Right", 8f, 2f, 8f),
                Slope("GroundSlope_Down", new Vector2(0f, 2f), new Vector2(4f, 0f)),
                Slope("GroundSlope_Up", new Vector2(4f, 0f), new Vector2(8f, 2f))
            };
            try
            {
                foreach (int fps in _frameRates)
                {
                    Walk(half, -2f, 10f, fps, Surface, $"{monster} V자 {fps}fps", out bool reached);
                    Check(reached, $"{monster} V자 {fps}fps: 양쪽 비탈 통과");
                }
            }
            finally { Clear(terrain); }
        }

        // 비탈이 아닌 평지 계단(높이 0.5)은 기존 벽 판정으로 막혀야 합니다.
        private static void VerifyLowStep(string monster, Vector2 half)
        {
            var terrain = new List<GameObject> { Box("Flat", -8f, 0f, 16f), Box("Step", 2f, .5f, 4f) };
            try
            {
                foreach (int fps in _frameRates)
                {
                    Vector3 end = Walk(half, -3f, 5f, fps, x => x < 2f ? 0f : .5f, $"{monster} 계단 {fps}fps", out bool reached, checkGap: false);
                    Check(!reached && end.x <= 2f - half.x + .05f, $"{monster} 계단 {fps}fps: 0.5칸 계단에서 막힘");
                }
            }
            finally { Clear(terrain); }
        }

        // 오르막 위의 수직 벽과 낮은 천장은 몸통으로 계속 막아야 합니다.
        private static void VerifyWallAndCeiling(string monster, Vector2 half)
        {
            const float length = 4f;
            var terrain = new List<GameObject>
            {
                Box("Flat", -8f, 0f, 8f), Box("Plateau", length, length * .5f, 8f),
                Slope("GroundSlope_Test", new Vector2(0f, 0f), new Vector2(length, length * .5f)),
                // 왼쪽 면이 x=2인 벽입니다(비탈 높이 1 지점).
                Solid("Wall", new Vector2(2.2f, 2f), new Vector2(.4f, 6f))
            };
            try
            {
                foreach (int fps in _frameRates)
                {
                    Vector3 end = Walk(half, -3f, length + 3f, fps, x => x < 0f ? 0f : Mathf.Min(x, length) * .5f,
                        $"{monster} 비탈 위 벽 {fps}fps", out bool reached, checkGap: false);
                    Check(!reached && end.x + half.x <= 2f + .05f, $"{monster} 비탈 위 벽 {fps}fps: 벽 앞에서 멈춤");
                }
            }
            finally { Clear(terrain); }
            // 비탈 높이 1 지점에서 몸통 윗변(Body가 _sink만큼 낮음)보다 0.2칸 낮은 천장입니다.
            float ceiling = 1f + half.y * 2f - _sink - .2f;
            terrain = new List<GameObject>
            {
                Box("Flat", -8f, 0f, 8f), Box("Plateau", length, length * .5f, 8f),
                Slope("GroundSlope_Test", new Vector2(0f, 0f), new Vector2(length, length * .5f)),
                Solid("Ceiling", new Vector2(4f, ceiling + .5f), new Vector2(4f, 1f))
            };
            try
            {
                foreach (int fps in _frameRates)
                {
                    Vector3 end = Walk(half, -3f, length + 3f, fps, x => x < 0f ? 0f : Mathf.Min(x, length) * .5f,
                        $"{monster} 비탈 위 천장 {fps}fps", out bool reached, checkGap: false);
                    Check(!reached && end.y + half.y <= ceiling + .05f, $"{monster} 비탈 위 천장 {fps}fps: 머리가 천장을 통과하지 않음");
                }
            }
            finally { Clear(terrain); }
        }

        // PreviewMonsterDriver.Step과 같은 순서입니다. 위치는 IntegerPosition 단위 정수로 보관하고(타일당 화면 거리 1),
        // 비탈 추종 성공이면 정수 높이 변화를, 아니면 기존 수평 벽 판정을 적용한 뒤 한 번에 확정합니다.
        private static Vector3 Walk(Vector2 half, float fromX, float toX, int fps, Func<float, float> surface, string label,
            out bool reached, bool checkGap = true)
        {
            Physics2D.SyncTransforms();
            object collision = CreateCollision(half);
            // 드라이버와 같은 정수 API(몸통 중심 X·Y, 정수 이동량)를 씁니다.
            MethodInfo follow = collision.GetType().GetMethod("TryFollowFootSlope", _flags, null,
                new[] { typeof(long), typeof(long), typeof(long), typeof(long).MakeByRefType() }, null)
                ?? throw new MissingMethodException("TryFollowFootSlope(long, long, long, out long)");
            MethodInfo move = collision.GetType().GetMethod("MoveHorizontal", _flags, null,
                new[] { typeof(long), typeof(long), typeof(long) }, null)
                ?? throw new MissingMethodException("MoveHorizontal(long, long, long)");
            long direction = Math.Sign(toX - fromX);
            // 초당 3칸을 정수 ms 프레임으로 나눈 정수 이동량입니다.
            long run = direction * (long)_speed * IntegerPosition.UnitsPerTile / fps;
            // y는 몸통 중심입니다. 프리팹처럼 Body가 지면보다 _sink만큼 낮게 놓인 상태에서 시작합니다.
            long x = ToUnits(fromX), y = ToUnits(surface(fromX) + half.y - _sink), target = ToUnits(toX);
            reached = false;
            for (int step = 0; step < 100000; step++)
            {
                if ((target - x) * direction <= 0) { reached = true; break; }
                // 시험 좌표는 지역 정수 좌표로 두고 물리 조회에 넘길 때만 합성 지형 기준점(정수)을 더합니다.
                long centerX = x + _originX, centerY = y + _originY;
                var args = new object[] { centerX, centerY, run, 0L };
                if ((bool)follow.Invoke(collision, args)) { x += run; y += (long)args[3]; }
                else
                {
                    long moved = (long)move.Invoke(collision, new object[] { centerX, centerY, run });
                    if (moved == 0) break;
                    x += moved;
                }
                if (checkGap)
                {
                    // 발 = Body 아래변 + 그림 보정. 이 값이 실제 지면과 같아야 합니다.
                    float gap = ToWorld(y) - half.y + _sink - surface(ToWorld(x));
                    if (Mathf.Abs(gap) > _gapTolerance)
                        throw new InvalidOperationException($"{label}: x={ToWorld(x):F3}에서 발 간격 {gap:F4}칸");
                }
            }
            if (checkGap) _checks++;
            return new Vector3(ToWorld(x), ToWorld(y), 0f);
        }

        private static long ToUnits(float world) => SpatialRuntime.ToInteger(world, 1f);
        private static float ToWorld(long units) => SpatialRuntime.ToWorld(units, 1f);

        private static object CreateCollision(Vector2 half)
        {
            Type type = typeof(GameplayPreview).Assembly.GetType("HumanGhost.Gameplay.Preview.PreviewFootCollision")
                ?? throw new MissingMemberException("PreviewFootCollision");
            return Activator.CreateInstance(type, _flags, null, new object[] { half, 1f }, null);
        }

        // 윗면이 top이고 x부터 width만큼인 일반 지면입니다.
        private static GameObject Box(string name, float x, float top, float width)
            => Solid(name, new Vector2(x + width * .5f, top - 2f), new Vector2(width, 4f));

        private static GameObject Solid(string name, Vector2 center, Vector2 size)
        {
            var target = new GameObject(name, typeof(BoxCollider2D));
            target.transform.position = center + _origin;
            target.GetComponent<BoxCollider2D>().size = size;
            return target;
        }

        // 윗변이 from→to인 비탈입니다. 실제 지형과 같은 GroundSlope_ 이름·PolygonCollider2D 계약을 따릅니다.
        private static GameObject Slope(string name, Vector2 from, Vector2 to)
        {
            var target = new GameObject(name, typeof(PolygonCollider2D));
            // 꼭짓점은 지역 좌표이고 오브젝트를 기준점에 둡니다.
            target.transform.position = _origin;
            float bottom = Mathf.Min(from.y, to.y) - 4f;
            target.GetComponent<PolygonCollider2D>().points = new[] { from, to, new Vector2(to.x, bottom), new Vector2(from.x, bottom) };
            return target;
        }

        private static void Clear(List<GameObject> terrain)
        {
            foreach (GameObject target in terrain) if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
