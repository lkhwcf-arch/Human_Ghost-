using System;
using System.Reflection;
using System.Threading;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HumanGhost.EditorTools
{
    public static class CameraLookVerification
    {
        private static int _checks;
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("카메라 검증은 별도 Unity 배치 프로세스에서 실행해 주세요.");
            EditorSceneManager.OpenScene("Assets/1.Scene/MapDohwagolTest.unity");
            var camera = UnityEngine.Object.FindFirstObjectByType<PreviewFollowCamera>();
            var target = (Transform)Field(camera, "_target").GetValue(camera);
            Check(target.GetComponentInParent<PlayerRuntime>()?.GetComponent<PlayerLocomotionInput>() != null,
                "씬 카메라 대상에 실제 플레이어·입력이 연결됨");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool("CameraLookVerification.Active", true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CameraLookVerification.Active", false)) return;
                SessionState.SetBool("CameraLookVerification.Active", false);
                EditorApplication.delayCall += RunAsync;
            };
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            _checks++;
            Debug.Log("CAMERA_LOOK_PASS: " + message);
        }
        private static async void RunAsync()
        {
            Keyboard keyboard = null;
            var previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            var previousUpdateMode = InputSystem.settings.updateMode;
            try
            {
                var scope = new GameObject("CameraLookVerificationScope");
                scope.SetActive(false);
                var playerObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/MOP/PlayerGameplay.prefab"), scope.transform);
                var player = playerObject.GetComponent<PlayerRuntime>();
                var input = player.GetComponent<PlayerLocomotionInput>();
                var cameraObject = new GameObject("CameraLookVerificationCamera", typeof(Camera));
                cameraObject.SetActive(false);
                var camera = cameraObject.AddComponent<PreviewFollowCamera>();
                Field(camera, "_target").SetValue(camera, player.transform);
                var assets = AssetDatabase.LoadAssetAtPath<GameDataAssets>("Assets/Data/GameDataAssets.asset");
                var data = await new DataBootstrapper(assets.CreateSource(), path => path).InitializeAsync(CancellationToken.None);
                player.Initialize(data, 100, true);
                Call(input, "Subscribe");
                Call(camera, "Awake");
                // 비활성 격리 범위는 실제 씬의 활성 부모 탐색을 실행하지 않으므로 검증한 동일 대상을 공급합니다.
                Field(camera, "_player").SetValue(camera, player);
                Field(camera, "_input").SetValue(camera, input);
                camera.SetArea(-100f, 100f, -100f, 5f);
                keyboard = InputSystem.AddDevice<Keyboard>("CameraVerificationKeyboard");
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                ((InputActionAsset)Field(input, "_ownedAsset").GetValue(input)).devices = new InputDevice[] { keyboard };
                Call(input, "OnDisable"); Call(input, "Subscribe");
                float scale = player.ToWorld(IntegerPosition.UnitsPerTile);
                float Goal() => ((Vector3)Call(camera, "GetGoal")).y;
                void Focus(int milliseconds) => Call(camera, "UpdateFocus", milliseconds);
                void Press(params Key[] keys)
                {
                    InputState.Change(keyboard, new KeyboardState(keys));
                }
                foreach (var layout in new[] { KeyboardLayout.Arrows, KeyboardLayout.Wasd })
                {
                    Press();
                    KeyboardControls.SetLayout(layout);
                    Field(input, "_unlockFrame").SetValue(input, -1);
                    if (!(bool)Field(input, "_subscribed").GetValue(input)) Call(input, "Subscribe");
                    Call(camera, "ResetFocus");
                    float baseline = Goal();
                    foreach (int direction in new[] { 1, -1 })
                    {
                        Key key = layout == KeyboardLayout.Arrows ? (direction == 1 ? Key.UpArrow : Key.DownArrow)
                            : direction == 1 ? Key.W : Key.S;
                        Press(key);
                        Check(input.VerticalDirection == direction, layout + " 실제 InputAction 상하 입력 " + direction + " / 실제값 " + input.VerticalDirection);
                        Focus(499);
                        Check(Mathf.Abs(Goal() - baseline) < .001f, layout + " 499ms에는 시점 이동 없음");
                        Focus(1);
                        Check(Mathf.Abs(Goal() - baseline - direction * (direction < 0 ? 6f : 3f) * scale) < .001f,
                            layout + " 500ms에 카메라 목표 높이 " + direction * (direction < 0 ? 6f : 3f) * scale + " / 실제 변화 " + (Goal() - baseline));
                        Press(); Focus(1);
                        Check(input.VerticalDirection == 0 && Mathf.Abs(Goal() - baseline) < .001f, layout + " 키 해제 후 기본 목표 높이 복귀");
                    }
                    Press(layout == KeyboardLayout.Arrows ? Key.UpArrow : Key.W);
                    player.Actor.Locomotion.SetGrounded(false); Focus(1000);
                    Check(Mathf.Abs(Goal() - baseline) < .001f, layout + " 공중에서는 시점 이동 없음");
                    player.Actor.Locomotion.SetGrounded(true);
                    camera.LookEnabled = false; Focus(1000);
                    Check(Mathf.Abs(Goal() - baseline) < .001f, layout + " 장부방 비활성 정책에서는 시점 이동 없음");
                    camera.LookEnabled = true;
                    input.SetLocked(true); Focus(1000);
                    Check(input.VerticalDirection == 0 && Mathf.Abs(Goal() - baseline) < .001f, layout + " 입력 잠금 시 시점 이동 없음");
                    input.SetLocked(false); Field(input, "_unlockFrame").SetValue(input, -1);
                    Press();
                }
                Debug.Log("CAMERA_LOOK_RESULT: " + _checks + "개 통과. 입력·목표 좌표 검사이며 실제 화면 수용과 구분합니다.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("CAMERA_LOOK_FAILED: " + error);
                EditorApplication.Exit(1);
            }
            finally
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior;
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
                InputSystem.settings.updateMode = previousUpdateMode;
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
