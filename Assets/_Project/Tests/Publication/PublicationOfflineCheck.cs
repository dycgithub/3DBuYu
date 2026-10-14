#if PUBLICATION_CHECKS && !UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _Project.Tests.Publication
{
    /// <summary>专用验证进程在任何场景初始化前拒绝 Addressables HTTP 请求，不改变系统网络。</summary>
    internal static class PublicationOfflineCheck
    {
        /// <summary>允许本地资源请求，远程请求直接失败，以验证语言资源可完全离线加载。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            if (!Environment.GetCommandLineArgs().Contains("-publication-check"))
            {
                return;
            }

            Addressables.WebRequestOverride = request =>
            {
                if (request.url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    request.url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Offline publication check blocked remote resource: " + request.url);
                }
            };
            Debug.Log("[PublicationOfflineCheck] Addressables HTTP/HTTPS requests disabled for this verification process.");
        }
    }
}
#endif
