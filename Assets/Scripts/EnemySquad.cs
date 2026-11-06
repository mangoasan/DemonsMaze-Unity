using System.Collections.Generic;
using UnityEngine;

sealed class EnemySquad
{
    readonly GameManager game;
    readonly Vector2[] goals = new Vector2[4];
    readonly int[][,] routes = new int[4][,];
    readonly List<Demon> group = new();
    readonly Dictionary<Demon, int> roles = new();

    public EnemySquad(GameManager owner)
    {
        game = owner;
    }

    public void Refresh()
    {
        Vector2 player = game.player.transform.position;
        Vector2 heading = game.player.TravelDirection;
        if (heading.sqrMagnitude < 0.1f) heading = game.player.facing;
        Vector2 left = new Vector2(-heading.y, heading.x);
        goals[0] = player;
        goals[1] = OpenExit(player, heading, 4);
        goals[2] = OpenExit(player, left, 3.2f);
        goals[3] = OpenExit(player, -left, 3.2f);

        group.Clear();
        roles.Clear();
        foreach (Demon enemy in game.enemies)
        {
            if (enemy == null || enemy.Kind == EnemyKind.Archer || !enemy.SeesHero()) continue;
            if (Vector2.Distance(enemy.transform.position, player) <= 11.5f) group.Add(enemy);
        }
        group.Sort((a, b) => ((Vector2)a.transform.position - player).sqrMagnitude.CompareTo(
            ((Vector2)b.transform.position - player).sqrMagnitude));

        routes[0] = game.maze.BuildDistances(player, 48, routes[0]);
        int assigned = 1;
        for (int i = 1; i < group.Count && assigned < goals.Length; i++)
        {
            if (Vector2.Distance(goals[assigned], player) > 1.25f)
            {
                roles[group[i]] = assigned;
                routes[assigned] = game.maze.BuildDistances(goals[assigned], 48, routes[assigned]);
            }
            assigned++;
        }
    }

    Vector2 OpenExit(Vector2 position, Vector2 direction, float reach)
    {
        Vector2 best = position;
        Vector2Int tile = game.maze.TileAt(position + direction * reach);
        float score = 0;
        for (int x = tile.x - 3; x <= tile.x + 3; x++)
        for (int y = tile.y - 3; y <= tile.y + 3; y++)
        {
            Vector2 candidate = game.maze.World(new Vector2Int(x, y));
            Vector2 delta = candidate - position;
            float advance = Vector2.Dot(delta, direction) - Mathf.Abs(Vector2.SignedAngle(direction, delta)) * 0.04f;
            if (advance <= score || delta.sqrMagnitude > reach * reach + 1 || !game.maze.CanStand(candidate)) continue;
            if (!game.maze.ClearMovementLine(position, candidate)) continue;
            best = candidate;
            score = advance;
        }
        return best;
    }

    public Vector2 Direction(Demon enemy)
    {
        Vector2 position = enemy.transform.position;
        Vector2 target = game.player.transform.position;
        if (roles.TryGetValue(enemy, out int role) && Vector2.Distance(position, target) > 1.85f)
        {
            if (Vector2.Distance(position, goals[role]) < 0.45f) return Vector2.zero;
            Vector2 flank = game.maze.ChaseDirection(position, goals[role], routes[role]);
            if (flank.sqrMagnitude > 0.01f) return flank;
        }
        return game.maze.ChaseDirection(position, target, routes[0]);
    }
}
