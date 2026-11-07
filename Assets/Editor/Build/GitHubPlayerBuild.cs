using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HumanGhost.Editor.Build
{
    // 빌드 대상과 출력만 담당합니다. 알림·인증·결과물 보관은 GitHub가 소유합니다.
    public static class GitHubPlayerBuild
    {
        public static void BuildWindows()
        {
            try
            {
                BuildWindowsCore();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError($"윈도우 게임 빌드에 실패했습니다.\n{error}");
                EditorApplication.Exit(1);
            }
        }

        private static void BuildWindowsCore()
        {
            string mode = ReadArgument("-humanGhostBuildMode", "development");
            BuildOptions options = mode == "development" ? BuildOptions.Development
                : mode == "release" ? BuildOptions.None
                : throw new BuildFailedException("지원하지 않는 빌드 종류입니다. development 또는 release를 지정해 주세요.");
            string modeName = mode == "release" ? "정식" : "테스트";
            string scene = ReadArgument("-humanGhostScene", "build-settings");
            string[] scenes = scene == "dohwagol"
                ? new[] { "Assets/1.Scene/MapDohwagolTest.unity" }
                : scene == "preview"
                    ? new[] { "Assets/1.Scene/GameplayPreview.unity" }
                    : scene == "build-settings"
                        ? EditorBuildSettings.scenes.Where(entry => entry.enabled).Select(entry => entry.path).ToArray()
                        : throw new BuildFailedException("지원하지 않는 시작 장면 설정입니다.");
            if (scenes.Length == 0 || scenes.Any(path => !File.Exists(path)))
                throw new BuildFailedException("빌드에 포함할 씬이 없거나 씬 파일을 찾을 수 없습니다.");

            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "build", "StandaloneWindows64", "HumanGhost.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var target = NamedBuildTarget.Standalone;
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(target);
            string version = PlayerSettings.bundleVersion;
            try
            {
                // 두 종류 모두 기존 Linux 실행 환경과 Windows 64비트 Mono 출력을 공유합니다.
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
                PlayerSettings.bundleVersion = ReadArgument("-humanGhostVersion", version);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = BuildTarget.StandaloneWindows64,
                    locationPathName = output,
                    options = options
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("윈도우 게임 빌드가 성공하지 못했습니다. 빌드 로그를 확인해주세요.");
                Debug.Log($"윈도우 {modeName} 게임 빌드를 완료했습니다.");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, backend);
                PlayerSettings.bundleVersion = version;
            }
        }

        private static string ReadArgument(string name, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            if (index < 0) return fallback;
            if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new BuildFailedException($"빌드 인자 {name}의 값이 필요합니다.");
            return arguments[index + 1];
        }
    }
}
