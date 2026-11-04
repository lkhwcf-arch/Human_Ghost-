using System;
using System.IO;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 설정 파일(O05)입니다. 세이브와 다른 파일이며 없거나 깨지면 기본값으로 시작합니다.
    public static class GameSettingsStore
    {
        [Serializable]
        private sealed class Payload { public int Music = 8; public int Effects = 8; public bool CameraShake = true; }

        private static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        public static GameSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new GameSettings();
                Payload data = JsonUtility.FromJson<Payload>(File.ReadAllText(FilePath));
                return data == null ? new GameSettings() : new GameSettings(data.Music, data.Effects, data.CameraShake);
            }
            catch (Exception error)
            {
                Debug.LogWarning("설정 파일을 읽지 못해 기본값으로 시작합니다.\n" + error.Message);
                return new GameSettings();
            }
        }

        public static void Save(GameSettings settings)
        {
            var data = new Payload { Music = settings.Music, Effects = settings.Effects, CameraShake = settings.CameraShake };
            File.WriteAllText(FilePath, JsonUtility.ToJson(data));
        }
    }
}
