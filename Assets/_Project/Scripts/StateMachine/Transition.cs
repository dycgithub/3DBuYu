using System;

namespace StateMachineSystem
{
    /// <summary>用条件委托和目标状态定义一次转换。</summary>
    /// <typeparam name="TContext">转换条件使用的宿主上下文。</typeparam>
    public sealed class Transition<TContext> : ITransition<TContext>
    {
        private readonly Func<TContext, bool> _condition;

        /// <summary>创建条件转换。</summary>
        /// <param name="condition">判断是否转换的条件。</param>
        /// <param name="targetState">条件成立后的目标状态。</param>
        public Transition(Func<TContext, bool> condition, IState<TContext> targetState)
        {
            _condition = condition;
            TargetState = targetState;
        }

        /// <summary>条件成立后进入的状态。</summary>
        public IState<TContext> TargetState { get; }

        /// <summary>执行条件委托并返回转换判断结果。</summary>
        /// <param name="context">状态机宿主上下文。</param>
        /// <returns>条件成立时返回 true。</returns>
        public bool CanTransition(TContext context)
        {
            return _condition(context);
        }
    }
}
