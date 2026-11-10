using System;
using System.IO;
using System.Text.RegularExpressions;

// 실제 화면이 숨겨진 채 입력을 가져갔던 프리팹·씬 설정의 회귀 검사입니다.
internal static class UiPrefabChecks
{
    public static void Run(string projectRoot, Action<bool> check)
    {
        string Read(string path) => File.ReadAllText(Path.Combine(projectRoot, path));
        string settings = Read("Assets/2.Model/Prefabs/UI/SettingsScreen.prefab");
        string root = RootRect(settings);
        check(root.Contains("m_LocalScale: {x: 1, y: 1, z: 1}"));
        check(root.Contains("m_AnchorMin: {x: 0, y: 0}") && root.Contains("m_AnchorMax: {x: 1, y: 1}"));
        check(settings.Contains("m_OverrideSorting: 1"));
        string host = RootRect(Read("Assets/2.Model/Prefabs/UI/ErrorPopupHost.prefab"));
        check(host.Contains("m_AnchorMin: {x: 0, y: 0}") && host.Contains("m_AnchorMax: {x: 1, y: 1}"));
        string title = Read("Assets/2.Model/Prefabs/UI/TitleScreen.prefab");
        Match reference = Regex.Match(title, @"_errors: \{fileID: (\d+)\}");
        check(reference.Success && Regex.IsMatch(title, @"(?m)^--- !u!114 &" + reference.Groups[1].Value + @"\r?$"));
        string scene = Read("Assets/1.Scene/Title.unity");
        check(!Regex.IsMatch(scene, @"propertyPath: m_LocalScale\.[xy]\s+value: 0\s"));
        string shrine = Read("Assets/2.Model/Prefabs/UI/ShrineScreen.prefab");
        string shrineRoot = RootRect(shrine);
        check(shrineRoot.Contains("m_LocalScale: {x: 1, y: 1, z: 1}") && shrineRoot.Contains("m_AnchorMax: {x: 1, y: 1}"));
        check(shrine.Contains("HumanGhost.Gameplay.Presentation.ShrineScreen") && shrine.Contains("_pipOn:") && shrine.Contains("_pipOff:"));
        string map = Read("Assets/1.Scene/MapDohwagolTest.unity");
        Match shrineGuid = Regex.Match(Read("Assets/2.Model/Prefabs/UI/ShrineScreen.prefab.meta"), @"(?m)^guid: ([a-f0-9]{32})\r?$");
        check(shrineGuid.Success && map.Contains("_shrinePrefab: {fileID: 900010, guid: " + shrineGuid.Groups[1].Value + ", type: 3}"));
        Match modal = Regex.Match(map, @"_shrineModalBinding: \{fileID: (\d+)\}");
        check(modal.Success && Regex.IsMatch(map, @"(?m)^--- !u!114 &" + modal.Groups[1].Value + @"\r?$"));
    }

    private static string RootRect(string yaml)
    {
        foreach (Match block in Regex.Matches(yaml, @"(?ms)^--- !u!224 &\d+\r?\n.*?(?=^--- !u!|\z)"))
            if (block.Value.Contains("m_Father: {fileID: 0}")) return block.Value;
        throw new InvalidOperationException("화면 루트에 RectTransform이 없습니다.");
    }
}
