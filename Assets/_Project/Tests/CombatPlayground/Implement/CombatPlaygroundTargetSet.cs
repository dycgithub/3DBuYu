using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using FlockingSystem.ECS;
using Interfaces;
using UnityEngine;
using VContainer;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>维护实验敌人、预设位置和移动目标，支持多目标技能与弹道测试。</summary>
    public sealed class CombatPlaygroundTargetSet : MonoBehaviour
    {
        private const float HealthMultiplierStep = 0.5f;
        private const float MotionAmplitude = 1.5f;
        private const float MotionFrequency = 1.3f;

        [SerializeField] private Enemy[] targets;
        [SerializeField] private Enemy movingTarget;
        [SerializeField] private Enemy behaviorTarget;
        [SerializeField] private GameObject behaviorSourcePrefab;

        [Inject] private EnemyFlockRuntimeService _flockRuntime;

        private readonly List<IDamageable> _livingTargets = new();
        private Vector3[] _startPositions;
        private Quaternion[] _startRotations;
        private bool _isMoving;
        private float _motionStartTime;

        /// <summary>读取默认选择目标。</summary>
        internal Enemy PrimaryTarget => targets != null && targets.Length > 0 ? targets[0] : null;

        /// <summary>缓存每个目标的场景初始姿态。</summary>
        private void Awake()
        {
            if (targets == null)
            {
                _startPositions = System.Array.Empty<Vector3>();
                _startRotations = System.Array.Empty<Quaternion>();
                return;
            }

            _startPositions = new Vector3[targets.Length];
            _startRotations = new Quaternion[targets.Length];
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] == null)
                {
                    continue;
                }

                _startPositions[index] = targets[index].transform.position;
                _startRotations[index] = targets[index].transform.rotation;
            }
        }

        /// <summary>初始化目标生命并登记 Buff 与空间查询入口。</summary>
        private void Start()
        {
            if (behaviorTarget != null)
            {
                behaviorTarget.SourcePrefab = behaviorSourcePrefab;
            }

            for (int index = 0; targets != null && index < targets.Length; index++)
            {
                targets[index]?.InitializeForSpawn(1f + index * HealthMultiplierStep, 1f);
            }

            RefreshBehaviorAgent(behaviorTarget);
        }

        /// <summary>推进追踪实验目标的横向移动。</summary>
        private void Update()
        {
            if (!_isMoving || movingTarget == null || targets == null)
            {
                return;
            }

            int index = System.Array.IndexOf(targets, movingTarget);
            if (index < 0 || index >= _startPositions.Length)
            {
                return;
            }

            float elapsed = Time.time - _motionStartTime;
            float offset = Mathf.Sin(elapsed * MotionFrequency) * MotionAmplitude;
            movingTarget.transform.position = _startPositions[index] + Vector3.right * offset;
        }

        /// <summary>判断点击对象是否属于本实验场景。</summary>
        internal bool Contains(Enemy enemy)
        {
            return enemy != null && targets != null && System.Array.IndexOf(targets, enemy) >= 0;
        }

        /// <summary>生成当前存活目标快照，供隔离动作执行器使用。</summary>
        internal IReadOnlyList<IDamageable> GetLivingTargets()
        {
            _livingTargets.Clear();
            if (targets == null)
            {
                return _livingTargets;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                Enemy enemy = targets[index];
                if (enemy != null && !enemy.IsDead && enemy.gameObject.activeInHierarchy)
                {
                    _livingTargets.Add(enemy);
                }
            }

            return _livingTargets;
        }

        /// <summary>切换偏轴目标的移动状态，用于追踪轨迹测试。</summary>
        internal void ToggleMotion()
        {
            _isMoving = !_isMoving;
            _motionStartTime = Time.time;
            if (!_isMoving)
            {
                RestoreMovingTargetPosition();
            }
        }

        /// <summary>重置敌人位置、生命、Buff 和目标移动状态。</summary>
        internal void ResetAll()
        {
            _isMoving = false;
            if (targets == null)
            {
                return;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                Enemy enemy = targets[index];
                if (enemy == null)
                {
                    continue;
                }

                enemy.transform.SetPositionAndRotation(_startPositions[index], _startRotations[index]);
                enemy.ResetForReuse();
                enemy.InitializeForSpawn(1f + index * HealthMultiplierStep, 1f);
            }

            RefreshBehaviorAgent(behaviorTarget);
        }

        /// <summary>同步Shooter的ECS Agent，以应用移动倍率和定向移动命令。</summary>
        internal bool RefreshBehaviorAgent(Enemy enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            if (enemy != behaviorTarget)
            {
                return true;
            }

            if (_flockRuntime == null || enemy.SourcePrefab == null)
            {
                return false;
            }

            EnemyFlockBridge bridge = enemy.GetComponent<EnemyFlockBridge>();
            if (bridge == null)
            {
                return false;
            }

            return _flockRuntime.TryAcquireAgent(
                bridge,
                enemy.SourcePrefab,
                enemy.EnemyType,
                enemy.SpeedMultiplier,
                enemy.transform.position,
                enemy.transform.rotation);
        }

        private void RestoreMovingTargetPosition()
        {
            int index = System.Array.IndexOf(targets, movingTarget);
            if (index >= 0 && index < _startPositions.Length && movingTarget != null)
            {
                movingTarget.transform.SetPositionAndRotation(
                    _startPositions[index],
                    _startRotations[index]);
            }
        }
    }
}
