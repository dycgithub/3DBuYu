using R3;
using UnityEngine;
using VContainer;

namespace _Project.UI.Framework
{
    public abstract class UguiViewBase<TViewModel> : MonoBehaviour, IView<TViewModel>
        where TViewModel : class, IViewModel
    {
        private CompositeDisposable _viewDisposables = new();
        private TViewModel _viewModel;

        public bool IsBound => _viewModel != null;
        protected TViewModel ViewModel => _viewModel;
        protected CompositeDisposable ViewDisposables => _viewDisposables;

        protected virtual bool DisposeViewModelOnUnbind => false;

        [Inject]
        public virtual void Bind(TViewModel viewModel)
        {
            if (IsBound)
            {
                Unbind();
            }

            _viewModel = viewModel;
            OnBind();
        }

        public virtual void Unbind()
        {
            if (!IsBound)
            {
                return;
            }

            OnUnbind();
            _viewDisposables.Dispose();
            _viewDisposables = new CompositeDisposable();

            if (DisposeViewModelOnUnbind)
            {
                _viewModel?.Dispose();
            }

            _viewModel = null;
        }

        protected abstract void OnBind();
        protected virtual void OnUnbind() { }

        protected void OnDestroy()
        {
            Unbind();
        }
    }
}
