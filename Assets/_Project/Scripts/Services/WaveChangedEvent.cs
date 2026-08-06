namespace Services
{
    public readonly struct WaveChangedEvent
    {
        public readonly int CurrentWaveNumber;
        public readonly int TotalWaveCount;

        public WaveChangedEvent(int currentWaveNumber, int totalWaveCount)
        {
            CurrentWaveNumber = currentWaveNumber;
            TotalWaveCount = totalWaveCount;
        }
    }
}