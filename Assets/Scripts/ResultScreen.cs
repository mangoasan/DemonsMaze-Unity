using UnityEngine;

public sealed class ResultScreen : MazeInterface
{
    GUIStyle heading;
    GUIStyle stats;
    GUIStyle choice;
    GUIStyle credits;

    void Start()
    {
        Time.timeScale = 1;
        GameMusic.Play(gameObject, "menu", 0.22f);
    }

    protected override void Draw(float width, float height)
    {
        heading ??= MainMenu.SerifLabel(72);
        stats ??= MainMenu.SerifLabel(24);
        choice ??= MainMenu.SerifLabel(31);
        credits ??= MainMenu.SerifLabel(28);
        MainMenu.DrawBackdrop(width, height);
        RunProgress run = GameManager.CurrentRun;
        float x = width * 0.5f;
        bool victory = run != null && run.Victory;
        float y = height * 0.5f - (victory ? 212 : 154);
        GUI.Label(new Rect(0, y, width, 90), victory ? "Победа" : "Поражение", heading);
        MainMenu.Ornament(x, y + 94);
        float buttons = y + 179;
        if (run != null)
        {
            GUI.Label(new Rect(0, y + 108, width, 49),
                $"{run.Character.title}     ·     Побеждено {run.Kills}     ·     Руны {run.Runes}/{GameManager.RequiredRunes}", stats);
            if (victory)
            {
                GUI.Label(new Rect(0, y + 166, width, 38), "Художник  ·  Кенжеханов Ратмин", credits);
                GUI.Label(new Rect(0, y + 212, width, 38), "Программист  ·  Мурат Асанали", credits);
                buttons = y + 278;
            }
            if (MainMenu.DrawChoice(new Rect(x - 136, buttons, 272, 54), "Новый забег", choice, true))
                GameManager.LoadGame(run.Character);
        }
        if (MainMenu.DrawChoice(new Rect(x - 136, buttons + 60, 272, 49), "Главное меню", choice)) GameManager.OpenMenu();
    }
}
