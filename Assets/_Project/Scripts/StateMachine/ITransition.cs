namespace StateMachineSystem
{
    /// <summary>公开目标状态和判断本次转换是否成立。</summary>
    /// <typeparam name="TContext">转换条件使用的宿主上下文。</typeparam>
    public interface ITransition<TContext>
    {
        /// <summary>条件成立后进入的状态。</summary>
        IState<TContext> TargetState { get; }

        /// <summary>判断转换条件是否成立。</summary>
        /// <param name="context">状态机宿主上下文。</param>
        /// <returns>条件成立时返回 true。</returns>
        bool CanTransition(TContext context);
    }
}
