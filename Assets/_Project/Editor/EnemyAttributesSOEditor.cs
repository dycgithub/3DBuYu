using System;
using EnemySystem;
using UnityEditor;
using UnityEngine;

namespace EnemySystem.Editor
{
    /// <summary>按敌人类型显示基础、行为、阶段及范围精英战术配置。</summary>
    [CustomEditor(typeof(EnemyAttributesSO))]
    [CanEditMultipleObjects]
    public sealed class EnemyAttributesSOEditor : UnityEditor.Editor
    {
        private static readonly string[] CommonProperties =
        {
            "enemyId",
            "baseHealth",
            "enemyType",
            "rewardMode",
            "energyReward",
            "pointsReward",
            "hitEffect",
            "deathEffect",
            "visualConfig"
        };

        private static readonly string[] BossProperties =
        {
            "bossSecondPhaseHealthPercent",
            "bossPhaseTwoSpeedMultiplier",
            "bossThreatLevel"
        };

        private static readonly string[] AssaultProperties =
        {
            "assaultPerceptionRadius",
            "assaultTurnSpeed",
            "assaultTurnTolerance",
            "assaultChargeDuration",
            "assaultDashSpeed",
            "assaultDashDistance",
            "assaultPhaseTwoDashCount",
            "assaultAttackRecoveryDuration"
        };

        private static readonly string[] GrowthProperties =
        {
            "goapReplanInterval",
            "growthTargetSearchRadius",
            "growthAbsorbRange",
            "growthPlayerEngageRange"
        };

        private static readonly string[] ShooterProperties =
        {
            "shooterAttackRadius",
            "shooterMinimumRange",
            "shooterFireInterval",
            "shooterPhaseTwoFireInterval",
            "shooterDamage",
            "shooterProjectileProfile"
        };

        private static readonly string[] MotherProperties =
        {
            "childPrefab",
            "childSpawnInterval",
            "childMaximumAlive",
            "childSpawnRadius",
            "childHealthMultiplier",
            "childSpeedMultiplier",
            "childPhaseOneTargetAlive",
            "childPhaseTwoTargetAlive",
            "childPhaseTwoSpawnInterval",
            "motherPlayerEngageRange",
            "goapReplanInterval"
        };

        private static readonly string[] AreaEffectProperties =
        {
            "areaEffectRadius",
            "areaEffectTickInterval",
            "areaSlowMultiplier",
            "areaEffectType",
            "areaSlowBuff",
            "areaHealBuff",
            "areaSensingRadius",
            "areaPreferredDistance",
            "areaMinimumSafeDistance",
            "areaMaximumRetreatDistance",
            "areaLowHealthRetreatThreshold",
            "areaApproachSpeed",
            "areaRetreatSpeed",
            "areaTurnSpeed",
            "areaSensingInterval",
            "areaCastWindowSeconds",
            "areaCastCooldownSeconds"
        };

        /// <summary>绘制通用配置和当前类型实际使用的机制字段。</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("基础与奖励", EditorStyles.boldLabel);
            DrawProperties(CommonProperties);

            SerializedProperty enemyType = serializedObject.FindProperty("enemyType");
            if (enemyType.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox(
                    "所选资产包含不同敌人类型；类型专属字段暂不显示。",
                    MessageType.Info);
            }
            else
            {
                DrawTypeProperties((EnemyType)enemyType.intValue);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>取得指定类型在 Inspector 中显示的机制字段。</summary>
        /// <param name="enemyType">敌人机制类型。</param>
        /// <returns>该类型使用的序列化字段名。</returns>
        internal static string[] GetPropertiesFor(EnemyType enemyType)
        {
            return enemyType switch
            {
                EnemyType.Fast => new[] { "dodgeChance", "dodgeDistance" },
                EnemyType.Tank => new[] { "shieldValue", "shieldRegenRate", "shieldRegenDelay" },
                EnemyType.Assault => Combine(AssaultProperties, BossProperties),
                EnemyType.Growth => Combine(GrowthProperties, BossProperties),
                EnemyType.Reflect => new[] { "reflectWindowSeconds" },
                EnemyType.Exploder => new[] { "explosionRadius", "explosionDamage" },
                EnemyType.Shooter => Combine(ShooterProperties, BossProperties),
                EnemyType.Slow or EnemyType.Healer or EnemyType.StrongHealer => AreaEffectProperties,
                EnemyType.Mother => Combine(MotherProperties, BossProperties),
                _ => Array.Empty<string>()
            };
        }

        /// <summary>绘制敌人类型标题和其专属字段。</summary>
        private void DrawTypeProperties(EnemyType enemyType)
        {
            string[] properties = GetPropertiesFor(enemyType);
            if (properties.Length == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{GetTitle(enemyType)} 配置", EditorStyles.boldLabel);
            DrawProperties(properties);
        }

        /// <summary>通过 SerializedProperty 绘制字段并保留 Unity 原生约束。</summary>
        private void DrawProperties(string[] propertyNames)
        {
            for (int index = 0; index < propertyNames.Length; index++)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyNames[index]);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
        }

        /// <summary>合并同一类型所需的机制字段和 Boss 阶段字段。</summary>
        private static string[] Combine(string[] properties, string[] phaseProperties)
        {
            string[] combined = new string[properties.Length + phaseProperties.Length];
            Array.Copy(properties, combined, properties.Length);
            Array.Copy(phaseProperties, 0, combined, properties.Length, phaseProperties.Length);
            return combined;
        }

        /// <summary>取得 Inspector 中使用的简短类型标题。</summary>
        private static string GetTitle(EnemyType enemyType)
        {
            return enemyType switch
            {
                EnemyType.Assault => "冲锋 Boss / BT",
                EnemyType.Growth => "吞噬 Boss / GOAP",
                EnemyType.Shooter => "远程 Boss / BT",
                EnemyType.Mother => "母体 Boss / GOAP",
                EnemyType.Exploder => "爆炸",
                EnemyType.StrongHealer => "强化治疗",
                _ => enemyType.ToString()
            };
        }
    }
}
