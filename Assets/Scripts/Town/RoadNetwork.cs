using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoadNetwork
{
    private sealed class Node { public Vector2 Position; public readonly List<Edge> Edges = new List<Edge>(); }
    private readonly struct Edge { public readonly string To; public readonly float Cost; public Edge(string to, float cost) { To = to; Cost = cost; } }
    private readonly Dictionary<string, Node> nodes = new Dictionary<string, Node>();

    public RoadNetwork(TownLayoutData layout)
    {
        foreach (TownRoad road in layout.roads)
        {
            Vector2 start = road.points[0].Vector;
            Vector2 end = road.points[road.points.Length - 1].Vector;
            if (!nodes.ContainsKey(road.from)) nodes[road.from] = new Node { Position = start };
            if (!nodes.ContainsKey(road.to)) nodes[road.to] = new Node { Position = end };
            nodes[road.from].Edges.Add(new Edge(road.to, road.length));
            nodes[road.to].Edges.Add(new Edge(road.from, road.length));
        }
    }

    public float ShortestDistance(string from, string to)
    {
        if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to)) return float.PositiveInfinity;
        var distance = new Dictionary<string, float>();
        var pending = new List<string> { from };
        distance[from] = 0f;
        while (pending.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < pending.Count; i++) if (distance[pending[i]] < distance[pending[bestIndex]]) bestIndex = i;
            string current = pending[bestIndex];
            pending.RemoveAt(bestIndex);
            if (current == to) return distance[current];
            foreach (Edge edge in nodes[current].Edges)
            {
                float next = distance[current] + edge.Cost;
                if (distance.TryGetValue(edge.To, out float old) && old <= next) continue;
                distance[edge.To] = next;
                if (!pending.Contains(edge.To)) pending.Add(edge.To);
            }
        }
        return float.PositiveInfinity;
    }
}
