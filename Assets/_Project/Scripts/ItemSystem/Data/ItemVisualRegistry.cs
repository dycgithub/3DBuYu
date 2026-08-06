using System.Collections.Generic;

namespace ItemSystem
{
    /// <summary>
    /// 物品表现配置注册表(itemId → ItemVisualConfig)。
    /// 由 ProjectLifetimeScope 以资产数组构建并注册,UI 层通过容器解析。
    /// </summary>
    public class ItemVisualRegistry
    {
        private readonly Dictionary<string, ItemVisualConfig> _byId = new();

        public ItemVisualRegistry(IEnumerable<ItemVisualConfig> configs)
        {
            if (configs == null) return;
            foreach (var config in configs)
            {
                if (config != null && !string.IsNullOrEmpty(config.itemId))
                    _byId[config.itemId] = config;
            }
        }

        public bool TryGet(string itemId, out ItemVisualConfig visual)
            => _byId.TryGetValue(itemId, out visual);

        public ItemVisualConfig Get(string itemId)
        {
            _byId.TryGetValue(itemId, out var visual);
            return visual;
        }
    }
}
