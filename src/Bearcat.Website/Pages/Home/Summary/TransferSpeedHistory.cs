namespace Bearcat.Website.Pages.Home.Summary;

public sealed class TransferSpeedHistory(int capacity)
{
    private readonly Queue<double> samples = new();

    public int Capacity => capacity;

    public IReadOnlyList<double> Samples => samples.ToList();

    public void Add(double bytesPerSecond)
    {
        samples.Enqueue(bytesPerSecond);

        while (samples.Count > capacity)
        {
            samples.Dequeue();
        }
    }
}
