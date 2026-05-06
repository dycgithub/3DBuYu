using System;
using System.IO;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 存档系统
    /// 管理游戏数据的保存和加载
    /// </summary>
    public static class SaveSystem
    {
        // 存档文件名
        private const string GAME_DATA_FILE = "gamedata.json";
        private const string RESOURCE_DATA_FILE = "resourcedata.json";
        private const string SETTINGS_FILE = "settings.json";

        /// <summary>
        /// 获取存档目录
        /// </summary>
        private static string GetSaveDirectory()
        {
            // 使用持久化数据目录
            string path = Application.persistentDataPath;

#if UNITY_EDITOR
            // 编辑器模式下使用项目目录
            path = Path.Combine(Application.dataPath, "../SaveData");
#endif

            // 确保目录存在
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        /// <summary>
        /// 获取完整文件路径
        /// </summary>
        private static string GetFilePath(string fileName)
        {
            return Path.Combine(GetSaveDirectory(), fileName);
        }

        #region 游戏数据存档

        /// <summary>
        /// 保存游戏数据
        /// </summary>
        public static void SaveGameData(GameSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                string filePath = GetFilePath(GAME_DATA_FILE);
                File.WriteAllText(filePath, json);

                Debug.Log($"游戏数据已保存: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"保存游戏数据失败: {e.Message}");
            }
        }

        /// <summary>
        /// 加载游戏数据
        /// </summary>
        public static GameSaveData LoadGameData()
        {
            try
            {
                string filePath = GetFilePath(GAME_DATA_FILE);

                if (!File.Exists(filePath))
                {
                    Debug.Log("游戏存档不存在，创建新存档");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

                Debug.Log($"游戏数据已加载: {filePath}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"加载游戏数据失败: {e.Message}");
                return null;
            }
        }

        #endregion

        #region 资源数据存档

        /// <summary>
        /// 保存资源数据
        /// </summary>
        public static void SaveResourceData(ResourceManager resourceManager)
        {
            try
            {
                var data = resourceManager.GetSaveData();
                string json = JsonUtility.ToJson(data, true);
                string filePath = GetFilePath(RESOURCE_DATA_FILE);
                File.WriteAllText(filePath, json);

                Debug.Log($"资源数据已保存: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"保存资源数据失败: {e.Message}");
            }
        }

        /// <summary>
        /// 加载资源数据
        /// </summary>
        public static ResourceSaveData LoadResourceData()
        {
            try
            {
                string filePath = GetFilePath(RESOURCE_DATA_FILE);

                if (!File.Exists(filePath))
                {
                    Debug.Log("资源存档不存在");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                ResourceSaveData data = JsonUtility.FromJson<ResourceSaveData>(json);

                Debug.Log($"资源数据已加载: {filePath}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"加载资源数据失败: {e.Message}");
                return null;
            }
        }

        #endregion

        #region 设置存档

        /// <summary>
        /// 保存设置
        /// </summary>
        public static void SaveSettings(SettingsData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                string filePath = GetFilePath(SETTINGS_FILE);
                File.WriteAllText(filePath, json);

                Debug.Log($"设置已保存: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"保存设置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        public static SettingsData LoadSettings()
        {
            try
            {
                string filePath = GetFilePath(SETTINGS_FILE);

                if (!File.Exists(filePath))
                {
                    // 返回默认设置
                    return new SettingsData();
                }

                string json = File.ReadAllText(filePath);
                SettingsData data = JsonUtility.FromJson<SettingsData>(json);

                Debug.Log($"设置已加载: {filePath}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"加载设置失败: {e.Message}");
                return new SettingsData();
            }
        }

        #endregion

        #region 存档管理

        /// <summary>
        /// 删除所有存档
        /// </summary>
        public static void DeleteAllSaves()
        {
            try
            {
                string saveDir = GetSaveDirectory();

                if (Directory.Exists(saveDir))
                {
                    Directory.Delete(saveDir, true);
                    Directory.CreateDirectory(saveDir);
                }

                Debug.Log("所有存档已删除");
            }
            catch (Exception e)
            {
                Debug.LogError($"删除存档失败: {e.Message}");
            }
        }

        /// <summary>
        /// 存档是否存在
        /// </summary>
        public static bool SaveExists(string fileName)
        {
            return File.Exists(GetFilePath(fileName));
        }

        /// <summary>
        /// 获取存档信息
        /// </summary>
        public static SaveFileInfo GetSaveInfo()
        {
            try
            {
                string filePath = GetFilePath(GAME_DATA_FILE);

                if (!File.Exists(filePath))
                {
                    return null;
                }

                FileInfo fileInfo = new FileInfo(filePath);

                return new SaveFileInfo
                {
                    fileName = GAME_DATA_FILE,
                    fileSize = fileInfo.Length,
                    lastModified = fileInfo.LastWriteTime.ToString()
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"获取存档信息失败: {e.Message}");
                return null;
            }
        }

        #endregion

        #region 数据迁移/备份

        /// <summary>
        /// 备份存档
        /// </summary>
        public static bool BackupSaves(string backupName)
        {
            try
            {
                string saveDir = GetSaveDirectory();
                string backupDir = Path.Combine(saveDir, $"Backup_{backupName}_{DateTime.Now:yyyyMMdd_HHmmss}");

                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // 复制所有存档文件
                foreach (string file in Directory.GetFiles(saveDir, "*.json"))
                {
                    string fileName = Path.GetFileName(file);
                    File.Copy(file, Path.Combine(backupDir, fileName), true);
                }

                Debug.Log($"存档已备份到: {backupDir}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"备份存档失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出存档到指定路径
        /// </summary>
        public static bool ExportSaves(string exportPath)
        {
            try
            {
                string saveDir = GetSaveDirectory();

                // 创建导出目录
                if (!Directory.Exists(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }

                // 复制所有存档文件
                foreach (string file in Directory.GetFiles(saveDir, "*.json"))
                {
                    string fileName = Path.GetFileName(file);
                    File.Copy(file, Path.Combine(exportPath, fileName), true);
                }

                Debug.Log($"存档已导出到: {exportPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"导出存档失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从指定路径导入存档
        /// </summary>
        public static bool ImportSaves(string importPath)
        {
            try
            {
                if (!Directory.Exists(importPath))
                {
                    Debug.LogError("导入路径不存在");
                    return false;
                }

                string saveDir = GetSaveDirectory();

                // 复制所有存档文件
                foreach (string file in Directory.GetFiles(importPath, "*.json"))
                {
                    string fileName = Path.GetFileName(file);
                    File.Copy(file, Path.Combine(saveDir, fileName), true);
                }

                Debug.Log($"存档已从 {importPath} 导入");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"导入存档失败: {e.Message}");
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// 设置数据
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        // 音频设置
        public float masterVolume = 1f;
        public float bgmVolume = 0.8f;
        public float sfxVolume = 1f;

        // 图形设置
        public int resolutionIndex = 0;
        public bool fullscreen = true;
        public int qualityLevel = 2;
        public float renderScale = 1f;

        // 游戏设置
        public float mouseSensitivity = 1f;
        public bool invertY = false;
        public bool showDamageNumbers = true;
        public bool showFPS = false;

        // 控制设置
        public KeyCode pauseKey = KeyCode.Escape;
        public KeyCode upgradeKey = KeyCode.U;
    }

    /// <summary>
    /// 存档文件信息
    /// </summary>
    [Serializable]
    public class SaveFileInfo
    {
        public string fileName;
        public long fileSize;
        public string lastModified;
    }
}
