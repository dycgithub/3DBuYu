using R3;

namespace _Project.UI.Framework
{
    public interface IReactiveCommand
    {
        ReactiveProperty<bool> CanExecute { get; }
        void Execute();
    }

    public interface IReactiveCommand<in T> : IReactiveCommand
    {
        void Execute(T parameter);
    }
}
