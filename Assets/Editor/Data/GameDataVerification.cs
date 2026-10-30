using System;
using System.IO;
using System.Threading;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HumanGhost.Editor.Data
{
    // 일괄 검증은 환경 변수로 지정한 임시 폴더에만 실행 결과를 만듭니다.
    public static class GameDataVerification
    {
        public static void Run() => EditorApplication.delayCall += RunAsync;

        private static async void RunAsync()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/1.Scene/SampleScene.unity");
                GameDataSceneSetup.ConfigureCurrentScene();
                EditorSceneManager.SaveOpenScenes();
                GameDataAssets assets = GameDataAssetBuilder.Refresh();
                var loader = new DataBootstrapper(assets.CreateSource(), path => path);
                var data = await loader.InitializeAsync(CancellationToken.None);
                if (data.GetTable<UnitStat>().Count == 0 || data.GetTable<Item>().Count == 0)
                    throw new InvalidOperationException("실제 에셋을 사용한 데이터 초기화 결과가 비어 있습니다.");
                Debug.Log("실제 텍스트 에셋을 사용한 데이터 초기화 검사를 통과했습니다.");
                string output = Environment.GetEnvironmentVariable("HUMANGHOST_VERIFY_ROOT");
                if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("검증 결과용 임시 경로가 필요합니다.");
                Directory.CreateDirectory(Path.Combine(output, "Player"));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/1.Scene/SampleScene.unity" },
                    locationPathName = Path.Combine(output, "Player", "HumanGhost.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("윈도우 실행 파일 빌드가 실패했습니다.");
                Debug.Log("데이터 에셋을 포함한 윈도우 빌드 검사를 통과했습니다.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError($"데이터 연결 검증이 실패했습니다.\n{error}");
                EditorApplication.Exit(1);
            }
        }
    }
}
