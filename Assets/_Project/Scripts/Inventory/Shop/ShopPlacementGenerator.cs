using System;
using System.Collections.Generic;
using UnityEngine;
using UnityRandom = UnityEngine.Random;

namespace InventorySystem.Shop
{
    internal sealed class ShopPlacementGenerator
    {
        private static readonly Dir[] Directions =
        {
            Dir.Down,
            Dir.Left,
            Dir.Up,
            Dir.Right,
        };

        private readonly struct Candidate
        {
            public readonly ItemDefinition Definition;
            public readonly Dir Direction;
            public readonly int Width;
            public readonly int Height;
            public readonly int Area;

            public Candidate(ItemDefinition definition, Dir direction, int width, int height)
            {
                Definition = definition;
                Direction = direction;
                Width = width;
                Height = height;
                Area = width * height;
            }
        }

        private sealed class AreaGroup
        {
            public readonly float Weight;
            public readonly List<Candidate> Candidates = new();

            public AreaGroup(float weight)
            {
                Weight = weight;
            }
        }

        internal readonly struct Placement
        {
            public readonly ItemDefinition Definition;
            public readonly Dir Direction;
            public readonly int Column;
            public readonly int Row;

            public Placement(ItemDefinition definition, Dir direction, int column, int row)
            {
                Definition = definition;
                Direction = direction;
                Column = column;
                Row = row;
            }
        }

        public IReadOnlyList<Placement> Generate(
            IReadOnlyList<ItemDefinition> definitions,
            ItemShapeSet shapeSet,
            int width,
            int height,
            Func<int, float> areaWeightResolver,
            int maxAttempts)
        {
            var placements = new List<Placement>();
            if (definitions == null || definitions.Count == 0 ||
                width <= 0 || height <= 0 || areaWeightResolver == null)
                return placements;

            var candidates = BuildCandidates(definitions, shapeSet);
            if (candidates.Count == 0) return placements;

            var board = new ShopPlacementBoard(width, height);
            int attempts = Mathf.Max(1, maxAttempts);

            for (int attempt = 0; attempt < attempts && !board.IsFull; attempt++)
            {
                var groups = BuildFeasibleGroups(candidates, board, areaWeightResolver);
                if (groups.Count == 0) break;

                var group = PickGroup(groups);
                var candidate = group.Candidates[UnityRandom.Range(0, group.Candidates.Count)];
                if (!board.TryReserve(candidate.Width, candidate.Height, out Vector2Int position))
                    continue;

                placements.Add(new Placement(
                    candidate.Definition,
                    candidate.Direction,
                    position.x,
                    position.y));
            }

            return placements;
        }

        private static List<Candidate> BuildCandidates(
            IReadOnlyList<ItemDefinition> definitions,
            ItemShapeSet shapeSet)
        {
            var candidates = new List<Candidate>(definitions.Count * Directions.Length);
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null) continue;

                for (int j = 0; j < Directions.Length; j++)
                {
                    var item = new ItemVM(definition, shapeSet);
                    item.SetDirection(Directions[j]);
                    if (item.Width <= 0 || item.Height <= 0) continue;

                    candidates.Add(new Candidate(
                        definition,
                        Directions[j],
                        item.Width,
                        item.Height));
                }
            }

            return candidates;
        }

        private static List<AreaGroup> BuildFeasibleGroups(
            List<Candidate> candidates,
            ShopPlacementBoard board,
            Func<int, float> areaWeightResolver)
        {
            var groupsByArea = new Dictionary<int, AreaGroup>();
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!board.HasAvailablePosition(candidate.Width, candidate.Height))
                    continue;

                float weight = NormalizeWeight(areaWeightResolver(candidate.Area));
                if (weight <= 0f) continue;

                if (!groupsByArea.TryGetValue(candidate.Area, out var group))
                {
                    group = new AreaGroup(weight);
                    groupsByArea.Add(candidate.Area, group);
                }

                group.Candidates.Add(candidate);
            }

            return new List<AreaGroup>(groupsByArea.Values);
        }

        private static AreaGroup PickGroup(List<AreaGroup> groups)
        {
            float totalWeight = 0f;
            for (int i = 0; i < groups.Count; i++)
                totalWeight += groups[i].Weight;

            float roll = UnityRandom.value * totalWeight;
            for (int i = 0; i < groups.Count; i++)
            {
                roll -= groups[i].Weight;
                if (roll < 0f) return groups[i];
            }

            return groups[groups.Count - 1];
        }

        private static float NormalizeWeight(float weight)
        {
            if (float.IsNaN(weight) || float.IsInfinity(weight)) return 0f;
            return Mathf.Max(0f, weight);
        }
    }

    internal sealed class ShopPlacementBoard
    {
        private readonly bool[,] _reserved;
        private int _usedCellCount;

        public int Width { get; }
        public int Height { get; }
        public bool IsFull => _usedCellCount >= Width * Height;

        public ShopPlacementBoard(int width, int height)
        {
            Width = width;
            Height = height;
            _reserved = new bool[width, height];
        }

        public bool HasAvailablePosition(int width, int height)
        {
            for (int row = 0; row <= Height - height; row++)
            {
                for (int column = 0; column <= Width - width; column++)
                {
                    if (CanFit(column, row, width, height)) return true;
                }
            }

            return false;
        }

        public bool TryReserve(int width, int height, out Vector2Int position)
        {
            position = default;
            int matchingPositionCount = 0;

            // Reservoir sampling keeps every valid anchor equally likely without
            // allocating a temporary list on every placement attempt.
            for (int row = 0; row <= Height - height; row++)
            {
                for (int column = 0; column <= Width - width; column++)
                {
                    if (!CanFit(column, row, width, height)) continue;

                    matchingPositionCount++;
                    if (UnityRandom.Range(0, matchingPositionCount) == 0)
                        position = new Vector2Int(column, row);
                }
            }

            if (matchingPositionCount == 0) return false;

            for (int row = position.y; row < position.y + height; row++)
            {
                for (int column = position.x; column < position.x + width; column++)
                {
                    _reserved[column, row] = true;
                    _usedCellCount++;
                }
            }

            return true;
        }

        private bool CanFit(int column, int row, int width, int height)
        {
            if (column < 0 || row < 0 || width <= 0 || height <= 0)
                return false;
            if (column + width > Width || row + height > Height)
                return false;

            for (int y = row; y < row + height; y++)
            {
                for (int x = column; x < column + width; x++)
                {
                    if (_reserved[x, y]) return false;
                }
            }

            return true;
        }
    }
}
