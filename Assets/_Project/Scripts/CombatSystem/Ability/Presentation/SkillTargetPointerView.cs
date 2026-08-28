using Interfaces;
using UnityEngine;
using VContainer;

namespace CombatSystem
{
    /// <summary>目标指针的 Unity 表现适配层，具体视觉资源由场景后续绑定。</summary>
    public sealed class SkillTargetPointerView : MonoBehaviour
    {
        [Inject] private SkillTargetPointer _pointer;

        public bool Confirm(IDamageable target)
        {
            return _pointer != null && _pointer.Confirm(target);
        }

        public void Clear()
        {
            _pointer?.Clear();
        }
    }
}
