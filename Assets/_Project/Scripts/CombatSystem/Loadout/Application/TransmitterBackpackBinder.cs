using System.Collections.Generic;
using Play;
using UnityEngine;
using VContainer;

namespace CombatSystem
{
    /// <summary>
    /// 校验发射器背包是否通过稳定 PortId 绑定，避免依赖中心对象或物体名称顺序。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TransmitterBackpackBinder : MonoBehaviour
    {
        [Inject] private CentralCore _centralCore;

        private void Start()
        {
            ValidateBindings();
        }

        private void ValidateBindings()
        {
            HashSet<string> realPortIds = CollectRealPortIds();
            var portIds = new HashSet<string>();
            IReadOnlyList<GridView> grids = GridView.ActiveGrids;
            for (int index = 0; index < grids.Count; index++)
            {
                GridView grid = grids[index];
                if (grid == null || grid.GridType != GridType.TransmitterBackpack)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(grid.PortId))
                {
                    Debug.LogError(
                        $"[TransmitterBackpackBinder] {grid.name}: 未配置 PortId。",
                        grid);
                    continue;
                }

                if (!realPortIds.Contains(grid.PortId))
                {
                    Debug.LogError(
                        $"[TransmitterBackpackBinder] {grid.name}: PortId '{grid.PortId}' 不是当前中心的真实端口。",
                        grid);
                }

                if (!portIds.Add(grid.PortId))
                {
                    Debug.LogError(
                        $"[TransmitterBackpackBinder] PortId '{grid.PortId}' 被多个发射器背包使用。",
                        grid);
                }
            }
        }

        private HashSet<string> CollectRealPortIds()
        {
            var portIds = new HashSet<string>();
            CentralSO centralSo = _centralCore?.CentralSoConfig;
            if (centralSo == null || centralSo.Transmitters == null)
            {
                Debug.LogError(
                    $"[TransmitterBackpackBinder] {name}: 无法读取当前中心的真实端口配置。",
                    this);
                return portIds;
            }

            int actualPortIndex = 0;
            for (int index = 0; index < centralSo.Transmitters.Length; index++)
            {
                TransmitterSO transmitter = centralSo.Transmitters[index];
                if (transmitter == null)
                {
                    continue;
                }

                string portId = string.IsNullOrWhiteSpace(transmitter.portId)
                    ? $"port-{actualPortIndex}"
                    : transmitter.portId;
                if (!portIds.Add(portId))
                {
                    Debug.LogError(
                        $"[TransmitterBackpackBinder] 中心端口 PortId '{portId}' 重复。",
                        centralSo);
                }

                actualPortIndex++;
            }

            return portIds;
        }
    }
}
