using UnityEngine;

public enum RoomKind
{
    Hall,
    Courtyard,
    Crypt,
    Watch,
    Keep
}

public sealed class MazeRoom
{
    public Vector2Int Center { get; }
    public RoomKind Kind { get; }
    public Vector2Int Half { get; }

    public MazeRoom(Vector2Int center, RoomKind kind, Vector2Int half)
    {
        Center = center;
        Kind = kind;
        Half = half;
    }
}
