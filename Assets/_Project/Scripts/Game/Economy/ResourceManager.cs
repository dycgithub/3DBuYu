using System;
using R3;
using UnityEngine;
using Services;

namespace GameSystem
{
    public class ResourceManager : MonoBehaviour, IPointsService
    {
        [Header("初始值")]
        [SerializeField] private int initialPoints = 1000;

        [Header("存档")]
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autoSaveInterval = 60f;

        private readonly ReactiveProperty<int> _points = new();
        private float autoSaveTimer;

        /// <summary>当前积分(可观察:R3,订阅即得当前值,变化实时推送)。</summary>
        public ReadOnlyReactiveProperty<int> Points => _points;

        private void Awake()
        {
        }

        private void Start()
        {
            if (!LoadData())
                _points.Value = initialPoints;

            // R3 ReactiveProperty 订阅时立即推送当前值,无需手动广播初始值
        }

        private void Update()
        {
            if (!autoSave) return;
            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= autoSaveInterval)
            {
                SaveData();
                autoSaveTimer = 0f;
            }
        }

        private void OnDestroy()
        {
            if (autoSave) SaveData();
            _points.Dispose();
        }

        public void AddPoints(int amount, string source = "")
        {
            if (amount <= 0) return;
            _points.Value += amount;
        }

        public bool SpendPoints(int amount, string reason = "")
        {
            if (amount <= 0) return true;
            if (_points.Value < amount) return false;
            _points.Value -= amount;
            return true;
        }

        public bool HasEnoughPoints(int amount) => _points.Value >= amount;

        public void SaveData()
        {
            SaveSystem.SaveResourceData(this);
        }

        public bool LoadData()
        {
            var data = SaveSystem.LoadResourceData();
            if (data == null) return false;
            _points.Value = data.points;
            return true;
        }

        public void ResetResources()
        {
            _points.Value = initialPoints;
            SaveData();
        }

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
