using System;
using System.Collections.Generic;

// Reserve on pickup, remove only on successful delivery. Each round visits every home once.
public sealed class DeliveryDeck
{
    private readonly Random random;
    private readonly int houseCount;
    private readonly List<int> remaining = new List<int>();
    private int pending = -1;
    private int lastDelivered = -1;
    private readonly int tutorialHouse;
    private readonly float[] weights;

    public int Remaining => remaining.Count;
    public int Round { get; private set; }

    public DeliveryDeck(int count, int seed, int tutorialHouse = -1)
        : this(count, seed, tutorialHouse, null)
    {
    }

    public DeliveryDeck(int count, int seed, int tutorialHouse, float[] destinationWeights)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        houseCount = count;
        if (tutorialHouse < -1 || tutorialHouse >= count) throw new ArgumentOutOfRangeException(nameof(tutorialHouse));
        this.tutorialHouse = tutorialHouse;
        if (destinationWeights != null && destinationWeights.Length != count) throw new ArgumentException("Jumlah bobot harus sama dengan jumlah rumah.", nameof(destinationWeights));
        weights = destinationWeights;
        random = new Random(seed);
        Refill(true);
    }

    public int Reserve()
    {
        if (pending >= 0) return pending;
        if (remaining.Count == 0) Refill(false);
        pending = remaining[remaining.Count - 1];
        return pending;
    }

    public bool Complete(int house)
    {
        if (pending < 0 || pending != house) return false;
        remaining.Remove(pending);
        lastDelivered = pending;
        pending = -1;
        return true;
    }

    private void Refill(bool tutorial)
    {
        Round++;
        var pool = new List<int>();
        for (int i = 0; i < houseCount; i++) pool.Add(i);
        while (pool.Count > 0)
        {
            double total = 0;
            foreach (int index in pool) total += weights == null ? 1 : Math.Max(0.00001f, weights[index]);
            double ticket = random.NextDouble() * total;
            int chosen = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                ticket -= weights == null ? 1 : Math.Max(0.00001f, weights[pool[i]]);
                if (ticket <= 0) { chosen = i; break; }
            }
            // Reserve mengambil elemen terakhir; sisipkan di depan agar undian pertama keluar lebih dulu.
            remaining.Insert(0, pool[chosen]);
            pool.RemoveAt(chosen);
        }
        int tail = remaining.Count - 1;
        if (tutorial && tutorialHouse >= 0)
        {
            int first = remaining.IndexOf(tutorialHouse);
            (remaining[first], remaining[tail]) = (remaining[tail], remaining[first]);
        }
        else if (houseCount > 1 && remaining[tail] == lastDelivered)
        {
            int other = random.Next(tail);
            (remaining[other], remaining[tail]) = (remaining[tail], remaining[other]);
        }
    }
}
