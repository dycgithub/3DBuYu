using System;
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

        private int currentPoints;
        private float autoSaveTimer;

        public int Points => currentPoints;

        public event Action<int, int> OnPointsChanged;

        private void Awake()
        {
        }

        private void Start()
        {
            if (!LoadData())
                currentPoints = initialPoints;

            // 广播初始值,避免 UI 在 Start 顺序中读到旧值
            OnPointsChanged?.Invoke(currentPoints, 0);
        }

        private void Update()
        {
            if (!autoSave) return;
            autoSaveTimer += Time.deltaTime;
            if (autoSaveTimer >= autoSaveInterval)
            {
                SaveData();
                autoSaveTimer = 0f;
            }
        }

        private void OnApplicationQuit()
        {
            if (autoSave) SaveData();
        }

        public void AddPoints(int amount, string source = "")
        {
            if (amount <= 0) return;
            int old = currentPoints;
            currentPoints += amount;
            int added = currentPoints - old;

            OnPointsChanged?.Invoke(currentPoints, added);
        }

        public bool SpendPoints(int amount, string reason = "")
        {
            if (amount <= 0) return true;
            if (currentPoints < amount) return false;
            currentPoints -= amount;

            OnPointsChanged?.Invoke(currentPoints, -amount);
            return true;
        }

        public bool HasEnoughPoints(int amount) => currentPoints >= amount;

        public void SaveData()
        {
            SaveSystem.SaveResourceData(this);
        }

        public bool LoadData()
        {
            var data = SaveSystem.LoadResourceData();
            if (data == null) return false;
            currentPoints = data.points;
            return true;
        }

        public void ResetResources()
        {
            currentPoints = initialPoints;
            OnPointsChanged?.Invoke(currentPoints, 0);
            SaveData();
        }

        public ResourceSaveData GetSaveData()
        {
            return new ResourceSaveData
            {
                points = currentPoints
            };
        }
    }

    [Serializable]
    public class ResourceSaveData
    {
        public int points;
    }
}
