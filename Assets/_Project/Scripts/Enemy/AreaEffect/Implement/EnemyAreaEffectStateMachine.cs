using System;
using System.Collections.Generic;
using EnemySystem.AreaEffect.Model;
using StateMachineSystem;
using UnityEngine;

namespace EnemySystem.AreaEffect.Implement
{
    /// <summary>管理范围精英的战术状态，并输出供组件执行的运动意图。</summary>
    internal sealed class EnemyAreaEffectStateMachine
    {
        private readonly EnemyAttributesSO _attributes;
        private readonly Context _context;
        private readonly FlockingState _flocking;
        private readonly SensingState _sensing;
        private readonly ApproachingState _approaching;
        private readonly CastingState _casting;
        private readonly CooldownState _cooldown;
        private readonly RetreatingState _retreating;
        private readonly StateMachine<Context> _machine;

        /// <summary>创建状态实例、转换表，并从群游状态开始。</summary>
        /// <param name="attributes">当前范围精英的静态战术配置。</param>
        internal EnemyAreaEffectStateMachine(EnemyAttributesSO attributes)
        {
            _attributes = attributes;
            _context = new Context();
            _flocking = new FlockingState();
            _sensing = new SensingState();
            _approaching = new ApproachingState();
            _casting = new CastingState();
            _cooldown = new CooldownState();
            _retreating = new RetreatingState();
            _machine = new StateMachine<Context>(_context);
            BuildTransitions();
            _machine.ChangeState(_flocking);
        }

        /// <summary>当前战术状态。</summary>
        internal EnemyAreaEffectState CurrentState =>
            _machine.CurrentState is AreaState state ? state.State : EnemyAreaEffectState.Flocking;

        /// <summary>当前状态要求组件执行的运动意图。</summary>
        internal EnemyAreaEffectIntent CurrentIntent => _context.Intent;

        /// <summary>仅在持续施法窗口内为真。</summary>
        internal bool IsCasting => CurrentState == EnemyAreaEffectState.Casting;

        /// <summary>根据最新感知结果推进一次状态转换。</summary>
        /// <param name="frame">当前目标、距离、生命和帧间隔快照。</param>
        internal void Tick(EnemyAreaEffectFrame frame)
        {
            if (_attributes == null)
            {
                Reset();
                return;
            }

            _context.Frame = frame;
            _machine.Tick();
        }

        /// <summary>清除计时并恢复群游状态，供对象池和死亡生命周期使用。</summary>
        internal void Reset()
        {
            _context.Reset();
            _machine.ChangeState(_flocking);
            _context.Intent = EnemyAreaEffectIntent.Flocking;
        }

        /// <summary>按优先级连接撤退、感知、接近、施法和冷却状态。</summary>
        private void BuildTransitions()
        {
            _flocking.AddTransition(NeedsRetreat, _retreating);
            _flocking.AddTransition(HasTarget, _sensing);

            _sensing.AddTransition(NeedsRetreat, _retreating);
            _sensing.AddTransition(context => !context.Frame.HasTarget, _flocking);
            _sensing.AddTransition(TargetInEffectRange, _casting);
            _sensing.AddTransition(HasTarget, _approaching);

            _approaching.AddTransition(NeedsRetreat, _retreating);
            _approaching.AddTransition(context => !context.Frame.HasTarget, _flocking);
            _approaching.AddTransition(TargetInEffectRange, _casting);

            _casting.AddTransition(NeedsRetreat, _retreating);
            _casting.AddTransition(context => !context.Frame.HasTarget, _sensing);
            _casting.AddTransition(CastWindowComplete, _cooldown);

            _cooldown.AddTransition(NeedsRetreat, _retreating);
            _cooldown.AddTransition(context => !context.Frame.HasTarget, _flocking);
            _cooldown.AddTransition(CooldownComplete, _sensing);

            _retreating.AddTransition(
                context => RetreatDistanceReached(context) && context.Frame.HasTarget,
                _sensing);
            _retreating.AddTransition(
                context => RetreatDistanceReached(context) && !context.Frame.HasTarget,
                _flocking);
        }

        /// <summary>根据玩家距离和生命比例判断是否需要优先撤退。</summary>
        private bool NeedsRetreat(Context context)
        {
            return EnemyAreaEffectTactics.NeedsRetreat(
                context.Frame.PlayerDistance,
                _attributes.areaMinimumSafeDistance,
                context.Frame.HealthPercent,
                _attributes.areaPreferredDistance,
                _attributes.areaMaximumRetreatDistance,
                _attributes.areaLowHealthRetreatThreshold);
        }

