namespace ShootingSystem.Networking
{
    public class LocalBulletNetworkBridge
    {
        private IBulletSpawner _spawner;

        public LocalBulletNetworkBridge(IBulletSpawner spawner)
        {
            _spawner = spawner;
        }

        public void OnSpawnReceived(SpawnRequest request)
        {
            _spawner.Spawn(request);
        }
    }
}
