using System.Collections.Generic;

namespace StateMachineSystem
{
    /// <summary>定义状态生命周期和按顺序检查的转换列表。</summary>
    /// <typeparam name="TContext">状态使用的宿主上下文。</typeparam>
    public interface IState<TContext>
    {
        /// <summary>当前状态声明的有序转换；未声明时没有自动转换。</summary>
        IReadOnlyList<ITransition<TContext>> Transitions =>
            System.Array.Empty<ITransition<TContext>>();

        /// <summary>状态成为当前状态时调用。</summary>
        /// <param name="context">状态宿主上下文。</param>
        void Enter(TContext context);

        /// <summary>状态保持活动时调用。</summary>
        /// <param name="context">状态宿主上下文。</param>
        void Tick(TContext context);

        /// <summary>状态被替换时调用。</summary>
        /// <param name="context">状态宿主上下文。</param>
        void Exit(TContext context);
    }
}
