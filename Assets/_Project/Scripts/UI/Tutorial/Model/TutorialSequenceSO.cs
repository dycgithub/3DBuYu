using System.Collections.Generic;
using UnityEngine;

namespace _Project.UI.Tutorial.Model
{
    /// <summary>可由多个按钮复用的教程步骤配置资产。</summary>
    [CreateAssetMenu(fileName = "TutorialSequence", menuName = "UI/Tutorial Sequence")]
    public sealed class TutorialSequenceSO : ScriptableObject
    {
        [SerializeField] private string _sequenceId;
        [SerializeField] private List<TutorialStepData> _steps = new();

        /// <summary>教程的稳定标识。</summary>
        public string SequenceId => _sequenceId;

        /// <summary>只读访问教程步骤。</summary>
        public IReadOnlyList<TutorialStepData> Steps => _steps;
    }
}
