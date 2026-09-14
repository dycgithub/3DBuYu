using System.Reflection;
using EnemySystem;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证对象池敌人在激活前初始化后，Unity 激活生命周期不会清空本次生成的生命值。
    /// </summary>
    public sealed class EnemyActivationLifecycleTests
    {
        [Test]
        public void InitializedEnemy_ActivationPreservesSpawnHealth()
        {
            GameObject enemyObject = new("EnemyActivationLifecycleTest");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 10f;
                SetField(enemy, "enemyAttributes", attributes);

                enemy.InitializeForSpawn(2f, 1f);
                Assert.That(enemy.CurrentHealth, Is.EqualTo(20f));

                enemyObject.SetActive(true);

                Assert.That(enemy.MaxHealth, Is.EqualTo(20f));
                Assert.That(enemy.CurrentHealth, Is.EqualTo(20f));
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
