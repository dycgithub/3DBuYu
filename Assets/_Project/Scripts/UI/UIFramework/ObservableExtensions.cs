using System;
using R3;
using UnityEngine;

namespace _Project.UI.Framework
{
    public static class ObservableExtensions
    {
        public static Observable<string> AsString<T>(this Observable<T> source, string format = "{0}")
        {
            return source.Select(x => string.Format(format, x));
        }

        public static Observable<float> Normalize(this Observable<int> source, Observable<int> maxSource)
        {
            return source.CombineLatest(maxSource, (value, max) => max == 0 ? 0f : (float)value / max);
        }

        public static Observable<float> Normalize(this Observable<float> source, Observable<float> maxSource)
        {
            return source.CombineLatest(maxSource, (value, max) => Mathf.Approximately(max, 0f) ? 0f : value / max);
        }

        public static Observable<string> ToRatioText(this Observable<int> source, Observable<int> maxSource,
            string format = "{0} / {1}")
        {
            return source.CombineLatest(maxSource, (value, max) => string.Format(format, value, max));
        }

        public static Observable<string> ToRatioText(this Observable<float> source, Observable<float> maxSource,
            string format = "{0:F0} / {1:F0}")
        {
            return source.CombineLatest(maxSource, (value, max) => string.Format(format, value, max));
        }
    }
}
