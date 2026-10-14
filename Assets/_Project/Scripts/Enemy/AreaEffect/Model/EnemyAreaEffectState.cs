using Interfaces;
using UnityEngine;

namespace EnemySystem.AreaEffect.Model
{
    /// <summary>范围效果精英的战术状态和组件可执行意图。</summary>
    internal enum EnemyAreaEffectState : byte
    {
        Flocking,
        Sensing,
        Approaching,
        Casting,
        Cooldown,
        Retreating
    }

    /// <summary>状态机交给范围效果组件的运动指令。</summary>
    internal enum EnemyAreaEffectIntent : byte
    {
        Flocking,
        Approach,
        Hold,
        Retreat
    }

    /// <summary>一次状态机决策使用的目标距离、玩家距离、生命和时间快照。</summary>
    internal readonly struct EnemyAreaEffectFrame
    {
        /// <summary>保存一次确定性的战术输入快照。</summary>
        /// <param name="deltaTime">本次推进的秒数。</param>
        /// <param name="hasTarget">当前是否有有效施法目标。</param>
        /// <param name="targetDistance">到施法目标的距离。</param>
        /// <param name="playerDistance">到玩家的距离；玩家不可用时传入正无穷。</param>
        /// <param name="healthPercent">范围精英当前生命比例。</param>
        internal EnemyAreaEffectFrame(
            float deltaTime,
            bool hasTarget,
            float targetDistance,
            float playerDistance,
            float healthPercent)
        {
            DeltaTime = Mathf.Max(0f, deltaTime);
            HasTarget = hasTarget;
            TargetDistance = Mathf.Max(0f, targetDistance);
            PlayerDistance = playerDistance;
            HealthPercent = Mathf.Clamp01(healthPercent);
        }

        internal float DeltaTime { get; }
        internal bool HasTarget { get; }
        internal float TargetDistance { get; }
        internal float PlayerDistance { get; }
        internal float HealthPercent { get; }
    }

    /// <summary>为范围精英集中计算安全距离、撤退条件和限速。</summary>
    internal static class EnemyAreaEffectTactics
    {
        /// <summary>按生命比例插值当前目标撤退距离。</summary>
        /// <param name="preferredDistance">健康时的首选距离。</param>
        /// <param name="maximumDistance">生命降低时的最大撤退距离。</param>
        /// <param name="healthPercent">当前生命比例。</param>
        /// <returns>本次撤退所需的目标安全距离。</returns>
        internal static float RetreatDistance(
            float preferredDistance,
            float maximumDistance,
            float healthPercent)
        {
            float preferred = Mathf.Max(0f, preferredDistance);
            float maximum = Mathf.Max(preferred, maximumDistance);
            return Mathf.Lerp(preferred, maximum, 1f - Mathf.Clamp01(healthPercent));
        }

        /// <summary>应用速度倍率后限制范围精英的定向移动速度。</summary>
        /// <param name="configuredSpeed">接近或撤退的基础速度。</param>
        /// <param name="speedMultiplier">Enemy 提供的生成及 Buff 合成倍率。</param>
        /// <param name="speedLimit">范围精英最大速度。</param>
        /// <returns>非负且不超过上限的移动速度。</returns>
        internal static float ClampSpeed(float configuredSpeed, float speedMultiplier, float speedLimit)
        {
            float effectiveSpeed = Mathf.Max(0f, configuredSpeed) * Mathf.Max(0f, speedMultiplier);
            return Mathf.Min(effectiveSpeed, Mathf.Max(0f, speedLimit));
        }

