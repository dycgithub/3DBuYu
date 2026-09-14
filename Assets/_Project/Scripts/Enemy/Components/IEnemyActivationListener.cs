namespace EnemySystem.Components
{
    /// <summary>
    /// 已挂载敌人机制在宿主完成激活后使用的内部初始化契约。
    /// </summary>
    internal interface IEnemyActivationListener
    {
        /// <summary>完成一次实体激活后的机制初始化。</summary>
        void OnEnemyActivationReady();
    }
}
