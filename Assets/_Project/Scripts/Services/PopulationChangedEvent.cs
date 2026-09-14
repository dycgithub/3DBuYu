namespace Services
{
    public readonly struct PopulationChangedEvent
    {
        public readonly int CurrentPopulationNumber;
        public readonly int TotalPopulationCount;

        public PopulationChangedEvent(int currentPopulationNumber, int totalPopulationCount)
        {
            CurrentPopulationNumber = currentPopulationNumber;
            TotalPopulationCount = totalPopulationCount;
        }
    }
}
