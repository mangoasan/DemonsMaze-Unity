using UnityEngine;

[RequireComponent(typeof(GameManager))]
public class GameplayHud : MazeInterface
{
    GameManager game;
    GUIStyle text;
    GUIStyle centered;
    GUIStyle counters;
    GUIStyle heading;
    Sprite soul;
    Sprite rune;
    readonly Sprite[] potions = new Sprite[3];

    void Awake()
    {
        game = GetComponent<GameManager>();
        soul = Resources.Load<Sprite>("Art/Effects/soul");
        rune = Resources.Load<Sprite>("Art/World/rune");
        for (int i = 0; i < potions.Length; i++)
            potions[i] = Resources.Load<Sprite>("Art/World/" + WorldLoot.PotionArt((Potion)i));
    }

    protected override void Draw(float width, float height)
    {
        PlayerController hero = game.player;
        if (hero == null) return;
        text ??= MainMenu.SerifLabel(20, TextAnchor.MiddleLeft);
        centered ??= MainMenu.SerifLabel(23);
        counters ??= MainMenu.SerifLabel(24, TextAnchor.MiddleRight);
        heading ??= MainMenu.SerifLabel(47);

        if (game.IntroducingBoss)
        {
            DrawBossIntro(width, height);
            return;
        }
        RunProgress run = game.Run;
        DrawHero(hero, run);
        DrawInventory(run, height);
        if (!game.InCitadel)
        {
            DrawCount(width, 22, rune, run.Runes, GameManager.RequiredRunes, 0.78f);
            DrawCount(width, 53, soul, run.Souls, 0, 0.28f);
        }
        if (game.Boss != null && !game.Boss.sleeping)
            DrawBoss(width, game.Boss);
        if (game.Nearby != null && !game.Nearby.Used && !game.Paused && !hero.Dead)
            DrawInteraction(game.Nearby, run, width, height);
        if (game.CheatsOpen) DrawCheats(width, height);
        else if (game.Shop != null) DrawAltar(width, height, game.Shop, run);
        else if (game.Paused) DrawPause(width, height);
    }

    void DrawHero(PlayerController hero, RunProgress run)
    {
        Track(new Rect(24, 27, 196, 9), hero.health / (float)run.MaxHealth, new Color(0.63f, 0.24f, 0.22f));
        GUI.Label(new Rect(228, 20, 76, 24), $"{hero.health}/{run.MaxHealth}", text);
        Track(new Rect(24, 43, 140, 4), hero.stamina / 100, new Color(0.40f, 0.50f, 0.32f));
        if (run.Shield > 0)
            Track(new Rect(24, 53, 86, 3), run.WardTime / PlayerController.ShieldDuration, new Color(0.46f, 0.63f, 0.72f));
        if (hero.Empowered)
            Fill(new Rect(172, 41, 8, 8), new Color(0.79f, 0.51f, 0.24f));
        GUI.Label(new Rect(24, 62, 260, 24), hero.AbilityStatus, text);
    }

    void DrawInventory(RunProgress run, float height)
    {
        float x = 24;
        for (int i = 0; i < potions.Length; i++)
        {
            if (run.Potions[i] <= 0) continue;
            Rect icon = new Rect(x, height - 53, 28, 31);
            Fill(new Rect(icon.x, icon.y, 62, 30), new Color(0.08f, 0.07f, 0.065f, 0.68f));
            DrawIcon(icon, potions[i]);
            GUI.Label(new Rect(icon.x + 30, icon.y, 29, icon.height),
                run.Potions[i] > 1 ? $"{i + 1}·{run.Potions[i]}" : (i + 1).ToString(), text);
            x += 69;
        }
    }

    void DrawCount(float width, float top, Sprite icon, int current, int required, float crop)
    {
        bool ready = required == 0 || current >= required;
        Rect marker = new Rect(width - 103, top, 27, 27);
        GUI.color = ready ? Cream : new Color(0.83f, 0.79f, 0.72f, 0.90f);
        DrawIcon(marker, icon, crop);
        GUI.color = Color.white;
        counters.normal.textColor = ready ? Cream : new Color(0.72f, 0.68f, 0.62f);
        GUI.Label(new Rect(width - 71, top - 2, 46, 30), required == 0 ? current.ToString() : $"{current}/{required}", counters);
    }

    void DrawBoss(float width, Demon boss)
    {
        float x = width * 0.5f - 208;
        GUI.Label(new Rect(x, 17, 416, 26), "Лорд Ноктул", centered);
        Track(new Rect(x, 45, 416, 7), boss.health / (float)boss.MaxHealth, new Color(0.59f, 0.18f, 0.24f));
    }

