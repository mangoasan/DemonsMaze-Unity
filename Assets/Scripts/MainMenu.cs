using UnityEngine;

public class MainMenu : MazeInterface
{
    static Texture2D background;
    static Font serif;
    CharacterData[] classes;
    GUIStyle titleStyle;
    GUIStyle headingStyle;
    GUIStyle classStyle;
    GUIStyle choiceStyle;
    bool choosingClass;
    int choice;

    void Start()
    {
        Time.timeScale = 1;
        Cursor.visible = true;
        classes = CharacterData.LoadPlayerClasses();
        GameMusic.Play(gameObject, "menu", 0.28f);
    }

    void Update()
    {
        if (choosingClass)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) choosingClass = false;
            return;
        }
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) choice = 1 - choice;
        if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
        if (choice == 0) choosingClass = true;
        else Quit();
    }

    protected override void Draw(float width, float height)
    {
        if (classes == null) return;
        titleStyle ??= SerifLabel(88);
        headingStyle ??= SerifLabel(43);
        classStyle ??= SerifLabel(24);
        choiceStyle ??= SerifLabel(31);

        DrawBackdrop(width, height);
        if (choosingClass)
        {
            DrawClasses(width, height);
            return;
        }

        float x = width * 0.5f;
        float y = height * 0.5f - 173;
        GUI.Label(new Rect(x - 510, y, 1020, 115), "DEMON’S MAZE", titleStyle);
        Ornament(x, y + 123);
        Rect play = new Rect(x - 143, y + 169, 286, 50);
        Rect exit = new Rect(x - 143, y + 223, 286, 50);
        if (Event.current.type == EventType.MouseMove)
        {
            if (play.Contains(Event.current.mousePosition)) choice = 0;
            if (exit.Contains(Event.current.mousePosition)) choice = 1;
        }
        if (DrawChoice(play, "Играть", choiceStyle, choice == 0)) choosingClass = true;
        if (DrawChoice(exit, "Выход", choiceStyle, choice == 1)) Quit();
    }

    public static GUIStyle SerifLabel(int size, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        serif ??= Resources.Load<Font>("UI/CormorantSC-Medium");
        GUIStyle style = LabelStyle(size, alignment, Cream);
        if (serif != null) style.font = serif;
        style.fontStyle = FontStyle.Normal;
        style.hover.textColor = Cream;
        style.active.textColor = Cream;
        style.focused.textColor = Cream;
        return style;
    }

    public static void DrawBackdrop(float width, float height)
    {
        if (background == null)
        {
            const int size = 64;
            background = new Texture2D(size, size, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            Color edge = new Color(0.013f, 0.013f, 0.016f);
            Color ember = new Color(0.115f, 0.092f, 0.068f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x / (float)(size - 1) - 0.5f, y / (float)(size - 1) - 0.54f);
                delta.x *= 1.45f;
                float light = 1 - Mathf.SmoothStep(0, 1, delta.magnitude / 0.66f);
                pixels[x + y * size] = Color.Lerp(edge, ember, light * light);
            }
            background.SetPixels32(pixels);
            background.Apply(false, true);
        }
        GUI.DrawTexture(new Rect(0, 0, width, height), background);
    }

    public static void Ornament(float x, float y)
    {
        Fill(new Rect(x - 85, y, 71, 1), Line);
        Fill(new Rect(x + 14, y, 71, 1), Line);
        Fill(new Rect(x - 1, y - 4, 3, 9), Copper);
        Fill(new Rect(x - 4, y - 1, 9, 3), Copper);
    }

    public static bool DrawChoice(Rect rect, string label, GUIStyle style, bool selected = false)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        if (selected || hover)
        {
            Fill(rect, new Color(0.55f, 0.40f, 0.24f, 0.13f));
            Fill(new Rect(rect.x + 11, rect.y + rect.height * 0.5f - 2, 4, 4), Copper);
            Fill(new Rect(rect.xMax - 15, rect.y + rect.height * 0.5f - 2, 4, 4), Copper);
        }
        style.normal.textColor = selected || hover ? Cream : new Color(0.68f, 0.63f, 0.56f);
        return GUI.Button(rect, label, style);
    }

    void DrawClasses(float width, float height)
    {
        const float cardWidth = 167;
        const float gap = 18;
        float left = (width - (classes.Length * (cardWidth + gap) - gap)) * 0.5f;
        float top = height * 0.5f - 93;
        GUI.Label(new Rect(0, top - 117, width, 62), "Выберите класс", headingStyle);
        Ornament(width * 0.5f, top - 38);

        for (int i = 0; i < classes.Length; i++)
        {
            Rect card = new Rect(left + i * (cardWidth + gap), top, cardWidth, 180);
            if (!DrawCard(card, classes[i])) continue;
            GameManager.LoadGame(classes[i]);
            return;
        }
        if (DrawChoice(new Rect(width * 0.5f - 118, top + 216, 236, 44), "Назад", choiceStyle))
            choosingClass = false;
    }

    bool DrawCard(Rect card, CharacterData character)
    {
        bool hover = card.Contains(Event.current.mousePosition);
        Color border = hover ? Copper : Line;
        Fill(new Rect(card.x, card.y, card.width, 1), border);
        Fill(new Rect(card.x, card.yMax - 1, card.width, 1), border);
        Fill(new Rect(card.x, card.y + 1, card.width, card.height - 2),
            hover ? new Color(0.18f, 0.142f, 0.106f) : new Color(0.057f, 0.051f, 0.047f));
        Sprite idle = character.idle;
        Rect source = idle.rect;
        Texture2D texture = idle.texture;
        Rect uv = new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height);
        GUI.DrawTextureWithTexCoords(new Rect(card.center.x - 66, card.y + 5, 132, 132), texture, uv);
        GUI.Label(new Rect(card.x, card.y + 135, card.width, 40), character.title, classStyle);
        return GUI.Button(card, GUIContent.none, GUIStyle.none);
    }

    void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
