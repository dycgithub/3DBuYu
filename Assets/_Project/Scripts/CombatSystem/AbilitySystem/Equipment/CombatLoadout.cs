using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>
    /// 战斗运行时装备快照。UI 只需要通过 GrantResolver 修改它。
    /// </summary>
    public sealed class CombatLoadout : IAttackModifierSource
    {
        private readonly Dictionary<int, ItemInstance> _portItems = new();
        private ItemInstance _turretItem;
        private int _runId;

        public ItemInstance TurretItem => _turretItem;
        public int RunId => _runId;

        /// <summary>开始新的一局，清除上一局的临时装备。</summary>
        public void BeginRun()
        {
            Clear();
            _runId++;
        }

        /// <summary>清除本局装备，永久库存不在这里处理。</summary>
        public void Clear()
        {
            _turretItem = null;
            _portItems.Clear();
        }

        public void SetTurretItem(ItemInstance item)
        {
            _turretItem = item;
        }

        public void SetPortItem(int portIndex, ItemInstance item)
        {
            if (portIndex < 0)
                return;

            if (item == null)
                _portItems.Remove(portIndex);
            else
                _portItems[portIndex] = item;
        }

        public ItemInstance GetPortItem(int portIndex)
        {
            _portItems.TryGetValue(portIndex, out ItemInstance item);
            return item;
        }

        public void CollectAttackModifiers(
            int portIndex,
            List<IAttackModifier> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            AddGrantModifiers(_turretItem?.CombatGrant, destination);

            if (_portItems.TryGetValue(portIndex, out ItemInstance portItem))
                AddGrantModifiers(portItem.CombatGrant, destination);
        }

        public void CollectSkills(List<SkillDefinition> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            AddGrantSkills(_turretItem?.CombatGrant, destination);

            foreach (ItemInstance item in _portItems.Values)
                AddGrantSkills(item?.CombatGrant, destination);
        }

        public void CollectEquipBuffs(List<BuffConfig> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            AddGrantBuffs(_turretItem?.CombatGrant, destination);

            foreach (ItemInstance item in _portItems.Values)
                AddGrantBuffs(item?.CombatGrant, destination);
        }

        private static void AddGrantModifiers(
            CombatItemGrant grant,
            List<IAttackModifier> destination)
        {
            if (grant?.AttackModifiers == null)
                return;

            for (int i = 0; i < grant.AttackModifiers.Length; i++)
            {
                IAttackModifier modifier = grant.AttackModifiers[i];
                if (modifier != null)
                    destination.Add(modifier);
            }
        }

        private static void AddGrantSkills(
            CombatItemGrant grant,
            List<SkillDefinition> destination)
        {
            if (grant?.SkillGrants == null)
                return;

            for (int i = 0; i < grant.SkillGrants.Length; i++)
            {
                SkillDefinition skill = grant.SkillGrants[i];
                if (skill != null)
                    destination.Add(skill);
            }
        }

        private static void AddGrantBuffs(
            CombatItemGrant grant,
            List<BuffConfig> destination)
        {
            if (grant?.EquipBuffs == null)
                return;

            for (int i = 0; i < grant.EquipBuffs.Length; i++)
            {
                BuffConfig buff = grant.EquipBuffs[i];
                if (buff != null)
                    destination.Add(buff);
            }
        }
    }
}
