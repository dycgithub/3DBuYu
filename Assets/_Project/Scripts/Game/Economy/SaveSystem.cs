using System;
using System.IO;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 存档系统。
    /// 管理游戏数据、资源、设置和通行证进度的持久化。
    /// </summary>
    public static class SaveSystem
    {
        private const string GAME_DATA_FILE = "gamedata.json";
        private const string RESOURCE_DATA_FILE = "resourcedata.json";
        private const string BATTLEPASS_DATA_FILE = "battlepass.json";
        private const string PLAYERLEVEL_DATA_FILE = "playerlevel.json";
        private const string SETTINGS_FILE = "settings.json";
        private const string INPUT_OVERRIDES_FILE = "inputoverrides.json";

        private static string GetSaveDirectory()
        {
            string path = Application.persistentDataPath;
#if UNITY_EDITOR
            path = Path.Combine(Application.dataPath, "../SaveData");
#endif
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        private static string GetFilePath(string fileName)
        {
            return Path.Combine(GetSaveDirectory(), fileName);
        }

        #region 游戏存档

        /// <summary>
        /// 保存游戏统计。
        /// </summary>
        public static void SaveGameData(GameSaveData data)
        {
            WriteJson(GAME_DATA_FILE, data);
        }

        /// <summary>
        /// 加载游戏统计。
        /// </summary>
        public static GameSaveData LoadGameData()
        {
            return ReadJson<GameSaveData>(GAME_DATA_FILE);
        }

        #endregion

        #region 资源存档

        /// <summary>
        /// 保存资源数据（积分）。
        /// </summary>
        public static void SaveResourceData(ResourceManager rm)
        {
            WriteJson(RESOURCE_DATA_FILE, rm.GetSaveData());
        }

        /// <summary>
        /// 加载资源数据（积分）。
        /// </summary>
        public static ResourceSaveData LoadResourceData()
        {
            return ReadJson<ResourceSaveData>(RESOURCE_DATA_FILE);
        }

        #endregion

        #region 通行证存档

        /// <summary>
        /// 保存通行证进度。
        /// </summary>
        public static void SaveBattlePassData(BattlePassSaveData data)
        {
            WriteJson(BATTLEPASS_DATA_FILE, data);
        }

        /// <summary>
        /// 加载通行证进度。文件不存在或字段不兼容时返回 null。
        /// </summary>
        public static BattlePassSaveData LoadBattlePassData()
        {
            return ReadJson<BattlePassSaveData>(BATTLEPASS_DATA_FILE);
        }

        #endregion

        #region 玩家等级存档

        /// <summary>
        /// 保存玩家等级数据。
        /// </summary>
        public static void SavePlayerLevelData(PlayerLevelSaveData data)
        {
            WriteJson(PLAYERLEVEL_DATA_FILE, data);
        }

        /// <summary>
        /// 加载玩家等级数据。
        /// </summary>
        public static PlayerLevelSaveData LoadPlayerLevelData()
        {
            return ReadJson<PlayerLevelSaveData>(PLAYERLEVEL_DATA_FILE);
        }

        #endregion

        #region 设置存档

        /// <summary>
        /// 保存设置。
        /// </summary>
        public static void SaveSettings(SettingsData data)
        {
            WriteJson(SETTINGS_FILE, data);
        }

        /// <summary>
        /// 加载设置。
        /// </summary>
        public static SettingsData LoadSettings()
        {
            return ReadJson<SettingsData>(SETTINGS_FILE) ?? new SettingsData();
        }

        #endregion

        #region 输入配置存档

        public static void SaveInputOverrides(PlayerInputActionOverrides data)
        {
            WriteJson(INPUT_OVERRIDES_FILE, data);
        }

        public static PlayerInputActionOverrides LoadInputOverrides()
        {
            return ReadJson<PlayerInputActionOverrides>(INPUT_OVERRIDES_FILE) ?? new PlayerInputActionOverrides();
        }

        #endregion

        #region 存档管理

        /// <summary>
        /// 删除所有存档。
        /// </summary>
        public static void DeleteAllSaves()
        {
            try
            {
                string dir = GetSaveDirectory();
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 删除存档失败: {e.Message}");
            }
        }

        /// <summary>
        /// 存档是否存在。
        /// </summary>
        public static bool SaveExists(string fileName)
        {
            return File.Exists(GetFilePath(fileName));
        }

        #endregion

        #region 工具方法

        private static void WriteJson<T>(string fileName, T data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetFilePath(fileName), json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 保存 {fileName} 失败: {e.Message}");
            }
        }

        private static T ReadJson<T>(string fileName) where T : class
        {
            try
            {
                string filePath = GetFilePath(fileName);
                if (!File.Exists(filePath)) return null;
                return JsonUtility.FromJson<T>(File.ReadAllText(filePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 加载 {fileName} 失败: {e.Message}");
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// 游戏统计存档。
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public float totalPlayTime;
        public int totalGamesPlayed;
        public int totalGamesWon;
        public int highestDifficultyReached;
        public string lastPlayDate;
    }

    /// <summary>
    /// 设置数据。
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        public float masterVolume = 1f;
        public float bgmVolume = 0.8f;
        public float sfxVolume = 1f;
        public int resolutionIndex;
        public bool fullscreen = true;
        public int qualityLevel = 2;
        public float mouseSensitivity = 1f;
        public bool invertY;
        public bool showFPS;
        public KeyCode pauseKey = KeyCode.Escape;
    }

    [Serializable]
    public class PlayerInputActionOverrides
    {
        public string pauseBindingOverridePath;
    }
}
