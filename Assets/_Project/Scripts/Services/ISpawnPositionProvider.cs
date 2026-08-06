using UnityEngine;

namespace Services
{
    public interface ISpawnPositionProvider
    {
        Vector3 GetSpawnPosition();
    }
}