        /// <summary>判断近距离或低生命条件是否要求撤退。</summary>
        /// <param name="playerDistance">到玩家的距离。</param>
        /// <param name="minimumSafeDistance">玩家近距离撤退阈值。</param>
        /// <param name="healthPercent">当前生命比例。</param>
        /// <param name="lowHealthThreshold">低生命撤退阈值。</param>
        /// <returns>需要优先撤退时返回 true。</returns>
        internal static bool NeedsRetreat(
            float playerDistance,
            float minimumSafeDistance,
            float healthPercent,
            float preferredDistance,
            float maximumRetreatDistance,
            float lowHealthThreshold)
        {
            if (float.IsNaN(playerDistance) || float.IsInfinity(playerDistance))
            {
                return false;
            }

            if (playerDistance < Mathf.Max(0f, minimumSafeDistance))
            {
                return true;
            }

            return healthPercent <= Mathf.Clamp01(lowHealthThreshold) &&
                playerDistance < RetreatDistance(
                    preferredDistance,
                    maximumRetreatDistance,
                    healthPercent);
        }
    }

    /// <summary>按范围精英类型从空间查询候选中选择一个战术目标。</summary>
    internal static class EnemyAreaEffectTargetSelector
    {
        /// <summary>Slow 锁定玩家；治疗型精英仅选择受伤盟友。</summary>
        /// <param name="enemyType">当前精英类型。</param>
        /// <param name="owner">执行范围效果的敌人。</param>
        /// <param name="player">玩家目标。</param>
        /// <param name="candidates">空间查询得到的敌人候选。</param>
        /// <returns>满足当前战术的目标；无治疗目标时返回 null。</returns>
        internal static IDamageable Select(
            EnemyType enemyType,
            Enemy owner,
            IPlayerCombatTarget player,
            System.Collections.Generic.IReadOnlyList<IDamageable> candidates)
        {
            if (enemyType == EnemyType.Slow)
            {
                return player != null && player.IsAlive && player.Transform != null
                    ? player
                    : null;
            }

            if (owner == null || candidates == null ||
                (enemyType != EnemyType.Healer && enemyType != EnemyType.StrongHealer))
            {
                return null;
            }

            return enemyType == EnemyType.Healer
                ? SelectNearestInjured(owner, candidates)
                : SelectLowestHealth(owner, candidates);
        }

        /// <summary>选择距离治疗精英最近的受伤盟友。</summary>
        private static Enemy SelectNearestInjured(
            Enemy owner,
            System.Collections.Generic.IReadOnlyList<IDamageable> candidates)
        {
            Enemy selected = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < candidates.Count; index++)
            {
                Enemy candidate = GetInjuredAlly(owner, candidates[index]);
                if (candidate == null)
                {
                    continue;
                }

                float distance = (candidate.transform.position - owner.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    selected = candidate;
                }
            }

            return selected;
        }

        /// <summary>按生命比例选择最需要治疗的盟友，同值时优先较近者。</summary>
        private static Enemy SelectLowestHealth(
            Enemy owner,
            System.Collections.Generic.IReadOnlyList<IDamageable> candidates)
        {
            Enemy selected = null;
            float lowestHealthPercent = float.PositiveInfinity;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < candidates.Count; index++)
            {
                Enemy candidate = GetInjuredAlly(owner, candidates[index]);
                if (candidate == null)
                {
                    continue;
                }

                float healthPercent = candidate.HealthPercent;
                float distance = (candidate.transform.position - owner.transform.position).sqrMagnitude;
                if (healthPercent < lowestHealthPercent ||
                    (Mathf.Approximately(healthPercent, lowestHealthPercent) && distance < nearestDistance))
                {
                    lowestHealthPercent = healthPercent;
                    nearestDistance = distance;
                    selected = candidate;
                }
            }

            return selected;
        }

        /// <summary>过滤施法者、死亡、满血和生命配置无效的候选。</summary>
        private static Enemy GetInjuredAlly(Enemy owner, IDamageable target)
        {
            if (target is not Enemy candidate || candidate == owner || candidate.IsDead ||
                candidate.MaxHealth <= 0f || candidate.CurrentHealth <= 0f ||
                candidate.CurrentHealth >= candidate.MaxHealth)
            {
                return null;
            }

            return candidate;
        }
    }
}
