using R3;

namespace _Project.UI.Framework
{
    public class ReactiveCommand : IReactiveCommand
    {
        private readonly Subject<Unit> _execute = new();

        public ReactiveProperty<bool> CanExecute { get; } = new(true);
        public Observable<Unit> OnExecute => _execute;

        public void Execute()
        {
            if (CanExecute.Value)
            {
                _execute.OnNext(Unit.Default);
            }
        }

        public void Dispose()
        {
            _execute.Dispose();
            CanExecute.Dispose();
        }
    }

    public class ReactiveCommand<T> : IReactiveCommand<T>
    {
        private readonly Subject<T> _execute = new();

        public ReactiveProperty<bool> CanExecute { get; } = new(true);
        public Observable<T> OnExecute => _execute;

        public void Execute()
        {
            Execute(default);
        }

        public void Execute(T parameter)
        {
            if (CanExecute.Value)
            {
                _execute.OnNext(parameter);
            }
        }

        public void Dispose()
        {
            _execute.Dispose();
            CanExecute.Dispose();
        }
    }
}
