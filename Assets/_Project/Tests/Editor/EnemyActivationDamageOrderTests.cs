using System.Reflection;
using CombatSystem;
using EnemySystem;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定敌人在激活回调期间已经具备本次生成生命值，避免首帧前的战斗请求命中零生命状态。
    /// </summary>
    public sealed class EnemyActivationDamageOrderTests
    {
        [Test]
        public void ActivationCallback_CanDamageInitializedEnemyBeforeStart()
        {
            GameObject enemyObject = new("EnemyActivationDamageOrderTest");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 20f;
                SetField(enemy, "enemyAttributes", attributes);
                enemy.InitializeForSpawn(1f, 1f);

                EnemyActivationDamageProbe probe = enemyObject.AddComponent<EnemyActivationDamageProbe>();
                probe.Target = enemy;
                enemyObject.SetActive(true);

                Assert.That(probe.Result.WasApplied, Is.True);
                Assert.That(probe.Result.WasKilled, Is.False);
                Assert.That(enemy.CurrentHealth, Is.EqualTo(15f));
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

    /// <summary>在 GameObject 激活回调中提交一次伤害，用于复现首帧前的调用顺序。</summary>
    [ExecuteAlways]
    public sealed class EnemyActivationDamageProbe : MonoBehaviour
    {
        public Enemy Target { get; set; }
        public DamageResult Result { get; private set; }

        private void OnEnable()
        {
            if (Target != null)
            {
                Result = Target.ReceiveDamageRequest(new DamageRequest
                {
                    DamageAmount = 5f,
                    DamageType = DamageType.Physical
                });
            }
        }
    }
}
