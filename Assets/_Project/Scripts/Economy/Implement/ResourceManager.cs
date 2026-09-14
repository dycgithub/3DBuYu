using System;
using R3;
using UnityEngine;
using Services;

namespace GameSystem
{
    /// <summary>
    /// 管理全局积分余额，并负责积分存档的加载和自动保存。
    /// </summary>
    public class ResourceManager : MonoBehaviour, IPointsService
    {
        [Header("初始值")]
        [SerializeField] private int initialPoints = 1000;

        [Header("存档")]
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autoSaveInterval = 60f;

        private readonly ReactiveProperty<int> _points = new();
        private float _autoSaveTimer;

        /// <summary>当前积分(可观察:R3,订阅即得当前值,变化实时推送)。</summary>
        public ReadOnlyReactiveProperty<int> Points => _points;

        /// <summary>
        /// 初始化资源管理器的运行时状态。
        /// </summary>
        private void Awake()
        {
        }

        /// <summary>
        /// 加载积分存档；没有存档时使用 Inspector 配置的初始积分。
        /// </summary>
        private void Start()
        {
            if (!LoadData())
            {
                _points.Value = initialPoints;
            }

            // R3 ReactiveProperty 订阅时立即推送当前值,无需手动广播初始值
        }

        /// <summary>
        /// 按未缩放时间推进自动保存计时器。
        /// </summary>
        private void Update()
        {
            if (!autoSave)
            {
                return;
            }

            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                SaveData();
                _autoSaveTimer = 0f;
            }
        }

        /// <summary>
        /// 销毁资源管理器前保存积分并释放响应式状态。
        /// </summary>
        private void OnDestroy()
        {
            if (autoSave)
            {
                SaveData();
            }

            _points.Dispose();
        }

        /// <summary>
        /// 增加全局积分。
        /// </summary>
        /// <param name="amount">要增加的积分。</param>
        /// <param name="source">积分来源说明。</param>
        public void AddPoints(int amount, string source = "")
        {
            if (amount <= 0)
            {
                return;
            }

            _points.Value += amount;
        }

        /// <summary>
        /// 在余额足够时扣除全局积分。
        /// </summary>
        /// <param name="amount">要扣除的积分。</param>
        /// <param name="reason">积分消耗原因。</param>
        /// <returns>扣除成功或无需扣除时返回 true。</returns>
        public bool SpendPoints(int amount, string reason = "")
        {
            if (amount <= 0)
            {
                return true;
            }

            if (_points.Value < amount)
            {
                return false;
            }

            _points.Value -= amount;
            return true;
        }

        /// <summary>
        /// 判断当前积分是否足够支付指定数量。
        /// </summary>
        /// <param name="amount">所需积分。</param>
        /// <returns>余额不少于所需积分时返回 true。</returns>
        public bool HasEnoughPoints(int amount)
        {
            return _points.Value >= amount;
        }

        /// <summary>
        /// 保存当前积分数据。
        /// </summary>
        public void SaveData()
        {
            SaveSystem.SaveResourceData(this);
        }

        /// <summary>
        /// 加载积分数据。
        /// </summary>
        /// <returns>成功加载存档时返回 true。</returns>
        public bool LoadData()
        {
            var data = SaveSystem.LoadResourceData();
            if (data == null)
            {
                return false;
            }

            _points.Value = data.points;
            return true;
        }

        /// <summary>
        /// 将积分重置为 Inspector 配置的初始值并立即保存。
        /// </summary>
        public void ResetResources()
        {
            _points.Value = initialPoints;
            SaveData();
        }

        /// <summary>
        /// 创建当前积分的存档数据快照。
        /// </summary>
        /// <returns>当前积分存档对象。</returns>
        public ResourceSaveData GetSaveData()
        {
            return new ResourceSaveData
            {
                points = _points.Value
            };
        }
    }

    [Serializable]
    public class ResourceSaveData
    {
        public int points;
    }
}
