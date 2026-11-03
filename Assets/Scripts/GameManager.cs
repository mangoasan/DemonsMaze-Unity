using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public MazeGenerator maze;
    public PlayerController player;
    public List<Demon> enemies = new List<Demon>();
    public Transform actors;
    public CharacterData[] classes;
    public bool Paused { get; private set; }
    public bool Playing => screen == 2;
    public Camera gameCamera;
    [Min(0.5f)] public float cameraSize = 2.5f;
    const float TerrainPixelsPerUnit = 64;
    int screen;
    float pathTimer;
    Vector3 cameraPosition;
    GUIStyle titleStyle;
    GUIStyle buttonStyle;
    GUIStyle nameStyle;
    GUIStyle barStyle;
    Texture2D buttonTexture;
    Texture2D hoverTexture;
    Texture2D vignette;

    void Start()
    {
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 1;
        maze = GetComponent<MazeGenerator>();
        gameCamera = Camera.main;
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = cameraSize;
        gameCamera.nearClipPlane = 0.1f;
        gameCamera.farClipPlane = 30;
        gameCamera.allowHDR = false;
        gameCamera.allowMSAA = false;
        gameCamera.transparencySortMode = TransparencySortMode.CustomAxis;
        gameCamera.transparencySortAxis = Vector3.up;
        gameCamera.backgroundColor = new Color(0.025f, 0.025f, 0.03f);
        string[] names = { "Knight", "Alchemist", "Samurai", "Mage", "Rogue" };
        classes = new CharacterData[names.Length];
        for (int i = 0; i < names.Length; i++) classes[i] = Resources.Load<CharacterData>("Classes/" + names[i]);
        maze.Generate(73621);
        cameraPosition = new Vector3(maze.spawn.x + 3, maze.spawn.y + 2, -10);
        UpdateCamera();
        Cursor.visible = true;
    }

    public void StartGame(int index, int? seed = null, bool spawnEnemies = true)
    {
        ClearActors();
        maze.Generate(seed ?? UnityEngine.Random.Range(1, int.MaxValue));
        actors = new GameObject("Characters").transform;
        GameObject obj = new GameObject(classes[index].title);
        obj.transform.SetParent(actors);
        obj.transform.position = maze.spawn;
        player = obj.AddComponent<PlayerController>();
        player.Setup(classes[index], this);
        screen = 2;
        Paused = false;
        Time.timeScale = 1;
        cameraPosition = new Vector3(player.transform.position.x, player.transform.position.y, -10);
        UpdateCamera();
        maze.UpdateDistances(player.transform.position);
        if (spawnEnemies)
        {
            System.Random rng = new System.Random(maze.seed + 37);
            List<Vector2> placed = new List<Vector2>();
            for (int i = 0; i < 2000 && enemies.Count < 42; i++)
            {
                Vector2 position = maze.World(maze.junctions[rng.Next(maze.junctions.Count)]);
                if (Vector2.Distance(position, maze.spawn) < 5.5f) continue;
                if (placed.Exists(p => Vector2.Distance(p, position) < 3)) continue;
                SpawnDemon(position);
                placed.Add(position);
            }
            foreach (Vector2 direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
            {
                Vector2 close = maze.spawn + direction * 4;
                if (!maze.CanStand(close) || !maze.ClearLine(maze.spawn, close)) continue;
                SpawnDemon(close);
                break;
            }
        }
    }

    public Demon SpawnDemon(Vector2 position)
    {
        if (!maze.CanStand(position))
        {
            float best = float.MaxValue;
            Vector2 desired = position;
            foreach (Vector2Int cell in maze.junctions)
            {
                Vector2 candidate = maze.World(cell);
                float distance = Vector2.Distance(candidate, desired);
                if (distance > 1 && distance < best && Vector2.Distance(candidate, maze.spawn) > 3)
                {
                    best = distance;
                    position = candidate;
                }
            }
        }
        GameObject obj = new GameObject("Demon");
        obj.transform.SetParent(actors);
        obj.transform.position = position;
        Demon demon = obj.AddComponent<Demon>();
        demon.Setup(this);
        enemies.Add(demon);
        return demon;
    }

    void Update()
    {
        if (!Playing) return;
        if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!Paused);
        if (Paused || player == null) return;
        pathTimer -= Time.deltaTime;
        if (pathTimer <= 0)
        {
            maze.UpdateDistances(player.transform.position);
            pathTimer = 0.35f;
        }
    }

    void LateUpdate()
    {
        if (Playing && player != null)
            cameraPosition = new Vector3(player.transform.position.x, player.transform.position.y, -10);
        UpdateCamera();
    }

    void UpdateCamera()
    {
        if (gameCamera == null || gameCamera.pixelHeight <= 0) return;
        // Whole-number texel scaling prevents floor patterns from changing while scrolling.
        float zoom = Mathf.Max(1, Mathf.Round(gameCamera.pixelHeight / (2 * cameraSize * TerrainPixelsPerUnit)));
        float pixelsPerUnit = TerrainPixelsPerUnit * zoom;
        gameCamera.orthographicSize = gameCamera.pixelHeight / (2 * pixelsPerUnit);
        Vector3 p = cameraPosition;
        // Let the view extend past the map so the player stays centered at every edge.
        p.x = Mathf.Round(p.x * pixelsPerUnit) / pixelsPerUnit;
        p.y = Mathf.Round(p.y * pixelsPerUnit) / pixelsPerUnit;
        gameCamera.transform.position = p;
    }

    public void SetPaused(bool value)
    {
        Paused = value;
        Time.timeScale = value ? 0 : 1;
    }

    public void ShowMenu()
    {
        SetPaused(false);
        ClearActors();
        screen = 0;
        cameraPosition = new Vector3(maze.spawn.x + 3, maze.spawn.y + 2, -10);
        UpdateCamera();
    }

    void ClearActors()
    {
        if (actors != null)
        {
            actors.gameObject.SetActive(false);
            Destroy(actors.gameObject);
        }
        enemies.Clear();
        player = null;
    }

    void PrepareGui()
    {
        if (titleStyle != null) return;
        buttonTexture = Texture(new Color(0.19f, 0.18f, 0.20f));
        hoverTexture = Texture(new Color(0.32f, 0.29f, 0.28f));
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 58, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = new Color(0.96f, 0.92f, 0.82f);
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 25, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0), padding = new RectOffset(16, 16, 0, 0) };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.hover.background = hoverTexture;
        buttonStyle.active.background = hoverTexture;
        buttonStyle.focused.background = hoverTexture;
        buttonStyle.normal.textColor = titleStyle.normal.textColor;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;
        buttonStyle.focused.textColor = Color.white;
        nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        nameStyle.normal.textColor = titleStyle.normal.textColor;
        barStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 0, 0) };
        barStyle.normal.textColor = Color.white;
        vignette = new Texture2D(128, 72, TextureFormat.RGBA32, false);
        vignette.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 72; y++)
        for (int x = 0; x < 128; x++)
        {
            float r = new Vector2((x - 63.5f) / 85, (y - 35.5f) / 52).magnitude;
            vignette.SetPixel(x, y, new Color(0, 0, 0, Mathf.SmoothStep(0, 0.35f, Mathf.InverseLerp(0.55f, 1.25f, r))));
        }
        vignette.Apply();
    }

    Texture2D Texture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    void OnGUI()
    {
        PrepareGui();
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        if (scale <= 0) return;
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUI.color = Color.white;
        GUI.depth = -100;
        try
        {
            // All interface controls are drawn after the world vignette, at native resolution.
            GUI.DrawTexture(new Rect(0, 0, width, height), vignette);
            if (screen < 2)
            {
                Fill(new Rect(0, 0, width, height), new Color(0.015f, 0.013f, 0.02f, 0.90f));
                if (screen == 0)
                {
                    Rect panel = new Rect(width * 0.5f - 280, height * 0.5f - 180, 560, 360);
                    Panel(panel);
                    GUI.Label(new Rect(panel.x, panel.y + 22, panel.width, 90), "DEMON'S MAZE", titleStyle);
                    if (GUI.Button(new Rect(panel.x + 100, panel.y + 150, 360, 58), "Играть", buttonStyle)) screen = 1;
                    if (GUI.Button(new Rect(panel.x + 100, panel.y + 226, 360, 58), "Выход", buttonStyle)) Quit();
                }
                else
                {
                    GUIStyle smaller = new GUIStyle(titleStyle) { fontSize = 38 };
                    float left = width * 0.5f - 550;
                    float top = height * 0.5f - 105;
                    GUI.Label(new Rect(0, top - 135, width, 64), "Выберите класс", smaller);
                    for (int i = 0; i < classes.Length; i++)
                    {
                        Rect card = new Rect(left + i * 224, top, 204, 232);
                        bool selected = GUI.Button(card, GUIContent.none, buttonStyle);
                        Sprite sprite = classes[i].idle;
                        GUI.DrawTexture(new Rect(card.x + 32, card.y + 24, 140, 140), sprite.texture, ScaleMode.ScaleToFit, true);
                        GUI.Label(new Rect(card.x, card.y + 177, card.width, 42), classes[i].title, nameStyle);
                        if (selected) { StartGame(i); break; }
                    }
                    if (GUI.Button(new Rect(width * 0.5f - 120, top + 284, 240, 54), "Назад", buttonStyle)) screen = 0;
                }
            }
            else if (player != null)
            {
                if (Paused) Fill(new Rect(0, 0, width, height), new Color(0.01f, 0.01f, 0.015f, 0.75f));
                Panel(new Rect(20, 20, 340, 108));
                DrawBar(new Rect(36, 36, 308, 30), player.health / (float)player.data.health,
                    new Color(0.68f, 0.15f, 0.16f), "Здоровье  " + player.health + " / " + player.data.health);
                DrawBar(new Rect(36, 80, 308, 28), player.stamina / 100,
                    new Color(0.25f, 0.51f, 0.29f), "Стамина  " + Mathf.CeilToInt(player.stamina) + " / 100");
                if (Paused)
                {
                    Rect panel = new Rect(width * 0.5f - 210, height * 0.5f - 150, 420, 300);
                    Panel(panel);
                    GUI.Label(new Rect(panel.x, panel.y + 20, panel.width, 64), "Пауза", new GUIStyle(titleStyle) { fontSize = 38 });
                    if (GUI.Button(new Rect(panel.x + 40, panel.y + 108, 340, 58), "Продолжить", buttonStyle)) SetPaused(false);
                    if (GUI.Button(new Rect(panel.x + 40, panel.y + 188, 340, 58), "Меню", buttonStyle)) ShowMenu();
                }
            }
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }

    void Panel(Rect rect)
    {
        Fill(rect, new Color(0.44f, 0.40f, 0.35f));
        Fill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), new Color(0.055f, 0.05f, 0.065f));
    }

    void DrawBar(Rect rect, float value, Color color, string text)
    {
        Fill(rect, new Color(0.55f, 0.51f, 0.44f));
        Rect inner = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
        Fill(inner, new Color(0.10f, 0.09f, 0.11f));
        Fill(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(value), inner.height), color);
        GUI.Label(rect, text, barStyle);
    }

    void Fill(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void Quit()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
