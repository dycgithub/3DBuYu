using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>管理一个目标身上的 Buff 生命周期、层数和数值倍率。</summary>
    public class BuffController : MonoBehaviour
    {
        private List<BuffBase> _buffs = new List<BuffBase>();
        private List<BuffBase> _pendingRemove = new List<BuffBase>();//待办

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
            AddBuff(config, 0);
        }

        public void AddBuff(BuffConfig config, int sourceId)
        {
            if (config == null)
                return;

            BuffBase existing = FindExistingBuff(config.Type, sourceId);

            if (existing != null && config.StackPolicy != BuffStackPolicy.Independent)
            {
                switch (config.StackPolicy)
                {
                    case BuffStackPolicy.AddStack:
                        existing.Stacks = Mathf.Min(
                            Mathf.Max(1, config.MaxStacks),
                            existing.Stacks + 1);
                        existing.TimeRemaining = config.Duration;
                        break;
                    case BuffStackPolicy.Replace:
                        existing.Config = config;
                        existing.Stacks = 1;
                        existing.TimeRemaining = config.Duration;
                        break;
                    default:
                        existing.TimeRemaining = config.Duration;
                        break;
                }
                return;
            }

            var buff = CreateBuff(config);
            if (buff != null)
            {
                buff.SourceId = sourceId;
                buff.Stacks = 1;
                _buffs.Add(buff);
                buff.OnApply();
            }
        }

        public float GetModifier(BuffType type)
        {
            float result = 1f;
            foreach (var buff in _buffs)
            {
                if (buff.Config != null && buff.Config.Type == type)
                {
                    float value = Mathf.Max(0f, buff.Config.Value);
                    for (int i = 0; i < Mathf.Max(1, buff.Stacks); i++)
                        result *= value;
                }
            }

            return result;
        }

        public void RemoveBySource(int sourceId)
        {
            _pendingRemove.Clear();
            foreach (var buff in _buffs)
            {
                if (buff.SourceId == sourceId)
                    _pendingRemove.Add(buff);
            }

            foreach (var buff in _pendingRemove)
            {
                buff.OnExpire();
                _buffs.Remove(buff);
            }
        }

        public void RemoveAll()
        {
            foreach (var buff in _buffs)
                buff.OnExpire();
            _buffs.Clear();
        }

        private BuffBase FindExistingBuff(BuffType type, int sourceId)
        {
            for (int index = 0; index < _buffs.Count; index++)
            {
                BuffBase buff = _buffs[index];
                if (buff.Config != null && buff.Config.Type == type && buff.SourceId == sourceId)
                    return buff;
            }

            return null;
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
