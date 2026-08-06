using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    public class ItemConfigRegistry
    {
        private Dictionary<string, ItemConfig> _byId = new();

        public void Register(ItemConfig config)
        {
            if (config == null) return;
            _byId[config.itemId] = config;
        }

        public bool TryGet(string id, out ItemConfig config)
        {
            return _byId.TryGetValue(id, out config);
        }

        public ItemConfig Get(string id)
        {
            _byId.TryGetValue(id, out var config);
            return config;
        }

        public void Clear()
        {
            _byId.Clear();
        }

        public IReadOnlyList<ItemConfig> GetAll()
        {
            return new List<ItemConfig>(_byId.Values);
        }

#if UNITY_EDITOR
        public static void ValidateDuplicateIds()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemConfig");
            var seen = new Dictionary<string, ItemConfig>();
            bool hasError = false;

            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemConfig>(path);
                if (config == null) continue;

                if (string.IsNullOrWhiteSpace(config.itemId))
                {
                    Debug.LogError($"[ItemConfigRegistry] {path}: itemId 为空。", config);
                    hasError = true;
                    continue;
                }

                if (seen.TryGetValue(config.itemId, out var existing))
                {
                    Debug.LogError($"[ItemConfigRegistry] 重复 itemId '{config.itemId}': {path} 与 {UnityEditor.AssetDatabase.GetAssetPath(existing)} 冲突。", config);
                    hasError = true;
                }
                else
                {
                    seen[config.itemId] = config;
                }
            }

            if (!hasError)
                Debug.Log($"[ItemConfigRegistry] 验证通过：共 {guids.Length} 个 ItemConfig，无重复 itemId。");
        }
#endif
    }
}
