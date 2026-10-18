using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace _Project.Tests.PlayMode
{
    /// <summary>为音频 PlayMode 测试创建临时 BGM 目录。</summary>
    internal static class AudioManagerBgmTestCatalog
    {
        /// <summary>创建包含指定曲目及音量的临时目录资产。</summary>
        /// <param name="tracks">由 BGM 类型名、片段及曲目音量构成的配置。</param>
        /// <returns>配置完成的目录 ScriptableObject。</returns>
        public static UnityEngine.Object Create(params (string id, AudioClip clip, float volume)[] tracks)
        {
            Type catalogType = Type.GetType("GameSystem.MusicCatalogSO, Assembly-CSharp", true);
            UnityEngine.Object catalog = ScriptableObject.CreateInstance(catalogType);
            Type entryType = Type.GetType("GameSystem.MusicCatalogEntry, Assembly-CSharp", true);
            Type bgmType = Type.GetType("GameSystem.BGMType, Assembly-CSharp", true);
            IList entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));

            foreach ((string id, AudioClip clip, float volume) track in tracks)
            {
                object entry = Activator.CreateInstance(entryType);
                entryType.GetField("Id").SetValue(entry, Enum.Parse(bgmType, track.id));
                entryType.GetField("Clip").SetValue(entry, track.clip);
                entryType.GetField("Volume").SetValue(entry, track.volume);
                entries.Add(entry);
            }

            FieldInfo entriesField = catalogType.GetField(
                "_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            entriesField.SetValue(catalog, entries);
            return catalog;
        }
    }
}
