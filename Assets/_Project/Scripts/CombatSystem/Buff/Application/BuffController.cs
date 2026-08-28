using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    public sealed class BuffController : MonoBehaviour
    {
        private readonly List<BuffRuntime> _active = new();
        private readonly List<BuffRuntime> _expired = new();

        private void Update()
        {
            _expired.Clear();
            for (int i = 0; i < _active.Count; i++)
            {
                BuffRuntime buff = _active[i];
                buff.Tick(Time.deltaTime);
                if (buff.IsExpired)
                    _expired.Add(buff);
            }

            for (int i = 0; i < _expired.Count; i++)
                _active.Remove(_expired[i]);
        }

        public void AddBuff(BuffDefinition definition) => AddBuff(definition, 0);

        public void AddBuff(BuffDefinition definition, int sourceId)
        {
            if (definition == null)
                return;

            BuffRuntime existing = FindExisting(definition.Type, sourceId);
            if (existing != null && definition.StackPolicy != BuffStackPolicy.Independent)
            {
                switch (definition.StackPolicy)
                {
                    case BuffStackPolicy.AddStack:
                        existing.Stacks = Mathf.Min(
                            Mathf.Max(1, definition.MaxStacks),
                            existing.Stacks + 1);
                        existing.TimeRemaining = definition.Duration;
                        break;
                    case BuffStackPolicy.Replace:
                        existing.Definition = definition;
                        existing.Stacks = 1;
                        existing.TimeRemaining = definition.Duration;
                        break;
                    default:
                        existing.TimeRemaining = definition.Duration;
                        break;
                }
                return;
            }

            _active.Add(new BuffRuntime
            {
                Definition = definition,
                SourceId = sourceId,
                TimeRemaining = definition.Duration,
                Stacks = 1
            });
        }

        public float GetModifier(BuffType type)
        {
            float result = 1f;
            for (int i = 0; i < _active.Count; i++)
            {
                BuffRuntime buff = _active[i];
                if (buff.Definition == null || buff.Definition.Type != type)
                    continue;

                float value = Mathf.Max(0f, buff.Definition.Value);
                for (int stack = 0; stack < Mathf.Max(1, buff.Stacks); stack++)
                    result *= value;
            }
            return result;
        }

        public void RemoveBySource(int sourceId)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].SourceId == sourceId)
                    _active.RemoveAt(i);
            }
        }

        public void RemoveAll() => _active.Clear();

        private BuffRuntime FindExisting(BuffType type, int sourceId)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                BuffRuntime buff = _active[i];
                if (buff.Definition != null && buff.Definition.Type == type && buff.SourceId == sourceId)
                    return buff;
            }
            return null;
        }
    }
}
