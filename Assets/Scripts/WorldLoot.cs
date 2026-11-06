using UnityEngine;

public sealed class WorldLoot : MonoBehaviour
{
    GameManager game;
    int souls;
    Potion? potion;
    float time;

    public static string PotionArt(Potion kind) => kind switch
    {
        Potion.Heal => "heal",
        Potion.Guard => "guard",
        _ => "fury"
    };

    public static void Drop(GameManager owner, Vector2 position, int souls = 0, Potion? potion = null)
    {
        GameObject obj = new GameObject(potion?.ToString() ?? "Souls");
        obj.transform.SetParent(owner.actors, false);
        obj.transform.position = position;
        WorldLoot item = obj.AddComponent<WorldLoot>();
        item.game = owner;
        item.souls = souls;
        item.potion = potion;
        SpriteRenderer view = obj.AddComponent<SpriteRenderer>();
        view.sortingOrder = 5;
        view.sprite = Resources.Load<Sprite>(potion.HasValue ? "Art/World/" + PotionArt(potion.Value) : "Art/Effects/soul");
    }

    void Update()
    {
        if (game.Paused || game.player == null || game.player.Dead) return;
        time += Time.deltaTime;
        Vector2 player = game.player.transform.position;
        float distance = Vector2.Distance(transform.position, player);
        transform.localScale = Vector3.one * (0.93f + Mathf.Sin(time * 5) * 0.12f);
        if (distance > 0.5f)
        {
            if (distance < 2.15f && game.maze.ClearLine(transform.position, player))
                transform.position = Vector2.MoveTowards(transform.position, player, Time.deltaTime * 5);
            return;
        }
        if (!game.maze.ClearLine(transform.position, player)) return;
        game.Run.Souls += souls;
        if (potion == Potion.Heal)
        {
            if (!game.Run.FlaskUsed) game.Run.Potions[(int)Potion.Heal] = 1;
        }
        else if (potion.HasValue) game.Run.Potions[(int)potion.Value]++;
        Destroy(gameObject);
    }
}
