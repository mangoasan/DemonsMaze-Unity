using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MazeGenerator : MonoBehaviour
{
    [Min(6)] public int cells = 16;
    [Min(5)] public int corridor = 5;
    [Min(5)] public int wall = 9;
    public int seed;
    public float tileSize = 0.5f;
    public int Size => cells * Step + wall;
    public int Step => corridor + wall;
    public List<Vector2Int> junctions = new();
    public List<MazeRoom> Rooms { get; } = new();
    [System.NonSerialized] public bool[,] floor;
    public Vector2 spawn;
    public Vector2Int Gate { get; private set; }
    public Vector2Int Throne { get; private set; }
    public Tilemap map;

    static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    readonly Dictionary<Vector2Int, string> props = new();
    readonly Dictionary<string, Tile> tiles = new();
    bool[,] solid;
    RoomKind[,] themes;
    Tilemap decorationMap;
    Material terrainMaterial;
    System.Random random;

    public void Generate(int newSeed, bool draw = true, bool citadel = false)
    {
        seed = newSeed;
        random = new System.Random(seed);
        floor = new bool[Size, Size];
        solid = new bool[Size, Size];
        themes = new RoomKind[Size, Size];
        props.Clear();
        Rooms.Clear();
        junctions.Clear();
        if (citadel) BuildCitadel();
        else BuildWorld();
        if (draw) Draw();
    }

    void BuildWorld()
    {
        Vector2Int first = Vector2Int.one;
        Vector2Int start = Center(first);
        spawn = World(start);
        bool[,] visited = new bool[cells, cells];
        Stack<Vector2Int> stack = new();
        stack.Push(first);
        visited[first.x, first.y] = true;
        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> choices = new();
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;
                if (CellInside(next) && !visited[next.x, next.y]) choices.Add(next);
            }
            if (choices.Count == 0)
            {
                stack.Pop();
                continue;
            }
            Vector2Int chosen = choices[random.Next(choices.Count)];
            Carve(Center(current), Center(chosen), corridor / 2);
            visited[chosen.x, chosen.y] = true;
            stack.Push(chosen);
        }

        for (int x = 0; x < cells; x++)
        for (int y = 0; y < cells; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            Vector2Int center = Center(cell);
            for (int i = 0; i < 2; i++)
            {
                Vector2Int next = cell + Directions[i];
                if (CellInside(next) && random.NextDouble() < 0.37)
                    Carve(center, Center(next), corridor / 2);
            }
            junctions.Add(center);
        }

        PlaceRoom(start, RoomKind.Courtyard, new Vector2Int(5, 4));
        var anchors = new Dictionary<Vector2Int, RoomKind>
        {
            [new Vector2Int(cells * 2 / 5, cells / 5)] = RoomKind.Watch,
            [new Vector2Int(cells / 3, cells * 3 / 5)] = RoomKind.Crypt,
            [new Vector2Int(cells * 7 / 10, cells * 3 / 5)] = RoomKind.Keep,
            [new Vector2Int(cells - 2, cells - 2)] = RoomKind.Keep
        };
        for (int x = 0; x < cells; x++)
        for (int y = 0; y < cells; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            if (cell == first) continue;
            if (!anchors.TryGetValue(cell, out RoomKind kind))
            {
                if (random.NextDouble() > 0.35) continue;
                kind = (RoomKind)random.Next((int)RoomKind.Courtyard, (int)RoomKind.Keep + 1);
            }
            Vector2Int half = kind switch
            {
                RoomKind.Courtyard => new Vector2Int(5, 4),
                RoomKind.Crypt => new Vector2Int(5, 5),
                RoomKind.Watch => new Vector2Int(5, 4),
                _ => new Vector2Int(4, 4)
            };
            if (random.Next(2) == 0) half = new Vector2Int(half.y, half.x);
            PlaceRoom(Center(cell), kind, half);
        }
    }

    void BuildCitadel()
    {
        Vector2Int entrance = Center(new Vector2Int(1, cells / 2));
        Throne = Center(new Vector2Int(cells - 2, cells / 2));
        Gate = (entrance + Throne) / 2;
        spawn = World(entrance + Vector2Int.left * 4);
        Carve(entrance, Throne, 2);
        PlaceRoom(entrance, RoomKind.Keep, new Vector2Int(6, 5));
        PlaceRoom(Throne, RoomKind.Crypt, new Vector2Int(9, 8));
        SetCitadelGate(false);
    }

    public void SetCitadelGate(bool open)
    {
        for (int i = -2; i <= 2; i++)
        {
            Vector2Int cell = Gate + Vector2Int.up * i;
            solid[cell.x, cell.y] = !open;
            if (open) props.Remove(cell);
            else props[cell] = "gate";
            if (decorationMap != null)
                decorationMap.SetTile((Vector3Int)cell, open ? null : GetTile("gate"));
        }
    }

    void PlaceRoom(Vector2Int center, RoomKind kind, Vector2Int half)
    {
        Rooms.Add(new MazeRoom(center, kind, half));
        for (int x = center.x - half.x; x <= center.x + half.x; x++)
        for (int y = center.y - half.y; y <= center.y + half.y; y++)
        {
            if (!Interior(x, y)) continue;
            if (kind == RoomKind.Courtyard && Mathf.Abs(x - center.x) >= half.x - 1 && Mathf.Abs(y - center.y) >= half.y - 1)
                continue;
            floor[x, y] = true;
            themes[x, y] = kind;
        }
        string obstacle = kind switch
        {
            RoomKind.Watch => "barricade",
            _ => "column"
        };
        int orientation = random.Next(2) == 0 ? 1 : -1;
        int corner = random.Next(4);
        int count = random.Next(1, 4);
        for (int i = 0; i < count; i++)
        {
            int slot = (corner + i) % 4;
            int sx = slot < 2 ? -1 : 1;
            int sy = slot % 2 == 0 ? -1 : 1;
            Vector2Int p = center + new Vector2Int(sx * (half.x - random.Next(1, 3)), sy * (half.y - 2));
            Prop(p, obstacle, true);
            if (kind == RoomKind.Watch && i == 0) Prop(p + Vector2Int.up * sy, obstacle, true);
        }
        if (kind is RoomKind.Courtyard or RoomKind.Keep)
            Prop(center + new Vector2Int(orientation * (half.x - 1), half.y - 1), "torch", false);
        if (kind == RoomKind.Crypt && half.x <= 6)
        {
            for (int side = -1; side <= 1; side += 2)
            for (int length = 2; length < half.y - 1; length++)
            {
                Vector2Int partition = new Vector2Int(side * 3, side * orientation * length);
                Prop(center + partition, "wall", true);
            }
        }
    }

    void Prop(Vector2Int p, string name, bool blocks)
    {
        if (!Interior(p.x, p.y) || !floor[p.x, p.y]) return;
        props[p] = name;
        solid[p.x, p.y] = blocks;
    }

    bool CellInside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < cells && p.y < cells;
    bool Interior(int x, int y) => x > 1 && y > 1 && x < Size - 2 && y < Size - 2;
    bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Size && p.y < Size;
    public Vector2Int Center(Vector2Int p) => p * Step + Vector2Int.one * (wall + corridor / 2);
    public Vector2 World(Vector2Int p) => ((Vector2)p + Vector2.one * 0.5f) * tileSize;
    public Vector2Int TileAt(Vector2 p) => Vector2Int.FloorToInt(p / tileSize);
    public bool IsFloor(Vector2Int p) => InBounds(p) && floor[p.x, p.y] && !solid[p.x, p.y];

    void Carve(Vector2Int a, Vector2Int b, int half)
    {
        for (int x = Mathf.Min(a.x, b.x) - half; x <= Mathf.Max(a.x, b.x) + half; x++)
        for (int y = Mathf.Min(a.y, b.y) - half; y <= Mathf.Max(a.y, b.y) + half; y++)
            if (Interior(x, y)) floor[x, y] = true;
    }

    public bool CanStand(Vector2 p, float radius = 0.28f)
    {
        Vector2 extent = Vector2.one * radius;
        Vector2Int min = TileAt(p - extent);
        Vector2Int max = TileAt(p + extent);
        for (int x = min.x; x <= max.x; x++)
        for (int y = min.y; y <= max.y; y++)
            if (!IsFloor(new Vector2Int(x, y))) return false;
        return true;
    }

    public bool ClearLine(Vector2 a, Vector2 b)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.12f));
        for (int i = 0; i <= steps; i++)
            if (!IsFloor(TileAt(Vector2.Lerp(a, b, i / (float)steps)))) return false;
        return true;
    }

    public int[,] BuildDistances(Vector2 target, int range, int[,] field = null, bool sound = false)
    {
        field ??= new int[Size, Size];
        System.Array.Clear(field, 0, field.Length);
        Vector2Int start = TileAt(target);
        Queue<Vector2Int> queue = new();
        if (sound)
        {
            if (!IsFloor(start)) return field;
            queue.Enqueue(start);
            field[start.x, start.y] = 1;
        }
        else
        {
            for (int x = start.x - 1; x <= start.x + 1; x++)
            for (int y = start.y - 1; y <= start.y + 1; y++)
            {
                Vector2Int nearby = new Vector2Int(x, y);
                if (!InBounds(nearby) || !ClearMovementLine(target, World(nearby))) continue;
                queue.Enqueue(nearby);
                field[x, y] = 1;
            }
        }
        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            if (field[p.x, p.y] >= range) continue;
            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = p + dir;
                if (!InBounds(next) || field[next.x, next.y] != 0 ||
                    (sound ? !IsFloor(next) : !CanStand(World(next)))) continue;
                field[next.x, next.y] = field[p.x, p.y] + 1;
                queue.Enqueue(next);
            }
        }
        return field;
    }

    public Vector2 ChaseDirection(Vector2 p, Vector2 target, int[,] field)
    {
        if (ClearMovementLine(p, target)) return (target - p).normalized;
        if (field == null) return Vector2.zero;
        Vector2Int cell = TileAt(p);
        int best = int.MaxValue;
        Vector2 waypoint = p;
        for (int x = cell.x - 1; x <= cell.x + 1; x++)
        for (int y = cell.y - 1; y <= cell.y + 1; y++)
        {
            Vector2Int next = new Vector2Int(x, y);
            if (!InBounds(next) || field[x, y] <= 0 || field[x, y] >= best) continue;
            Vector2 candidate = World(next);
            if (!ClearMovementLine(p, candidate)) continue;
            best = field[x, y];
            waypoint = candidate;
        }
        return (waypoint - p).normalized;
    }

    public bool ClearMovementLine(Vector2 a, Vector2 b)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.15f));
        for (int i = 1; i <= steps; i++)
            if (!CanStand(Vector2.Lerp(a, b, i / (float)steps))) return false;
        return true;
    }

    void Draw()
    {
        if (map == null)
        {
            GameObject grid = new GameObject("Terrain", typeof(Grid));
            grid.transform.SetParent(transform, false);
            grid.GetComponent<Grid>().cellSize = new Vector3(tileSize, tileSize, 1);
            map = AddLayer(grid.transform, "Ground", -10);
            decorationMap = AddLayer(grid.transform, "Props", -9);
            terrainMaterial = new Material(Resources.Load<Shader>("Shaders/MazeTerrain"));
            map.GetComponent<TilemapRenderer>().sharedMaterial = terrainMaterial;
        }
        map.ClearAllTiles();
        decorationMap.ClearAllTiles();
        TileBase[] terrain = new TileBase[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            string asset = floor[x, y] ? themes[x, y] switch
            {
                RoomKind.Courtyard => "earth",
                RoomKind.Crypt => "stone",
                RoomKind.Watch => "sand",
                RoomKind.Keep => "flagstone",
                _ => "rect_gray" + random.Next(4)
            } : "brick_dark" + random.Next(4);
            terrain[x + y * Size] = GetTile(asset);
        }
        map.SetTilesBlock(new BoundsInt(0, 0, 0, Size, Size, 1), terrain);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            Vector3Int p = new Vector3Int(x, y, 0);
            map.SetTileFlags(p, TileFlags.None);
            float shade = floor[x, y] ? 0.92f : 0.38f;
            if (floor[x, y] && themes[x, y] != RoomKind.Hall)
                shade *= 0.92f + (float)random.NextDouble() * 0.14f;
            if (floor[x, y] && !IsFloor(new Vector2Int(x, y + 1))) shade *= 0.73f;
            Color tone = themes[x, y] switch
            {
                RoomKind.Courtyard => new Color(0.88f, 0.83f, 0.76f),
                RoomKind.Crypt => new Color(0.80f, 0.86f, 0.92f),
                RoomKind.Watch => new Color(0.87f, 0.82f, 0.78f),
                RoomKind.Keep => new Color(0.91f, 0.88f, 0.83f),
                _ => new Color(1, 0.95f, 0.88f)
            };
            map.SetColor(p, new Color(tone.r * shade, tone.g * shade, tone.b * shade));
        }
        foreach (var entry in props)
        {
            Vector3Int p = (Vector3Int)entry.Key;
            decorationMap.SetTile(p, GetTile(entry.Value));
            decorationMap.SetTileFlags(p, TileFlags.None);
            decorationMap.SetColor(p, new Color(0.86f, 0.81f, 0.77f));
            if (entry.Value == "barricade")
                decorationMap.SetTransformMatrix(p, Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90 * random.Next(4))));
        }
    }

    static Tilemap AddLayer(Transform grid, string name, int order)
    {
        GameObject layer = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        layer.transform.SetParent(grid, false);
        layer.GetComponent<TilemapRenderer>().sortingOrder = order;
        return layer.GetComponent<Tilemap>();
    }

    Tile GetTile(string name)
    {
        if (tiles.TryGetValue(name, out Tile tile)) return tile;
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Resources.Load<Sprite>("Art/World/" + name);
        if (tile.sprite == null) tile.sprite = Resources.Load<Sprite>("Art/Tiles/" + name);
        tile.colliderType = Tile.ColliderType.None;
        tiles.Add(name, tile);
        return tile;
    }

    void OnDestroy()
    {
        if (terrainMaterial != null) Destroy(terrainMaterial);
        foreach (Tile tile in tiles.Values) Destroy(tile);
    }
}
