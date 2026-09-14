using System.Reflection;
using CombatSystem;
using EnemySystem;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人在生成初始化前不会把伤害误判为致死伤害。
    /// </summary>
    public sealed class EnemyHealthInitializationGuardTests
    {
        [Test]
        public void UninitializedEnemy_RejectsDamageWithoutEnteringDeath()
        {
            GameObject enemyObject = new("EnemyHealthInitializationGuardTest");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 20f;
                SetField(enemy, "enemyAttributes", attributes);

                DamageResult result = enemy.ReceiveDamageRequest(new DamageRequest
                {
                    DamageAmount = 5f,
                    DamageType = DamageType.Physical
                });

                Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Invalid));
                Assert.That(enemy.CurrentHealth, Is.EqualTo(0f));
                Assert.That(enemy.IsDead, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
                Object.DestroyImmediate(attributes);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
