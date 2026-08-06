using System;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace _Project.UI.Framework
{
    public abstract class UIDocumentViewBase<TViewModel> : MonoBehaviour, IView<TViewModel>
        where TViewModel : class, IViewModel
    {
        [SerializeField] private UIDocument _uiDocument;

        private CompositeDisposable _viewDisposables = new();
        private TViewModel _viewModel;

        public bool IsBound => _viewModel != null;
        protected TViewModel ViewModel => _viewModel;
        protected VisualElement Root { get; private set; }
        protected CompositeDisposable ViewDisposables => _viewDisposables;

        protected virtual bool DisposeViewModelOnUnbind => false;

        [Inject]
        public virtual void Bind(TViewModel viewModel)
        {
            if (IsBound)
            {
                Unbind();
            }

            Root = ResolveRoot();
            if (Root == null)
            {
                Debug.LogError($"[{GetType().Name}] UIDocument or rootVisualElement is missing.", this);
                return;
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
            Root = null;
        }

        protected abstract void OnBind();
        protected virtual void OnUnbind() { }

        protected VisualElement ResolveRoot()
        {
            if (_uiDocument != null)
            {
                return _uiDocument.rootVisualElement;
            }

            var document = GetComponent<UIDocument>();
            return document != null ? document.rootVisualElement : null;
        }

        protected void OnDestroy()
        {
            Unbind();
        }
    }
}
