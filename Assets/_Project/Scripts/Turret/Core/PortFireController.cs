using System.Collections.Generic;
using UnityEngine;
using Interfaces;
using Services;
using VContainer;
using ShootingSystem;
using ItemSystem;

namespace TurretSystem
{
    public class PortFireController : MonoBehaviour
    {
        [Header("发射点")]
        public Transform[] firePoints;

        [Header("子弹")]
        public BulletProfile bulletProfile;
        public float fireRate = 2f;

        [Header("开火模式")]
        public bool autoFire = true;
        public PhysicsTurretDetector detector;

        [Inject] private IBulletSpawner _spawner;
        [Inject] private IInputService _input;

        private Turret _turret;
        private readonly Dictionary<int, float> _lastFireTime = new Dictionary<int, float>();

        private void Awake()
        {
            _turret = GetComponent<Turret>();
        }

        private void Update()
        {
            if (_spawner == null || firePoints == null) return;

            int maxPorts = Mathf.Min(firePoints.Length, _input?.MaxPorts ?? 0);
            for (int i = 0; i < maxPorts; i++)
            {
                if (_turret.GetPort(i) is { IsLocked: true }) continue;

                bool shouldFire = false;

                if (autoFire && detector != null && detector.HasTarget(i))
                {
                    shouldFire = true;
                }
                
                if (_input != null && _input.IsPortFireHeld(i))
                {
                    shouldFire = true;
                    
                }

                if (shouldFire && !IsOnCooldown(i))
                    FireAtTarget(i);
            }
        }

        private bool IsOnCooldown(int index)
        {
            // 射速 = 场景基础射速 × 端口属性倍率(弹药/技能物品可提升)
            float rate = fireRate;
            var port = _turret != null ? _turret.GetPort(index) : null;
            if (port?.Attributes != null && port.Attributes.FireRate > 0f)
                rate = fireRate * port.Attributes.FireRate;

            if (_lastFireTime.TryGetValue(index, out float last))
                return Time.time - last < 1f / Mathf.Max(0.001f, rate);
            return false;
        }

        private void FireAtTarget(int portIndex)
        {
            var fp = firePoints[portIndex];
            if (fp == null) return;

            var profile = bulletProfile;
            float damageOverride = 0f;

            // 端口弹药:决定弹种 + 攻击加成(伤害覆写)
            var port = _turret != null ? _turret.GetPort(portIndex) : null;
            if (port?.Inventory != null)
            {
                foreach (var placed in port.Inventory.GetAllItems())
                {
                    var config = port.Inventory.GetItemConfig(placed.instanceId);
                    if (config == null || config.ItemType != ItemType.Ammunition) continue;

                    if (config.providedBulletConfig != null)
                        profile = config.providedBulletConfig;
                    if (config.attackBonus > 0f)
                        damageOverride = profile.Damage + config.attackBonus;
                    break;
                }
            }

            var request = new SpawnRequest
            {
                Profile = profile,
                DamageOverride = damageOverride,
                Origin = fp.position,
                Direction = fp.forward,
                Seed = Random.Range(int.MinValue, int.MaxValue)
            };
            _spawner.Spawn(request);
            _lastFireTime[portIndex] = Time.time;
        }
    }
}
