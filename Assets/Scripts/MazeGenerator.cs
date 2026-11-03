using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MazeGenerator : MonoBehaviour
{
    public int cells = 23;
    public int corridor = 5;
    public int wall = 3;
    public int seed;
    public float tileSize = 0.5f;
    public List<Vector2Int> junctions = new List<Vector2Int>();
    public bool[,] floor;
    public Vector2 spawn;
    public int Size => cells * (corridor + wall) + wall;
    public int Step => corridor + wall;
    public int LoopCount { get; private set; }
    public Tilemap map;
    System.Random random;
    int[,] distance;
    Tile[] floorTiles;
    Tile[] wallTiles;
    Material terrainMaterial;
    readonly Vector2Int[] directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

    public void Generate(int newSeed, bool draw = true)
    {
        seed = newSeed;
        random = new System.Random(seed);
        floor = new bool[Size, Size];
        junctions.Clear();
        LoopCount = 0;
        bool[,] visited = new bool[cells, cells];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int start = new Vector2Int(1, 1);
        visited[1, 1] = true;
        stack.Push(start);
        Carve(Center(start), Center(start));
        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> choices = new List<Vector2Int>();
            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;
                if (CellInside(next) && !visited[next.x, next.y])
                    choices.Add(next);
            }
            if (choices.Count == 0)
            {
                stack.Pop();
                continue;
            }
            Vector2Int chosen = choices[random.Next(choices.Count)];
            Carve(Center(current), Center(chosen));
            visited[chosen.x, chosen.y] = true;
            stack.Push(chosen);
        }
        for (int x = 0; x < cells; x++)
        for (int y = 0; y < cells; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            foreach (Vector2Int dir in new[] { Vector2Int.right, Vector2Int.up })
            {
                Vector2Int next = cell + dir;
                if (!CellInside(next)) continue;
                Vector2Int middle = (Center(cell) + Center(next)) / 2;
                if (!floor[middle.x, middle.y] && random.NextDouble() < 0.14)
                {
                    Carve(Center(cell), Center(next));
                    LoopCount++;
                }
            }
            junctions.Add(Center(cell));
        }
        spawn = World(Center(start));
        if (draw) Draw();
    }

    bool CellInside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < cells && p.y < cells;
    public Vector2Int Center(Vector2Int p) => p * Step + new Vector2Int(wall + corridor / 2, wall + corridor / 2);
    public Vector2 World(Vector2Int p) => ((Vector2)p + Vector2.one * 0.5f) * tileSize;
    public Vector2Int TileAt(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / tileSize), Mathf.FloorToInt(p.y / tileSize));
    public bool IsFloor(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Size && p.y < Size && floor[p.x, p.y];

    void Carve(Vector2Int a, Vector2Int b)
    {
        int half = corridor / 2;
        for (int x = Mathf.Min(a.x, b.x) - half; x <= Mathf.Max(a.x, b.x) + half; x++)
        for (int y = Mathf.Min(a.y, b.y) - half; y <= Mathf.Max(a.y, b.y) + half; y++)
            floor[x, y] = true;
    }

    public bool CanStand(Vector2 p, float radius = 0.28f)
    {
        return IsFloor(TileAt(p + new Vector2(radius, radius))) && IsFloor(TileAt(p + new Vector2(-radius, radius)))
            && IsFloor(TileAt(p + new Vector2(radius, -radius))) && IsFloor(TileAt(p + new Vector2(-radius, -radius)));
    }

    public Vector2 Move(Vector2 position, Vector2 delta, float radius = 0.28f)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.12f));
        Vector2 step = delta / steps;
        for (int i = 0; i < steps; i++)
        {
            if (CanStand(position + new Vector2(step.x, 0), radius)) position.x += step.x;
            if (CanStand(position + new Vector2(0, step.y), radius)) position.y += step.y;
        }
        return position;
    }

    public bool ClearLine(Vector2 a, Vector2 b)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.12f);
        for (int i = 0; i <= steps; i++)
            if (!IsFloor(TileAt(Vector2.Lerp(a, b, i / (float)Mathf.Max(1, steps))))) return false;
        return true;
    }

    public void UpdateDistances(Vector2 target)
    {
        distance = new int[Size, Size];
        Vector2Int start = TileAt(target);
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        distance[start.x, start.y] = 1;
        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            if (distance[p.x, p.y] > 45) continue;
            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = p + dir;
                if (!IsFloor(next) || distance[next.x, next.y] != 0) continue;
                distance[next.x, next.y] = distance[p.x, p.y] + 1;
                queue.Enqueue(next);
            }
        }
    }

    public Vector2 ChaseDirection(Vector2 p, Vector2 target)
    {
        if (ClearLine(p, target)) return (target - p).normalized;
        if (distance == null) return Vector2.zero;
        Vector2Int cell = TileAt(p);
        int best = IsFloor(cell) && distance[cell.x, cell.y] > 0 ? distance[cell.x, cell.y] : int.MaxValue;
        Vector2 waypoint = p;
        foreach (Vector2Int dir in directions)
        {
            Vector2Int next = cell + dir;
            if (IsFloor(next) && distance[next.x, next.y] > 0 && distance[next.x, next.y] < best)
            {
                best = distance[next.x, next.y];
                waypoint = World(next);
            }
        }
        return (waypoint - p).normalized;
    }

    void Draw()
    {
        if (map == null)
        {
            GameObject grid = new GameObject("Maze", typeof(Grid));
            grid.transform.SetParent(transform, false);
            grid.GetComponent<Grid>().cellSize = new Vector3(tileSize, tileSize, 1);
            GameObject layer = new GameObject("Stone", typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(grid.transform, false);
            map = layer.GetComponent<Tilemap>();
            TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = -10;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            terrainMaterial = new Material(Resources.Load<Shader>("Shaders/MazeTerrain"));
            renderer.sharedMaterial = terrainMaterial;
            floorTiles = LoadTiles("rect_gray", 4);
            wallTiles = LoadTiles("brick_dark", 4);
        }
        map.ClearAllTiles();
        BoundsInt bounds = new BoundsInt(0, 0, 0, Size, Size, 1);
        TileBase[] tiles = new TileBase[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int choice = random.Next(4);
            tiles[x + y * Size] = floor[x, y] ? floorTiles[choice] : wallTiles[choice];
        }
        map.SetTilesBlock(bounds, tiles);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            Vector3Int p = new Vector3Int(x, y, 0);
            map.SetTileFlags(p, TileFlags.None);
            float shade = floor[x, y] ? 0.87f : 0.39f;
            if (floor[x, y] && !IsFloor(new Vector2Int(x, y + 1))) shade *= 0.65f;
            else if (floor[x, y] && (!IsFloor(new Vector2Int(x - 1, y)) || !IsFloor(new Vector2Int(x + 1, y)))) shade *= 0.83f;
            map.SetColor(p, new Color(shade, shade * 0.96f, shade * 0.91f));
        }
    }

    Tile[] LoadTiles(string prefix, int count)
    {
        Tile[] result = new Tile[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ScriptableObject.CreateInstance<Tile>();
            result[i].sprite = Resources.Load<Sprite>("Art/Tiles/" + prefix + i);
            result[i].colliderType = Tile.ColliderType.None;
        }
        return result;
    }

    void OnDestroy()
    {
        if (terrainMaterial != null) Destroy(terrainMaterial);
    }

}
