using UnityEngine;

public class Projectile : MonoBehaviour
{
    public static int impacts;
    GameManager game;
    CharacterData data;
    Vector2 direction;
    SpriteRenderer view;
    Sprite[] frames;
    float age;
    bool exploding;

    public static Projectile Create(GameManager game, Vector2 position, Vector2 direction, CharacterData data)
    {
        GameObject obj = new GameObject(data.bottle ? "Bottle" : "Spell");
        obj.transform.SetParent(game.actors);
        obj.transform.position = position;
        Projectile shot = obj.AddComponent<Projectile>();
        shot.game = game;
        shot.data = data;
        shot.direction = direction;
        shot.view = obj.AddComponent<SpriteRenderer>();
        shot.view.sortingOrder = 12;
        shot.view.spriteSortPoint = SpriteSortPoint.Pivot;
        shot.frames = Resources.LoadAll<Sprite>("Art/Effects");
        shot.view.sprite = Resources.Load<Sprite>(data.bottle ? "Art/Effects/vial" : "Art/Effects/magic_04");
        obj.transform.localScale = Vector3.one * (data.bottle ? 1 : 1.5f);
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
        Vector2 movement = direction * (data.bottle ? 6 : 8) * Time.deltaTime;
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
            foreach (Demon enemy in game.enemies.ToArray())
            {
                if (enemy == null || enemy.Dead || Vector2.Distance(next, enemy.transform.position) > 0.43f) continue;
                if (!data.bottle) enemy.TakeDamage(data.damage, direction * 0.13f);
                Impact();
                return;
            }
        }
        transform.Rotate(0, 0, Time.deltaTime * (data.bottle ? 240 : 75));
        if (!data.bottle) view.sprite = Resources.Load<Sprite>("Art/Effects/magic_" + (1 + (int)(age * 10) % 8).ToString("00"));
        if (age > (data.bottle ? 0.8f : 1.4f)) Impact();
    }

    void Impact()
    {
        impacts++;
        if (!data.bottle)
        {
            Destroy(gameObject);
            return;
        }
        exploding = true;
        age = 0;
        transform.localScale = Vector3.one * 2;
        foreach (Demon enemy in game.enemies.ToArray())
        {
            if (enemy != null && !enemy.Dead && Vector2.Distance(transform.position, enemy.transform.position) <= 1.15f && game.maze.ClearLine(transform.position, enemy.transform.position)) enemy.TakeDamage(data.damage, direction * 0.15f);
        }
    }
}
