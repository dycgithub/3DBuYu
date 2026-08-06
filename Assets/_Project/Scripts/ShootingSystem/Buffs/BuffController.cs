using System.Collections.Generic;
using UnityEngine;

namespace ShootingSystem.Buffs
{
    public class BuffController : MonoBehaviour
    {
        private List<BuffBase> _buffs = new List<BuffBase>();
        private List<BuffBase> _pendingRemove = new List<BuffBase>();

        private void Update()
        {
            float dt = Time.deltaTime;
            _pendingRemove.Clear();

            foreach (var buff in _buffs)
            {
                buff.TimeRemaining -= dt;
                buff.OnTick(dt);
                if (buff.IsExpired)
                    _pendingRemove.Add(buff);
            }

            foreach (var buff in _pendingRemove)
            {
                buff.OnExpire();
                _buffs.Remove(buff);
            }
        }

        public void AddBuff(BuffConfig config)
        {
            var existing = _buffs.Find(b => b.Config != null && b.Config.Type == config.Type);
            if (existing != null)
            {
                existing.TimeRemaining = config.Duration;
                return;
            }

            var buff = CreateBuff(config);
            if (buff != null)
            {
                _buffs.Add(buff);
                buff.OnApply();
            }
        }

        public float GetModifier(BuffType type)
        {
            foreach (var buff in _buffs)
            {
                if (buff.Config != null && buff.Config.Type == type)
                    return buff.Config.Value;
            }
            return 1f;
        }

        public void RemoveAll()
        {
            foreach (var buff in _buffs)
                buff.OnExpire();
            _buffs.Clear();
        }

        private static BuffBase CreateBuff(BuffConfig config)
        {
            switch (config.Type)
            {
                case BuffType.DamageTakenMultiplier:
                    return new DamageTakenMultiplierBuff { Config = config, TimeRemaining = config.Duration };
                default:
                    // 数值型 buff（敌人侧与玩家/弹药侧通用），无附加逻辑，数值经 GetModifier 读取
                    return new StatBuff { Config = config, TimeRemaining = config.Duration };
            }
        }
    }
}