    void DrawInteraction(WorldLandmark item, RunProgress run, float width, float height)
    {
        string label = item.Kind == Landmark.Gate
            ? run.Runes >= GameManager.RequiredRunes
                ? "E   Открыть врата"
                : "Нужны три руны"
            : "E   " + item.Title;
        Rect prompt = new Rect(width * 0.5f - 205, height - 74, 410, 36);
        Fill(prompt, new Color(0.08f, 0.07f, 0.065f, 0.82f));
        GUI.Label(prompt, label, centered);
    }

    void DrawBossIntro(float width, float height)
    {
        float time = game.BossIntroTime;
        float reveal = Mathf.Min(Mathf.InverseLerp(0, 0.45f, time),
            Mathf.InverseLerp(GameManager.BossIntroDuration, 2.75f, time));
        float bar = Mathf.SmoothStep(0, height * 0.13f, reveal);
        Fill(new Rect(0, 0, width, bar), Color.black);
        Fill(new Rect(0, height - bar, width, bar), Color.black);
        if (time >= 0.75f && time <= 2.9f)
        {
            GUI.color = new Color(1, 1, 1, reveal);
            GUI.Label(new Rect(0, height - bar - 87, width, 62), "Лорд Ноктул", heading);
            GUI.color = Color.white;
        }
        GUI.Label(new Rect(width - 208, height - 40, 184, 28), "Пробел   Пропустить", text);
    }

    void DrawCheats(float width, float height)
    {
        float x = width * 0.5f;
        float y = height * 0.5f - 150;
        Modal(width, height, y, "Чит-меню");
        if (!game.InCitadel && MainMenu.DrawChoice(new Rect(x - 165, y + 98, 330, 46), "Выдать три руны", centered))
            game.GrantRunes();
        if (MainMenu.DrawChoice(new Rect(x - 165, y + 151, 330, 46), "Телепорт к боссу", centered))
            game.WarpToBoss();
        if (MainMenu.DrawChoice(new Rect(x - 165, y + 215, 330, 43), "Закрыть", centered))
            game.ToggleCheats();
    }

    void DrawAltar(float width, float height, WorldLandmark shop, RunProgress run)
    {
        float x = width * 0.5f;
        float y = height * 0.5f - 150;
        Modal(width, height, y, shop.Title);
        string title = shop.Offer == Potion.Guard ? "Зелье щита" : "Зелье ярости";
        DrawIcon(new Rect(x - 114, y + 101, 33, 33), potions[(int)shop.Offer]);
        GUI.Label(new Rect(x - 66, y + 98, 220, 38), title, text);
        string stock = shop.Stock > 0 ? $"В запасе {shop.Stock}    ·    Души {run.Souls}" : "Распродано";
        GUI.Label(new Rect(x - 165, y + 136, 330, 31), stock, centered);
        if (shop.CanBuy)
        {
            if (MainMenu.DrawChoice(new Rect(x - 165, y + 174, 330, 42), $"Купить за {shop.Price}", centered, true))
                shop.Buy();
        }
        else if (shop.Stock > 0)
            GUI.Label(new Rect(x - 165, y + 174, 330, 42), $"Цена {shop.Price}", centered);
        if (MainMenu.DrawChoice(new Rect(x - 165, y + 224, 330, 42), "Закрыть", centered))
            game.CloseShop();
    }

    void Modal(float width, float height, float y, string title)
    {
        Fill(new Rect(0, 0, width, height), new Color(0.018f, 0.012f, 0.02f, 0.88f));
        GUI.Label(new Rect(0, y, width, 73), title, heading);
        MainMenu.Ornament(width * 0.5f, y + 81);
    }

    void DrawPause(float width, float height)
    {
        Fill(new Rect(0, 0, width, height), new Color(0.018f, 0.012f, 0.02f, 0.85f));
        float x = width * 0.5f;
        float y = height * 0.5f - 122;
        GUI.Label(new Rect(x - 220, y, 440, 75), "Пауза", heading);
        MainMenu.Ornament(x, y + 82);
        if (MainMenu.DrawChoice(new Rect(x - 136, y + 105, 272, 50), "Продолжить", centered, true))
            game.SetPaused(false);
        if (MainMenu.DrawChoice(new Rect(x - 136, y + 160, 272, 50), "Главное меню", centered))
            game.ShowMenu();
    }

    static void Track(Rect rect, float value, Color color)
    {
        Fill(rect, new Color(0.09f, 0.075f, 0.07f, 0.90f));
        Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
    }

    static void DrawIcon(Rect rect, Sprite sprite, float crop = 1)
    {
        if (sprite == null) return;
        Rect frame = sprite.rect;
        Vector2 size = frame.size * crop;
        Vector2 origin = frame.center - size * 0.5f;
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, new Rect(origin.x / sprite.texture.width,
            origin.y / sprite.texture.height, size.x / sprite.texture.width, size.y / sprite.texture.height));
    }
}
