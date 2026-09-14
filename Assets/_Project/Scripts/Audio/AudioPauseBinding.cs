using System;
using Services;
using VContainer.Unity;

namespace GameSystem
{
    /// <summary>
    /// 把 GameLoop 的暂停状态桥接给跨场景 AudioManager。
    /// 桥接对象随子容器销毁，避免全局服务持有已失效的场景引用。
    /// </summary>
    public sealed class AudioPauseBinding : IStartable, IDisposable
    {
        private readonly AudioManager _audioManager;
        private readonly IGamePauseService _pauseService;

        public AudioPauseBinding(AudioManager audioManager, IGamePauseService pauseService)
        {
            _audioManager = audioManager;
            _pauseService = pauseService;
        }

        public void Start()
        {
            _audioManager.BindPauseService(_pauseService);
        }

        public void Dispose()
        {
            _audioManager.UnbindPauseService(_pauseService);
        }
    }
}
