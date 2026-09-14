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
        private const string GameDataFileName = "gamedata.json";
        private const string ResourceDataFileName = "resourcedata.json";
        private const string BattlePassDataFileName = "battlepass.json";
        private const string PlayerLevelDataFileName = "playerlevel.json";
        private const string SettingsFileName = "settings.json";
        private const string InputOverridesFileName = "inputoverrides.json";

        /// <summary>
        /// 获取并创建存档目录。
        /// </summary>
        /// <returns>当前平台使用的存档目录路径。</returns>
        private static string GetSaveDirectoryPath()
        {
            string path = Application.persistentDataPath;
#if UNITY_EDITOR
            path = Path.Combine(Application.dataPath, "../SaveData");
#endif
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        /// <summary>
        /// 根据文件名构造完整存档路径。
        /// </summary>
        /// <param name="fileName">存档文件名。</param>
        /// <returns>完整存档文件路径。</returns>
        private static string GetSaveFilePath(string fileName)
        {
            return Path.Combine(GetSaveDirectoryPath(), fileName);
        }

        #region 游戏存档

        /// <summary>
        /// 保存游戏统计。
        /// </summary>
        public static void SaveGameData(GameSaveData data)
        {
            WriteJsonFile(GameDataFileName, data);
        }

        /// <summary>
        /// 加载游戏统计。
        /// </summary>
        public static GameSaveData LoadGameData()
        {
            return ReadJsonFile<GameSaveData>(GameDataFileName);
        }

        #endregion

        #region 资源存档

        /// <summary>
        /// 保存资源数据（积分）。
        /// </summary>
        public static void SaveResourceData(ResourceManager resourceManager)
        {
            WriteJsonFile(ResourceDataFileName, resourceManager.GetSaveData());
        }

        /// <summary>
        /// 加载资源数据（积分）。
        /// </summary>
        public static ResourceSaveData LoadResourceData()
        {
            return ReadJsonFile<ResourceSaveData>(ResourceDataFileName);
        }

        #endregion

        #region 通行证存档

        /// <summary>
        /// 保存通行证进度。
        /// </summary>
        public static void SaveBattlePassData(BattlePassSaveData data)
        {
            WriteJsonFile(BattlePassDataFileName, data);
        }

        /// <summary>
        /// 加载通行证进度。文件不存在或字段不兼容时返回 null。
        /// </summary>
        public static BattlePassSaveData LoadBattlePassData()
        {
            return ReadJsonFile<BattlePassSaveData>(BattlePassDataFileName);
        }

        #endregion

        #region 玩家等级存档

        /// <summary>
        /// 保存玩家等级数据。
        /// </summary>
        public static void SavePlayerLevelData(PlayerLevelSaveData data)
        {
            WriteJsonFile(PlayerLevelDataFileName, data);
        }

        /// <summary>
        /// 加载玩家等级数据。
        /// </summary>
        public static PlayerLevelSaveData LoadPlayerLevelData()
        {
            return ReadJsonFile<PlayerLevelSaveData>(PlayerLevelDataFileName);
        }

        #endregion

        #region 设置存档

        /// <summary>
        /// 保存设置。
        /// </summary>
        public static void SaveSettings(SettingsData data)
        {
            WriteJsonFile(SettingsFileName, data);
        }

        /// <summary>
        /// 加载设置。
        /// </summary>
        public static SettingsData LoadSettings()
        {
            return ReadJsonFile<SettingsData>(SettingsFileName) ?? new SettingsData();
        }

        #endregion

        #region 输入配置存档

        /// <summary>
        /// 保存玩家输入覆盖配置。
        /// </summary>
        /// <param name="data">输入覆盖存档数据。</param>
        public static void SaveInputOverrides(PlayerInputActionOverrides data)
        {
            WriteJsonFile(InputOverridesFileName, data);
        }

        /// <summary>
        /// 加载玩家输入覆盖配置；没有存档时返回默认对象。
        /// </summary>
        /// <returns>玩家输入覆盖存档数据。</returns>
        public static PlayerInputActionOverrides LoadInputOverrides()
        {
            return ReadJsonFile<PlayerInputActionOverrides>(InputOverridesFileName)
                ?? new PlayerInputActionOverrides();
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
                string saveDirectoryPath = GetSaveDirectoryPath();
                if (Directory.Exists(saveDirectoryPath))
                {
                    Directory.Delete(saveDirectoryPath, true);
                    Directory.CreateDirectory(saveDirectoryPath);
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
            return File.Exists(GetSaveFilePath(fileName));
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 将对象序列化为 JSON 文件。
        /// </summary>
        /// <param name="fileName">存档文件名。</param>
        /// <param name="data">要写入的存档对象。</param>
        private static void WriteJsonFile<T>(string fileName, T data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(GetSaveFilePath(fileName), json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 保存 {fileName} 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从 JSON 文件读取指定类型的存档对象。
        /// </summary>
        /// <param name="fileName">存档文件名。</param>
        /// <returns>读取到的对象；文件不存在或读取失败时返回 null。</returns>
        private static T ReadJsonFile<T>(string fileName) where T : class
        {
            try
            {
                string filePath = GetSaveFilePath(fileName);
                if (!File.Exists(filePath))
                {
                    return null;
                }

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
        /// <summary>暂停操作的输入覆盖路径。</summary>
        public string pauseBindingOverridePath;
    }
}
