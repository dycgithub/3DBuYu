using System;
using EnemySystem;
using GameSystem;
using R3;
using Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace _Project.UI.Item
{
    /// <summary>
    /// 战斗状态面板(经过时间 / 数量配额 / 存活怪物 / 积分)。
    /// 数据源:GameManager(战斗场景)、IEnemyPopulationService(战斗场景)、IPointsService(全局)。
    /// 基地场景无敌群/战斗数据时面板整体隐藏。
    /// </summary>
    public class BattleAttributesPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statsText;

        private readonly System.Collections.Generic.List<IDisposable> _rx = new();

        [Inject] private GameManager _gameManager;
        private IEnemyPopulationService _population;
        [Inject] private IPointsService _points;
        [Inject] private IObjectResolver _resolver;
        private bool _initialized;
        private bool _hasData;
        private bool _runTimeSubscribed;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }
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
            if (_resolver != null)
            {
                _resolver.TryResolve(out _population);
            }

            _hasData = _gameManager != null && _population != null && _points != null;
        }

        private void Subscribe()
        {
            if (_gameManager != null)
            {
                _gameManager.OnRunTimeChanged += HandleRunTimeChanged;
                _runTimeSubscribed = true;
            }

            if (_points != null)
            {
                _rx.Add(_points.Points.Subscribe(_ => Refresh()));
            }

            if (_population != null)
            {
                _rx.Add(_population.OnPopulationStarted.Subscribe(_ => Refresh()));
                _rx.Add(_population.OnEnemiesAliveChanged.Subscribe(_ => Refresh()));
            }
        }

        private void OnDestroy()
        {
            foreach (var d in _rx)
            {
                d.Dispose();
            }
            _rx.Clear();

            if (_runTimeSubscribed && _gameManager != null)
            {
                _gameManager.OnRunTimeChanged -= HandleRunTimeChanged;
            }
        }

        private void Refresh()
        {
            if (_statsText == null)
            {
                return;
            }

            float elapsed = _gameManager?.Session != null
                ? Mathf.Max(0f, _gameManager.Session.ElapsedTime)
                : 0f;
            var time = TimeSpan.FromSeconds(elapsed);
            string timeStr = string.Format("{0:D2}:{1:D2}", (int)time.TotalMinutes, time.Seconds);

            int maximum = _population != null ? _population.MaximumPopulation : 0;
            int alive = _population != null ? _population.EnemiesAlive : 0;
            int points = _points != null ? _points.Points.CurrentValue : 0;

            _statsText.text = string.Format(
                "经过时间  {0}\n存活怪物  {1}/{2}\n积分      {3}",
                timeStr, alive, maximum, points);
        }

        private void HandleRunTimeChanged(float _)
        {
            Refresh();
        }
    }
}
