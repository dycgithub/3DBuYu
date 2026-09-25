namespace CombatSystem
{
    /// <summary>清理中央主动技能留下的敌人永久效果。</summary>
    public interface ICentralSkillEffectResetService
    {
        /// <summary>移除当前中央主动技能提交的所有永久敌人效果。</summary>
        void ClearPermanentEffects();
    }
}
