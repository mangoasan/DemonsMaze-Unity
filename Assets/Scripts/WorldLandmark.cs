using UnityEngine;

public enum Landmark
{
    Spring,
    Altar,
    Vitality,
    Power,
    Chest,
    Rune,
    Gate
}

public sealed class WorldLandmark : MonoBehaviour
{
    public Landmark Kind { get; private set; }
    public bool Used { get; private set; }
    public int Stock { get; private set; }
    public Potion Offer => reward;
    public int Price => reward == Potion.Guard ? 3 : 4;
    public bool CanBuy => game.Shop == this && Stock > 0 && game.Run.Souls >= Price && game.Run.Potions[(int)reward] < 9;
    public string Title => Kind switch
    {
        Landmark.Spring => "Святой источник",
        Landmark.Altar => "Алтарь расходников",
        Landmark.Vitality => "Молитва жизни",
        Landmark.Power => "Молитва силы",
        Landmark.Chest => "Сундук",
        Landmark.Rune => "Руна",
        _ => "Врата цитадели"
    };

    GameManager game;
    SpriteRenderer view;
    AudioSource water;
    Potion reward;

    public static WorldLandmark Create(GameManager owner, Landmark kind, Vector2 position, Potion reward, int stock)
    {
        GameObject obj = new GameObject(kind.ToString());
        obj.transform.SetParent(owner.actors, false);
        obj.transform.position = position;
        WorldLandmark mark = obj.AddComponent<WorldLandmark>();
        mark.game = owner;
        mark.Kind = kind;
        mark.reward = reward;
        mark.Stock = stock;
        mark.view = obj.AddComponent<SpriteRenderer>();
        mark.view.sortingOrder = 3;
        mark.view.sprite = Resources.Load<Sprite>("Art/World/" + (kind switch
        {
            Landmark.Spring => "holy_spring",
            Landmark.Altar => "rune",
            Landmark.Vitality => "prayer_life",
            Landmark.Power => "prayer_power",
            Landmark.Chest => "chest",
            Landmark.Rune => "rune",
            _ => "gate"
        }));
        if (kind == Landmark.Spring)
        {
            mark.water = obj.AddComponent<AudioSource>();
            mark.water.clip = Resources.Load<AudioClip>("Music/waterflow");
            mark.water.loop = true;
            mark.water.volume = 0;
        }
        if (kind == Landmark.Altar) mark.view.color = new Color(1.1f, 0.83f, 0.49f);
        obj.transform.localScale = Vector3.one * (kind is Landmark.Gate or Landmark.Altar ? 1.45f : 1.1f);
        return mark;
    }

    void Update()
    {
        if (water == null || water.clip == null || game.player == null) return;
        float distance = Vector2.Distance(game.player.transform.position, transform.position);
        float volume = distance < 5 && game.maze.ClearLine(transform.position, game.player.transform.position)
            ? 0.30f * Mathf.InverseLerp(5, 0.65f, distance) : 0;
        water.volume = Mathf.MoveTowards(water.volume, volume, Time.unscaledDeltaTime * 0.7f);
        if (water.volume > 0.002f && !water.isPlaying) water.Play();
        if (water.volume <= 0.002f && water.isPlaying) water.Stop();
    }

    public void Use()
    {
        if (Used || game.player.Dead || Vector2.Distance(transform.position, game.player.transform.position) > 1.35f ||
            !game.maze.ClearLine(transform.position, game.player.transform.position)) return;

        switch (Kind)
        {
            case Landmark.Gate:
                game.EnterCitadel();
                return;
            case Landmark.Altar:
                game.OpenShop(this);
                return;
            case Landmark.Spring:
                if (game.player.health >= game.Run.MaxHealth || game.player.Fighting) return;
                game.player.Heal(16);
                break;
            case Landmark.Vitality:
                game.Run.Vitality++;
                game.player.Heal(9);
                view.enabled = false;
                break;
            case Landmark.Power:
                game.Run.Power++;
                view.enabled = false;
                break;
            case Landmark.Rune:
                game.Run.Runes = Mathf.Min(GameManager.RequiredRunes, game.Run.Runes + 1);
                HideRune();
                return;
            case Landmark.Chest:
                WorldLoot.Drop(game, transform.position, potion: reward);
                view.sprite = Resources.Load<Sprite>("Art/World/open_chest");
                break;
        }
        Used = true;
        view.color = new Color(0.73f, 0.70f, 0.66f);
    }

    public void HideRune()
    {
        Used = true;
        view.enabled = false;
    }

    public void Buy()
    {
        if (!CanBuy || !game.Run.Spend(Price)) return;
        Stock--;
        game.Run.Potions[(int)reward]++;
    }
}
