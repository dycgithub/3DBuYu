namespace CombatSystem
{
    /// <summary>把物品实例放入炮台或端口装备快照。</summary>
    public sealed class GrantResolver
    {
        public bool Equip(CombatLoadout loadout, ItemInstance item, int portIndex = -1)
        {
            if (loadout == null || item?.CombatGrant == null)
                return false;

            if (item.CombatGrant.Scope == EquipmentScope.Turret)
            {
                loadout.SetTurretItem(item);
                return true;
            }

            if (portIndex < 0)
                return false;

            loadout.SetPortItem(portIndex, item);
            return true;
        }

        public void UnEquipPort(CombatLoadout loadout, int portIndex)
        {
            loadout?.SetPortItem(portIndex, null);
        }

        public void UnEquipTurret(CombatLoadout loadout)
        {
            loadout?.SetTurretItem(null);
        }
    }
}
