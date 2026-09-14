using CombatSystem;
using EnemySystem;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人致死时先保留死亡高亮状态，供对象池延迟回收期间显示。
    /// </summary>
    public sealed class EnemyDeathHighlightTests
    {
        [Test]
        public void LethalDamage_AppliesDeathHighlightBeforeDeathEvent()
        {
            GameObject enemyObject = new("EnemyDeathHighlightTest");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
            Material material = null;
            Mesh mesh = null;
            try
            {
                MeshFilter meshFilter = enemyObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = enemyObject.AddComponent<MeshRenderer>();
                mesh = new Mesh
                {
                    vertices = new[]
                    {
                        new Vector3(-1f, -1f, 0f),
                        new Vector3(1f, -1f, 0f),
                        new Vector3(0f, 1f, 0f)
                    },
                    triangles = new[] { 0, 1, 2 }
                };
                meshFilter.sharedMesh = mesh;
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                renderer.sharedMaterial = material;
                enemyObject.AddComponent<BoxCollider>();
                Enemy enemy = enemyObject.AddComponent<Enemy>();

                attributes.baseHealth = 10f;
                attributes.hitEffect = EffectSystem.EffectId.None;
                attributes.deathEffect = EffectSystem.EffectId.None;
                SerializedObject serialized = new(enemy);
                serialized.FindProperty("enemyAttributes").objectReferenceValue = attributes;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                enemy.InitializeForSpawn(1f, 1f);

                DamageResult result = enemy.ReceiveDamageRequest(new DamageRequest
                {
                    DamageAmount = 10f,
                    DamageType = DamageType.Physical,
                    HitPosition = Vector3.zero,
                    HitNormal = Vector3.forward
                });

                FieldInfo colorField = typeof(Enemy).GetField(
                    "_activeFlashColor",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo timerField = typeof(Enemy).GetField(
                    "_damageFlashTimer",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(result.WasKilled, Is.True);
                Assert.That(enemy.IsDead, Is.True);
                Assert.That(enemyObject.activeSelf, Is.True);
                Assert.That(colorField.GetValue(enemy), Is.EqualTo(new Color(1f, 0.75f, 0.2f, 1f)));
                Assert.That((float)timerField.GetValue(enemy), Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
                Object.DestroyImmediate(attributes);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
