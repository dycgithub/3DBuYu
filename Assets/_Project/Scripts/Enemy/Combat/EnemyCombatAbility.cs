using System;
using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using UnityEngine;
using VContainer;

namespace EnemySystem
{
    /// <summary>
    /// 为敌人提供直接释放技能和发射投射物的运行时入口。
    /// <para>敌人只组装来源、位置和目标；实际执行由通用战斗服务完成。</para>
    /// <para>本组件不检查能量、物品、冷却或战斗阶段，适合 AI、事件和 EditMode 测试直接调用。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyCombatAbility : MonoBehaviour
    {
        [Inject] private ISkillExecutionService _skillExecutionService;
        [Inject] private IProjectileLauncher _projectileLauncher;

        /// <summary>
        /// 让敌人直接执行一个技能。
        /// </summary>
        /// <param name="definition">要执行的技能定义。</param>
        /// <param name="targets">AI 已经选择的目标快照。</param>
        /// <returns>技能执行结果。</returns>
        public SkillExecutionResult ExecuteSkill(
            SkillDefinitionSO definition,
            IReadOnlyList<IDamageable> targets)
        {
            if (_skillExecutionService == null)
            {
                return SkillExecutionResult.Failure(
                    SkillExecutionFailureReason.ActionFailed);
            }

            var request = new SkillExecutionRequest(
                definition,
                GetInstanceID(),
                transform.position,
                targets);
            return _skillExecutionService.Execute(in request);
        }

        /// <summary>
        /// 让敌人直接对单个目标执行技能。
        /// </summary>
        /// <param name="definition">要执行的技能定义。</param>
        /// <param name="target">技能目标。</param>
        /// <returns>技能执行结果。</returns>
        public SkillExecutionResult ExecuteSkill(
            SkillDefinitionSO definition,
            IDamageable target)
        {
            IReadOnlyList<IDamageable> targets = target == null
                ? Array.Empty<IDamageable>()
                : new[] { target };
            return ExecuteSkill(definition, targets);
        }

        /// <summary>
        /// 让敌人直接发射一个或多个投射物。
        /// </summary>
        /// <param name="projectileProfile">投射物静态配置。</param>
        /// <param name="damageAmount">每个投射物的伤害量。</param>
        /// <param name="launchDirection">发射方向。</param>
        /// <param name="projectileCount">一次生成的投射物数量。</param>
        /// <param name="penetrationCount">每个投射物的穿透次数。</param>
        /// <returns>投射物请求被接受时返回 <c>true</c>。</returns>
        public bool LaunchProjectile(
            ProjectileProfileSO projectileProfile,
            float damageAmount,
            Vector3 launchDirection,
            int projectileCount = 1,
            int penetrationCount = 0,
            ProjectileTargetMode targetMode = ProjectileTargetMode.Enemy)
        {
            if (_projectileLauncher == null)
            {
                return false;
            }

            var request = new ProjectileLaunchRequest(
                GetInstanceID(),
                projectileProfile,
                transform.position,
                launchDirection,
                damageAmount,
                projectileCount,
                penetrationCount,
                false,
                targetMode);
            return _projectileLauncher.Launch(in request);
        }
    }
}
