using System;
using EnemySystem.Wave;
using GameSystem;
using R3;
using Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace _Project.UI.Item
{
    /// <summary>
    /// 战斗状态面板(剩余时间 / 波次 / 存活怪物 / 积分)。
    /// 数据源:TimeManager(全局)、IWaveEventService(战斗场景)、IPointsService(全局)。
    /// 基地场景无波次/战斗数据时面板整体隐藏。
    /// </summary>
    public class BattleAttributesPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statsText;

        private readonly System.Collections.Generic.List<IDisposable> _rx = new();

        private TimeManager _timeManager;
        private IWaveEventService _wave;
        private IPointsService _points;
        private bool _initialized;
        private bool _hasData;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            ResolveSources();

            if (!_hasData)
            {
                gameObject.SetActive(false);
                return;
            }

            Subscribe();
            Refresh();
        }

        private void ResolveSources()
        {
            var scope = ProjectLifetimeScope.Instance;
            if (scope?.Container != null)
            {
                try { _timeManager = scope.Container.Resolve<TimeManager>(); } catch { }
                try { _points = scope.Container.Resolve<IPointsService>(); } catch { }
            }

            _wave = FindFirstObjectByType<WaveController>() as IWaveEventService;

            _hasData = _timeManager != null && _wave != null && _points != null;
        }

        private void Subscribe()
        {
            if (_timeManager != null)
                _timeManager.OnTimeChanged += RefreshTime;

            if (_points != null)
                _points.OnPointsChanged += RefreshPoints;

            if (_wave != null)
            {
                _rx.Add(_wave.OnWaveChanged.Subscribe(_ => Refresh()));
                _rx.Add(_wave.OnEnemiesAliveChanged.Subscribe(_ => Refresh()));
            }
        }

        private void OnDestroy()
        {
            if (_timeManager != null)
                _timeManager.OnTimeChanged -= RefreshTime;

            if (_points != null)
                _points.OnPointsChanged -= RefreshPoints;

            foreach (var d in _rx)
                d.Dispose();
            _rx.Clear();
        }

        private void RefreshTime(float _) => Refresh();
        private void RefreshPoints(int _, int __) => Refresh();

        private void Refresh()
        {
            if (_statsText == null) return;

            var time = TimeSpan.FromSeconds(_timeManager != null ? Mathf.Max(0f, _timeManager.RemainingTime) : 0f);
            string timeStr = string.Format("{0:D2}:{1:D2}", (int)time.TotalMinutes, time.Seconds);

            int waveNum = _wave != null ? _wave.CurrentWaveNumber : 0;
            int totalWave = _wave != null ? _wave.TotalWaveCount : 0;
            int alive = _wave != null ? _wave.EnemiesAlive : 0;
            int points = _points != null ? _points.Points : 0;

            _statsText.text = string.Format(
                "剩余时间  {0}\n波次      {1}/{2}\n存活怪物  {3}\n积分      {4}",
                timeStr, waveNum, totalWave, alive, points);
        }
    }
}