using System;
using System.Collections.Generic;
using System.IO;
using CombatSystem;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 存档系统。
    /// 管理资源、设置和输入覆盖的持久化；Windows 发布版的存档随游戏目录移动。
    /// </summary>
    public static class SaveSystem
    {
        private const string ResourceDataFileName = "resourcedata.json";
        private const string SettingsFileName = "settings.json";
        private const string InputOverridesFileName = "inputoverrides.json";

        /// <summary>
        /// 获取并创建当前平台的存档目录；目录不可写时不回退到其他位置。
        /// </summary>
        /// <returns>当前平台使用的存档目录路径。</returns>
        private static string GetSaveDirectoryPath()
        {
            string path = ResolveSaveDirectory(
                Application.dataPath, Application.persistentDataPath, Application.platform);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        /// <summary>编辑器与 Windows 使用数据目录旁的 SaveData，其他平台使用持久化目录。</summary>
        /// <param name="dataPath">Unity 资产或发布版数据目录的绝对路径。</param>
        /// <param name="persistentPath">平台默认的持久化目录。</param>
        /// <param name="platform">当前运行平台。</param>
        /// <returns>不依赖启动工作目录的存档路径。</returns>
        private static string ResolveSaveDirectory(
            string dataPath, string persistentPath, RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    return Path.Combine(Path.GetDirectoryName(dataPath), "SaveData");
                default:
                    return persistentPath;
            }
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

        #region 设置存档

        /// <summary>
        /// 保存设置。
        /// </summary>
        public static void SaveSettings(SettingsData data)
        {
            WriteJsonFile(SettingsFileName, data);
        }

        /// <summary>
        /// 加载当前格式的设置；文件不存在或读取失败时返回默认设置。
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

        /// <summary>先写完同目录临时文件，再将其原子提交到目标路径。</summary>
        /// <param name="filePath">目标 JSON 文件的完整路径。</param>
        /// <param name="json">待写入的 JSON 文本。</param>
        /// <param name="writeTemporaryFile">写入临时文件的操作。</param>
        /// <param name="commitTemporaryFile">提交临时文件的操作。</param>
        private static void WriteJsonAtomically(
            string filePath,
            string json,
            Action<string, string> writeTemporaryFile,
            Action<string, string> commitTemporaryFile)
        {
            string temporaryFilePath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                writeTemporaryFile(temporaryFilePath, json);
                commitTemporaryFile(temporaryFilePath, filePath);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryFilePath))
                    {
                        File.Delete(temporaryFilePath);
                    }
                }
                catch (Exception)
                {
                    // 清理异常不能覆盖写入或提交时的原始异常。
                }
            }
        }

        /// <summary>根据目标文件是否存在，将临时文件替换或移动为正式存档。</summary>
        /// <param name="temporaryFilePath">已完整写入并关闭的临时文件路径。</param>
        /// <param name="destinationFilePath">正式存档的完整路径。</param>
        private static void CommitTemporaryFile(string temporaryFilePath, string destinationFilePath)
        {
            if (File.Exists(destinationFilePath))
            {
                File.Replace(temporaryFilePath, destinationFilePath, null);
                return;
            }

            File.Move(temporaryFilePath, destinationFilePath);
        }

        /// <summary>
        /// 将对象序列化为 JSON 并原子写入存档文件。
        /// </summary>
        /// <param name="fileName">存档文件名。</param>
        /// <param name="data">要写入的存档对象。</param>
        private static void WriteJsonFile<T>(string fileName, T data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                WriteJsonAtomically(GetSaveFilePath(fileName), json, File.WriteAllText, CommitTemporaryFile);
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
    /// 跨启动保存音频、显示、输入和语言设置；缺少语言字段时默认英文。
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        /// <summary>玩家选择的语言代码；首次启动不跟随系统语言。</summary>
        public string languageCode = "en";

        public float masterVolume = 1f;
        public float bgmVolume = 0.8f;
        public float sfxVolume = 1f;
        public int resolutionIndex;
        public bool fullscreen = true;
        public bool vSync = true;
        public bool frameRateLimitEnabled;
        public int frameRateLimit = 60;
        public int qualityLevel = 2;
        public float mouseSensitivity = 1f;
        public bool invertY;
        public bool showFPS;
        public bool showCombatRangeVisuals = true;
        /// <summary>用户选择的发射器攻击输入模式。</summary>
        public TransmitterAttackMode attackMode = TransmitterAttackMode.Automatic;
    }

    [Serializable]
    public class PlayerInputActionOverrides
    {
        /// <summary>暂停操作的输入覆盖路径。</summary>
        public string pauseBindingOverridePath;

        /// <summary>按固定输入动作顺序保存的主绑定覆盖路径。</summary>
        public List<string> bindingOverridePaths = new();
    }
}
