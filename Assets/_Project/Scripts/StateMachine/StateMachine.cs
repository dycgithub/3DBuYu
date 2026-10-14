using System;
using System.Collections.Generic;

namespace StateMachineSystem
{
    /// <summary>持有一个状态，并由调用方显式推进其生命周期。</summary>
    /// <typeparam name="TContext">传递给状态的宿主上下文。</typeparam>
    public sealed class StateMachine<TContext>
    {
        private readonly TContext _context;
        private IState<TContext> _currentState;

        /// <summary>绑定状态机使用的上下文。</summary>
        /// <param name="context">状态宿主上下文。</param>
        public StateMachine(TContext context)
        {
            _context = context;
        }

        /// <summary>当前状态；尚未切换时为 null。</summary>
        public IState<TContext> CurrentState => _currentState;

        /// <summary>切换状态；同一实例不会重复执行进入和退出。</summary>
        /// <param name="nextState">要切换到的状态。</param>
        public void ChangeState(IState<TContext> nextState)
        {
            if (ReferenceEquals(_currentState, nextState))
            {
                return;
            }

            _currentState?.Exit(_context);
            _currentState = nextState;
            _currentState?.Enter(_context);
        }

        /// <summary>推进当前状态，不绑定 Unity 帧循环。</summary>
        public void Tick()
        {
            IState<TContext> state = _currentState;
            if (state == null)
            {
                return;
            }

            state.Tick(_context);
            if (!ReferenceEquals(state, _currentState))
            {
                return;
            }

            IReadOnlyList<ITransition<TContext>> transitions = state.Transitions;
            for (int index = 0; index < transitions.Count; index++)
            {
                ITransition<TContext> transition = transitions[index];
                if (!transition.CanTransition(_context) ||
                    ReferenceEquals(state, transition.TargetState))
                {
                    continue;
                }

                ChangeState(transition.TargetState);
                break;
            }
        }
    }
}
