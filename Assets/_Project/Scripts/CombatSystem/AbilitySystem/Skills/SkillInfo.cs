using UnityEngine;

namespace CombatSystem
{
    /// <summary>一次技能激活时的来源、位置和方向快照。</summary>
    public struct SkillInfo
    {
        public int SourceId;
        public SkillDefinition Definition;
        public Vector3 Origin;
        public Vector3 Direction;
    }
}
