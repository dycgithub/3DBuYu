using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace _Project.Localization.View
{
    /// <summary>通过字体资产表切换预置 TMP 文本字体，不创建界面或维护独立语言状态。</summary>
    public sealed class LocalizedFontEvent : LocalizedAssetEvent<TMP_FontAsset, LocalizedTmpFont, LocalizedFontEvent.FontChangedEvent>
    {
        /// <summary>序列化字体更新事件，供编辑器绑定 TMP 的公开 font 属性。</summary>
        [Serializable]
        public sealed class FontChangedEvent : UnityEvent<TMP_FontAsset> { }
    }
}
