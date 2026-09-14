using System.Reflection;
using EnemySystem;
using EnemySystem.Components;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证对象池敌人在激活前完成生成初始化后，生命组件的启用回调不会覆盖生命状态。
    /// </summary>
    public sealed class EnemyHealthActivationContractTests
    {
        [Test]
        public void HealthActivation_DoesNotClearInitializedSpawnState()
        {
            GameObject enemyObject = new("EnemyHealthActivationContractTest");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 20f;
                SetField(enemy, "enemyAttributes", attributes);
                enemy.InitializeForSpawn(1.5f, 1f);

                EnemyHealthComponent health = enemyObject.GetComponent<EnemyHealthComponent>();
                MethodInfo onEnable = typeof(EnemyHealthComponent).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(health, Is.Not.Null);
                Assert.That(onEnable, Is.Not.Null);
                onEnable.Invoke(health, null);

                Assert.That(enemy.MaxHealth, Is.EqualTo(30f));
                Assert.That(enemy.CurrentHealth, Is.EqualTo(30f));
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
