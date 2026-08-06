using System.Collections.Generic;
using TurretSystem;
using UnityEngine;

namespace ItemSystem.Functions
{
    /// <summary>
    /// 主动技能管理器 — 命令模式 Invoker。
    /// 战斗开始(Rebuild)时扫描炮塔装备格(PlayerLoadout.TurretInventory)中的 Skill 物品,
    /// 按 ItemConfig.skillKind 实例化具体技能命令,并用 CooldownSkillDecorator 包装冷却;
    /// 玩家/UI/输入通过 Execute 触发命令,由 IItemActivationContext(Receiver)执行效果。
    /// 预留触发接口:IsReady/GetCooldownRemaining/GetName/GetIcon 供技能条 UI 绑定。
    /// </summary>
    public class SkillManager
    {
        public event System.Action OnSkillsChanged;

        public int Count => _skills.Count;

        private readonly PlayerLoadout _loadout;
        private readonly IItemActivationContext _context;
        private readonly List<SkillEntry> _skills = new();

        private class SkillEntry
        {
            public ISkill Command;
            public CooldownSkillDecorator Cooldown;
            public ItemConfig Config;
        }

        public SkillManager(PlayerLoadout loadout, IItemActivationContext context)
        {
            _loadout = loadout;
            _context = context;
        }

        /// <summary>从炮塔装备格重建技能列表(战斗开始/装备变化时调用)。</summary>
        public void Rebuild()
        {
            _skills.Clear();

            if (_loadout?.TurretInventory != null)
            {
                foreach (var placed in _loadout.TurretInventory.GetAllItems())
                {
                    var config = _loadout.TurretInventory.GetItemConfig(placed.instanceId);
                    if (config == null || config.ItemType != ItemType.Skill) continue;

                    var command = CreateCommand(config);
                    if (command == null) continue;

                    _skills.Add(new SkillEntry
                    {
                        Command = command,
                        Cooldown = new CooldownSkillDecorator(command, config.cooldownSeconds),
                        Config = config
                    });
                }
            }

            OnSkillsChanged?.Invoke();
        }

        /// <summary>释放指定槽位的技能(冷却中就绪时触发,否则返回 false)。</summary>
        public bool Execute(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _skills.Count) return false;
            var entry = _skills[slotIndex];
            if (!entry.Cooldown.IsReady) return false;
            entry.Cooldown.Activate(_context);
            return true;
        }

        public bool IsReady(int slotIndex)
            => slotIndex >= 0 && slotIndex < _skills.Count && _skills[slotIndex].Cooldown.IsReady;

        /// <summary>剩余冷却秒数(&lt;= 0 表示就绪)。</summary>
        public float GetCooldownRemaining(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _skills.Count) return 0f;
            return Mathf.Max(0f, _skills[slotIndex].Cooldown.NextAvailableTime - Time.time);
        }

        public string GetName(int slotIndex)
            => slotIndex >= 0 && slotIndex < _skills.Count ? _skills[slotIndex].Config.displayName : string.Empty;

        public Sprite GetIcon(int slotIndex)
            => slotIndex >= 0 && slotIndex < _skills.Count
                ? _Project.UI.Inventory.ItemVisualHelper.GetIcon(_skills[slotIndex].Config.itemId)
                : null;

        private static ISkill CreateCommand(ItemConfig config)
        {
            return config.skillKind switch
            {
                SkillKind.KillAllEnemies => new KillAllEnemiesSkill(),
                SkillKind.UnlockAllPorts => new UnlockAllPortsSkill(),
                _ => null
            };
        }
    }
}
