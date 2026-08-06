using System;
using R3;
using UnityEngine.UIElements;

namespace _Project.UI.Framework
{
    public abstract class UIViewBase<TViewModel> : IView<TViewModel>
        where TViewModel : class, IViewModel
    {
        private CompositeDisposable _viewDisposables = new();
        private TViewModel _viewModel;

        public bool IsBound => _viewModel != null;
        protected TViewModel ViewModel => _viewModel;
        protected VisualElement Root { get; }
        protected CompositeDisposable ViewDisposables => _viewDisposables;

        protected virtual bool DisposeViewModelOnUnbind => false;

        protected UIViewBase(VisualElement root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

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
    }
}
