using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HumanGhost.Data.Csv;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;
using Newtonsoft.Json.Linq;

internal static class ResourceConnectionChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var assets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string meta in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.meta", SearchOption.AllDirectories))
        {
            string line = File.ReadLines(meta).Take(3).FirstOrDefault(value => value.StartsWith("guid: ", StringComparison.Ordinal));
            if (line == null) continue;
            string guid = line.Substring(6).Trim();
            if (!assets.TryGetValue(guid, out var paths)) assets.Add(guid, paths = new List<string>());
            paths.Add(meta.Substring(0, meta.Length - 5));
        }
        string Asset(string guid, string location)
        {
            check(assets.TryGetValue(guid, out var paths) && paths.Count == 1 && File.Exists(paths[0]),
                "리소스 연결 오류: " + location + "의 GUID가 없거나 중복됐습니다: " + guid);
            return assets[guid][0];
        }
        var parser = new CsvParser();
        var stages = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Stage.csv"))).Rows
            .ToDictionary(row => int.Parse(row.Get("ID")));
        var monsters = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Monster.csv"))).Rows
            .Select(row => int.Parse(row.Get("ID"))).ToHashSet();
        string config = File.ReadAllText(Path.Combine(root, "Assets/2.Model/Prefabs/Config/StageContentSet.asset"));
        foreach (Match reference in Regex.Matches(config, @"guid: ([a-f0-9]{32})")) Asset(reference.Groups[1].Value, "StageContentSet");
        string platformSection = config.Split(new[] { "  _platforms:" }, StringSplitOptions.None)[1]
            .Split(new[] { "  _monsters:" }, StringSplitOptions.None)[0];
        var platforms = Regex.Matches(platformSection, @"guid: ([a-f0-9]{32})").Cast<Match>()
            .Select(match => Path.GetFileNameWithoutExtension(Asset(match.Groups[1].Value, "플랫폼 목록"))).ToHashSet(StringComparer.Ordinal);
        var monsterPrefabs = Regex.Matches(config, @"- Code: (\d+)\s+Prefab: \{[^}]*guid: ([a-f0-9]{32})").Cast<Match>()
            .ToDictionary(match => int.Parse(match.Groups[1].Value), match => Asset(match.Groups[2].Value, "몬스터 프리팹 목록"));
        foreach (int code in monsterPrefabs.Keys) check(monsters.Contains(code), "몬스터 프리팹 코드의 CSV 참조가 없습니다: " + code);
        foreach (Match entry in Regex.Matches(config, @"(?ms)^  - StageId: (\d+)\r?\n(.*?)(?=^  - StageId:|^  _platforms:|\z)"))
        {
            int stageId = int.Parse(entry.Groups[1].Value);
            check(stages.ContainsKey(stageId), "StageContentSet의 Stage CSV 참조가 없습니다: " + stageId);
            Match spawn = Regex.Match(entry.Groups[2].Value, @"Spawn: \{[^}]*guid: ([a-f0-9]{32})");
            // 기존 보스 전용 맵처럼 Spawn을 연결하지 않는 실행 경로는 그대로 구분합니다.
            if (!spawn.Success) continue;
            string path = Asset(spawn.Groups[1].Value, "스테이지 " + stageId + " 스폰");
            string declared = Path.GetFullPath(Path.Combine(root, "Assets/Data/Json", stages[stageId].Get("SpawnFile")));
            check(string.Equals(Path.GetFullPath(path), declared, StringComparison.OrdinalIgnoreCase), "Stage.SpawnFile과 실제 스폰 참조가 다릅니다: " + stageId);
            string source = File.ReadAllText(path);
            var tokens = (JContainer)JToken.Parse(source);
            check(!tokens.DescendantsAndSelf().OfType<JValue>().Any(value => value.Type == JTokenType.Float), "스폰 정수 필드에 소수가 있습니다: " + path);
            StageLayout data = StageLayoutReader.Read(source);
            check(data.StageID == stageId, "스폰 JSON의 StageID가 다릅니다: " + path);
            int count = 0;
            foreach (StagePlacement placement in data.Placements)
            {
                check(placement.PlacementID > 0 && platforms.Contains(placement.Prefab), "스폰 플랫폼의 배치 ID·프리팹 연결이 잘못됐습니다: " + path);
                foreach (StageMonsterGroup group in placement.Monsters)
                {
                    check(group.MonsterCount >= 0, "몬스터 수가 음수입니다: " + path);
                    check(monsters.Contains(group.MonsterCode) && monsterPrefabs.ContainsKey(group.MonsterCode), "스폰 몬스터의 CSV·프리팹 연결이 없습니다: " + group.MonsterCode);
                    count = checked(count + group.MonsterCount);
                }
            }
            var warnings = new List<string>();
            StageLayoutRules.ResolveMonsters(data, monsters.Contains, _ => true, warnings);
            check(warnings.Count == 0, "스폰 실행 연결 오류: " + path + " / " + string.Join(" / ", warnings));
            check(data.MaxMonsterCount >= 0 && count <= data.MaxMonsterCount, "실제 스폰 수가 JSON의 최대 수를 넘습니다: " + path);
        }
        string audio = File.ReadAllText(Path.Combine(root, "Assets/2.Model/Prefabs/Config/GameAudio.prefab"));
        var soundRows = parser.Read(File.ReadAllText(Path.Combine(root, "Assets/Data/Csv/Sound.csv"))).Rows
            .ToDictionary(row => int.Parse(row.Get("ID")));
        var audioKeys = new HashSet<string>(StringComparer.Ordinal);
        var soundAddresses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match entry in Regex.Matches(audio, @"- _key: ([^\r\n]+)\r?\n\s+_clip: \{[^}]*guid: ([a-f0-9]{32})[^}]*\}\r?\n\s+_soundId: (\d+)"))
        {
            string key = entry.Groups[1].Value.Trim().Trim('"');
            audioKeys.Add(key);
            string clip = Asset(entry.Groups[2].Value, "소리 " + key);
            int id = int.Parse(entry.Groups[3].Value);
            if (id == 0) continue; // 기존 문자열 소리 키는 Sound ID 없이도 동작합니다.
            check(soundRows.ContainsKey(id), "GameAudio의 Sound CSV 참조가 없습니다: " + id);
            string address = soundRows[id].Get("Address");
            check(!string.IsNullOrWhiteSpace(address), "Sound 주소가 비어 있습니다: " + id);
            check(!soundAddresses.TryGetValue(address, out string previous) || previous == clip,
                "같은 Sound 주소에 다른 클립이 연결됐습니다: " + address);
            soundAddresses[address] = clip;
        }
        foreach (var stage in stages.Values)
            foreach (string column in new[] { "BGM_Normal", "BGM_Battle" })
            {
                string key = stage.Get(column);
                check(string.IsNullOrEmpty(key) || audioKeys.Contains(key), "Stage[" + stage.Get("ID") + "]." + column + "의 소리 키가 없습니다: " + key);
            }
    }
}