        /// <summary>读取当前感知快照中的目标有效状态。</summary>
        private bool HasTarget(Context context) => context.Frame.HasTarget;

        /// <summary>判断目标是否已经进入范围效果半径。</summary>
        private bool TargetInEffectRange(Context context)
        {
            float radius = Mathf.Max(0f, _attributes.areaEffectRadius);
            return context.Frame.HasTarget && context.Frame.TargetDistance <= radius;
        }

        /// <summary>判断当前施法窗口是否结束。</summary>
        private bool CastWindowComplete(Context context)
        {
            return context.StateTime >= Mathf.Max(0f, _attributes.areaCastWindowSeconds);
        }

        /// <summary>判断施法后的冷却是否结束。</summary>
        private bool CooldownComplete(Context context)
        {
            return context.StateTime >= Mathf.Max(0f, _attributes.areaCastCooldownSeconds);
        }

        /// <summary>判断是否达到生命比例对应的安全距离。</summary>
        private bool RetreatDistanceReached(Context context)
        {
            float targetDistance = EnemyAreaEffectTactics.RetreatDistance(
                _attributes.areaPreferredDistance,
                _attributes.areaMaximumRetreatDistance,
                context.Frame.HealthPercent);
            return float.IsInfinity(context.Frame.PlayerDistance) ||
                context.Frame.PlayerDistance >= targetDistance;
        }

        private sealed class Context
        {
            internal EnemyAreaEffectFrame Frame;
            internal float StateTime;
            internal EnemyAreaEffectIntent Intent;

            /// <summary>清空当前帧、状态计时和组件运动意图。</summary>
            internal void Reset()
            {
                Frame = default;
                StateTime = 0f;
                Intent = EnemyAreaEffectIntent.Flocking;
            }
        }

        private abstract class AreaState : IState<Context>
        {
            private readonly List<ITransition<Context>> _transitions = new();
            private readonly EnemyAreaEffectIntent _intent;

            protected AreaState(EnemyAreaEffectState state, EnemyAreaEffectIntent intent)
            {
                State = state;
                _intent = intent;
            }

            internal EnemyAreaEffectState State { get; }
            public IReadOnlyList<ITransition<Context>> Transitions => _transitions;

            /// <summary>追加一个按优先级检查的状态转换条件。</summary>
            internal void AddTransition(
                Func<Context, bool> condition,
                IState<Context> target)
            {
                _transitions.Add(new Transition<Context>(condition, target));
            }

            /// <summary>进入状态时重置计时并发布对应运动意图。</summary>
            public virtual void Enter(Context context)
            {
                context.StateTime = 0f;
                context.Intent = _intent;
            }

            /// <summary>推进状态计时并保持状态运动意图。</summary>
            public virtual void Tick(Context context)
            {
                context.StateTime += context.Frame.DeltaTime;
                context.Intent = _intent;
            }

            /// <summary>离开状态时清理状态自有资源。</summary>
            public virtual void Exit(Context context)
            {
            }
        }

        private sealed class FlockingState : AreaState
        {
            /// <summary>创建没有定向目标时由 ECS 群游控制的状态。</summary>
            internal FlockingState()
                : base(EnemyAreaEffectState.Flocking, EnemyAreaEffectIntent.Flocking)
            {
            }
        }

        private sealed class SensingState : AreaState
        {
            /// <summary>创建等待目标范围判断的状态。</summary>
            internal SensingState()
                : base(EnemyAreaEffectState.Sensing, EnemyAreaEffectIntent.Flocking)
            {
            }
        }

        private sealed class ApproachingState : AreaState
        {
            /// <summary>创建以范围精英速度接近目标的状态。</summary>
            internal ApproachingState()
                : base(EnemyAreaEffectState.Approaching, EnemyAreaEffectIntent.Approach)
            {
            }
        }

        private sealed class CastingState : AreaState
        {
            /// <summary>创建保持姿态并允许范围效果服务工作的状态。</summary>
            internal CastingState()
                : base(EnemyAreaEffectState.Casting, EnemyAreaEffectIntent.Hold)
            {
            }
        }

        private sealed class CooldownState : AreaState
        {
            /// <summary>创建暂停施法并交还群游控制的状态。</summary>
            internal CooldownState()
                : base(EnemyAreaEffectState.Cooldown, EnemyAreaEffectIntent.Flocking)
            {
            }
        }

        private sealed class RetreatingState : AreaState
        {
            /// <summary>创建沿玩家反方向移动的状态。</summary>
            internal RetreatingState()
                : base(EnemyAreaEffectState.Retreating, EnemyAreaEffectIntent.Retreat)
            {
            }
        }
    }
}
