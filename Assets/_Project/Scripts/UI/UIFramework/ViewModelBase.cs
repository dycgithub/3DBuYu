using R3;

namespace _Project.UI.Framework
{
    public abstract class ViewModelBase : IViewModel
    {
        protected readonly CompositeDisposable Disposables = new();

        public virtual void Dispose()
        {
            Disposables.Dispose();
        }
    }
}
