namespace _Project.UI.Framework
{
    public interface IView<in TViewModel> where TViewModel : IViewModel
    {
        bool IsBound { get; }
        void Bind(TViewModel viewModel);
        void Unbind();
    }
}
