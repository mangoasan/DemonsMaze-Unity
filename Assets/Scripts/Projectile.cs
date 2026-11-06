using UnityEngine;

public class Projectile : MonoBehaviour
{
    GameManager game;
    Demon source;
    Vector2 direction;
    SpriteRenderer view;
    int damage;
    float speed;
    float age;
    bool bottle;
    bool spell;
    bool exploding;

    public static void Create(GameManager game, Vector2 position, Vector2 direction, CharacterData data, int damage)
    {
        Projectile shot = Spawn(game, position, direction, damage, data.bottle ? "vial" : "magic_04");
        shot.bottle = data.bottle;
        shot.spell = !data.bottle;
        shot.speed = data.bottle ? 6 : 8;
    }

    public static void CreateEnemy(GameManager game, Demon owner, Vector2 direction, int damage, string sprite)
    {
        Projectile shot = Spawn(game, (Vector2)owner.transform.position + direction * 0.42f, direction, damage, sprite);
        shot.source = owner;
        shot.speed = sprite == "enemy_arrow" ? 9 : 6;
    }

    static Projectile Spawn(GameManager game, Vector2 position, Vector2 direction, int damage, string sprite)
    {
        GameObject obj = new GameObject(sprite);
        obj.transform.SetParent(game.actors);
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90);
        Projectile shot = obj.AddComponent<Projectile>();
        shot.game = game;
        shot.direction = direction;
        shot.damage = damage;
        shot.view = obj.AddComponent<SpriteRenderer>();
        shot.view.sortingOrder = 12;
        shot.view.sprite = Resources.Load<Sprite>("Art/Effects/" + sprite);
        return shot;
    }

    void Update()
    {
        if (game.Paused) return;
        age += Time.deltaTime;
        if (exploding)
        {
            view.sprite = Resources.Load<Sprite>("Art/Effects/burst_" + Mathf.Min(3, (int)(age * 10)).ToString("00"));
            if (age > 0.42f) Destroy(gameObject);
            return;
        }

        Vector2 movement = direction * speed * Time.deltaTime;
        int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / 0.1f));
        for (int i = 0; i < steps; i++)
        {
            Vector2 next = (Vector2)transform.position + movement / steps;
            if (!game.maze.IsFloor(game.maze.TileAt(next)))
            {
                Impact();
                return;
            }
            transform.position = next;
            if (HitsTarget(next))
            {
                Impact();
                return;
            }
        }

        if (bottle || spell) transform.Rotate(0, 0, Time.deltaTime * (bottle ? 240 : 75));
        if (spell) view.sprite = Resources.Load<Sprite>("Art/Effects/magic_" + (1 + (int)(age * 10) % 8).ToString("00"));
        if (age > (bottle ? 0.8f : 1.6f)) Impact();
    }

    bool HitsTarget(Vector2 next)
    {
        bool hostile = source != null && !source.Allied;
        if (hostile && !game.player.Dead && Vector2.Distance(next, game.player.transform.position) < 0.35f)
        {
            game.player.TakeDamage(damage);
            return true;
        }
        foreach (Demon enemy in game.enemies)
        {
            if (enemy == null || enemy == source || enemy.Dead || enemy.Allied != hostile ||
                Vector2.Distance(next, enemy.transform.position) > enemy.Radius + 0.08f) continue;
            if (!bottle) enemy.TakeDamage(damage, direction * 0.13f);
            return true;
        }
        return false;
    }

    void Impact()
    {
        if (!bottle)
        {
            Destroy(gameObject);
            return;
        }
        exploding = true;
        age = 0;
        transform.localScale = Vector3.one * 2;
        foreach (Demon enemy in game.enemies)
        {
            if (enemy != null && !enemy.Dead && !enemy.Allied &&
                Vector2.Distance(transform.position, enemy.transform.position) <= 1.15f &&
                game.maze.ClearLine(transform.position, enemy.transform.position))
                enemy.TakeDamage(damage, direction * 0.15f);
        }
    }
}
