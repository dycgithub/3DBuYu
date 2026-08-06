using System;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.UI.Framework
{
    public static class BindingExtensions
    {
        #region Label

        public static IDisposable BindText(this Label label, Observable<string> source)
        {
            return source.Subscribe(x => label.text = x).AddTo(label);
        }

        public static IDisposable BindText(this Label label, Observable<int> source, string format = "{0}")
        {
            return source.Subscribe(x => label.text = string.Format(format, x)).AddTo(label);
        }

        public static IDisposable BindText(this Label label, Observable<float> source, string format = "{0:F1}")
        {
            return source.Subscribe(x => label.text = string.Format(format, x)).AddTo(label);
        }

        #endregion

        #region Button

        public static IDisposable BindClick(this Button button, Action onClick)
        {
            button.clicked += onClick;
            return Disposable.Create(() => button.clicked -= onClick).AddTo(button);
        }

        public static IDisposable BindClick(this Button button, IReactiveCommand command)
        {
            button.clicked += command.Execute;
            var canExecuteSubscription = command.CanExecute
                .Subscribe(can => button.SetEnabled(can))
                .AddTo(button);

            return Disposable.Create(() =>
            {
                canExecuteSubscription.Dispose();
                button.clicked -= command.Execute;
            }).AddTo(button);
        }

        public static IDisposable BindClick<T>(this Button button, IReactiveCommand<T> command, T parameter)
        {
            void OnClick() => command.Execute(parameter);
            button.clicked += OnClick;

            var canExecuteSubscription = command.CanExecute
                .Subscribe(can => button.SetEnabled(can))
                .AddTo(button);

            return Disposable.Create(() =>
            {
                canExecuteSubscription.Dispose();
                button.clicked -= OnClick;
            }).AddTo(button);
        }

        #endregion

        #region Visibility

        public static IDisposable BindVisible(this VisualElement element, Observable<bool> source)
        {
            return source.Subscribe(x =>
                element.style.display = x ? DisplayStyle.Flex : DisplayStyle.None).AddTo(element);
        }

        public static IDisposable BindClass(this VisualElement element, Observable<bool> source, string className)
        {
            return source.Subscribe(x =>
            {
                if (x)
                {
                    element.AddToClassList(className);
                }
                else
                {
                    element.RemoveFromClassList(className);
                }
            }).AddTo(element);
        }

        #endregion

        #region ProgressBar

        public static IDisposable BindProgress(this ProgressBar progressBar, Observable<float> source)
        {
            return source.Subscribe(x => progressBar.value = x).AddTo(progressBar);
        }

        public static IDisposable BindProgress(this ProgressBar progressBar, Observable<float> valueSource,
            Observable<string> titleSource)
        {
            var valueSubscription = valueSource.Subscribe(x => progressBar.value = x).AddTo(progressBar);
            var titleSubscription = titleSource.Subscribe(x => progressBar.title = x).AddTo(progressBar);

            return new CompositeDisposable(valueSubscription, titleSubscription);
        }

        #endregion

        #region Slider

        public static IDisposable BindValue(this Slider slider, Observable<float> source)
        {
            return source.Subscribe(x => slider.value = x).AddTo(slider);
        }

        public static IDisposable BindTwoWayValue(this Slider slider, ReactiveProperty<float> source)
        {
            return new SliderTwoWayBinding(slider, source);
        }

        #endregion

        #region Toggle

        public static IDisposable BindValue(this Toggle toggle, Observable<bool> source)
        {
            return source.Subscribe(x => toggle.value = x).AddTo(toggle);
        }

        public static IDisposable BindTwoWayValue(this Toggle toggle, ReactiveProperty<bool> source)
        {
            return new ToggleTwoWayBinding(toggle, source);
        }

        #endregion

        #region TextField

        public static IDisposable BindValue(this TextField textField, Observable<string> source)
        {
            return source.Subscribe(x => textField.value = x).AddTo(textField);
        }

        public static IDisposable BindTwoWayValue(this TextField textField, ReactiveProperty<string> source)
        {
            return new TextFieldTwoWayBinding(textField, source);
        }

        #endregion

        #region Image

        public static IDisposable BindSprite(this UnityEngine.UIElements.Image image, Observable<Sprite> source)
        {
            return source.Subscribe(x => image.sprite = x).AddTo(image);
        }

        public static IDisposable BindTexture(this UnityEngine.UIElements.Image image, Observable<Texture2D> source)
        {
            return source.Subscribe(x => image.image = x).AddTo(image);
        }

        #endregion

        #region Two-Way Binding Helpers

        private class SliderTwoWayBinding : IDisposable
        {
            private readonly Slider _slider;
            private readonly ReactiveProperty<float> _source;
            private readonly IDisposable _subscription;
            private bool _suppress;

            public SliderTwoWayBinding(Slider slider, ReactiveProperty<float> source)
            {
                _slider = slider;
                _source = source;

                _subscription = source.Subscribe(x =>
                {
                    if (!Mathf.Approximately(_slider.value, x))
                    {
                        _suppress = true;
                        _slider.value = x;
                        _suppress = false;
                    }
                }).AddTo(slider);

                slider.RegisterValueChangedCallback(OnValueChanged);
            }

            private void OnValueChanged(ChangeEvent<float> evt)
            {
                if (!_suppress && !Mathf.Approximately(_source.Value, evt.newValue))
                {
                    _source.Value = evt.newValue;
                }
            }

            public void Dispose()
            {
                _subscription.Dispose();
                _slider.UnregisterValueChangedCallback(OnValueChanged);
            }
        }

        private class ToggleTwoWayBinding : IDisposable
        {
            private readonly Toggle _toggle;
            private readonly ReactiveProperty<bool> _source;
            private readonly IDisposable _subscription;
            private bool _suppress;

            public ToggleTwoWayBinding(Toggle toggle, ReactiveProperty<bool> source)
            {
                _toggle = toggle;
                _source = source;

                _subscription = source.Subscribe(x =>
                {
                    if (_toggle.value != x)
                    {
                        _suppress = true;
                        _toggle.value = x;
                        _suppress = false;
                    }
                }).AddTo(toggle);

                toggle.RegisterValueChangedCallback(OnValueChanged);
            }

            private void OnValueChanged(ChangeEvent<bool> evt)
            {
                if (!_suppress && _source.Value != evt.newValue)
                {
                    _source.Value = evt.newValue;
                }
            }

            public void Dispose()
            {
                _subscription.Dispose();
                _toggle.UnregisterValueChangedCallback(OnValueChanged);
            }
        }

        private class TextFieldTwoWayBinding : IDisposable
        {
            private readonly TextField _textField;
            private readonly ReactiveProperty<string> _source;
            private readonly IDisposable _subscription;
            private bool _suppress;

            public TextFieldTwoWayBinding(TextField textField, ReactiveProperty<string> source)
            {
                _textField = textField;
                _source = source;

                _subscription = source.Subscribe(x =>
                {
                    if (_textField.value != x)
                    {
                        _suppress = true;
                        _textField.value = x;
                        _suppress = false;
                    }
                }).AddTo(textField);

                textField.RegisterValueChangedCallback(OnValueChanged);
            }

            private void OnValueChanged(ChangeEvent<string> evt)
            {
                if (!_suppress && _source.Value != evt.newValue)
                {
                    _source.Value = evt.newValue;
                }
            }

            public void Dispose()
            {
                _subscription.Dispose();
                _textField.UnregisterValueChangedCallback(OnValueChanged);
            }
        }

        #endregion
    }
}
