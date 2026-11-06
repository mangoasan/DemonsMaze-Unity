using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MazeGenerator))]
public class GameManager : MonoBehaviour
{
    public const int RequiredRunes = 3;
    public const float BossIntroDuration = 3.4f;
    public static RunProgress CurrentRun { get; private set; }
    static bool warpOnArrival;

    public MazeGenerator maze;
    public PlayerController player;
    public List<Demon> enemies = new();
    public Transform actors;
    public Camera gameCamera;
    public bool Paused => paused || CheatsOpen || Shop != null || IntroducingBoss;
    public bool CheatsOpen { get; private set; }
    public WorldLandmark Shop { get; private set; }
    public bool IntroducingBoss { get; private set; }
    public float BossIntroTime { get; private set; }
    public bool Finishing => finishing;
    public bool InCitadel => citadel;
    public RunProgress Run { get; private set; }
    public Demon Boss { get; private set; }
    public WorldLandmark Nearby { get; private set; }

    [SerializeField] bool citadel;
    readonly List<WorldLandmark> landmarks = new();
    readonly List<WorldLandmark> springs = new();
    readonly List<Demon> gateGuards = new();
    EnemySquad squad;
    MazeCamera mazeCamera;
    int[,] soundField;
    float worldTimer;
    bool paused;
    bool gateOpened;
    bool entering;
    bool finishing;
    float finishTimer;

    public static void LoadGame(CharacterData character)
    {
        CurrentRun = new RunProgress(character, Random.Range(1, int.MaxValue));
        warpOnArrival = false;
        SceneManager.LoadScene("DemonsMaze");
    }

    public static void OpenMenu()
    {
        CurrentRun = null;
        warpOnArrival = false;
        Time.timeScale = 1;
        SceneManager.LoadScene("MainMenu");
    }

    void Start()
    {
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 1;
        maze = GetComponent<MazeGenerator>();
        mazeCamera = gameCamera.GetComponent<MazeCamera>();
        CurrentRun ??= new RunProgress(CharacterData.LoadPlayerClasses()[0], Random.Range(1, int.MaxValue));
        Run = CurrentRun;
        maze.Generate(Run.Seed, citadel: citadel);
        actors = new GameObject("Actors").transform;
        actors.SetParent(transform, false);
        GameObject hero = new GameObject(Run.Character.title);
        hero.transform.SetParent(actors, false);
        hero.transform.position = maze.spawn;
        player = hero.AddComponent<PlayerController>();
        player.Setup(Run.Character, this);
        mazeCamera.SetEffectSource(player);
        mazeCamera.Follow(hero.transform);
        squad = new EnemySquad(this);
        SetPaused(false);
        if (citadel) PopulateCitadel();
        else PopulateWorld();
        GameMusic.Play(gameObject, citadel ? "boss" : "world", citadel ? 0.30f : 0.40f);
        if (citadel && warpOnArrival) ArriveAtBoss();
        RefreshWorld();
    }

    void PopulateWorld()
    {
        var random = new System.Random(maze.seed);
        var reserved = new HashSet<MazeRoom> { maze.Rooms[0] };
        AddLandmark(Landmark.Chest, maze.spawn + Vector2.left * 1.3f, Potion.Guard);
        AddLandmark(Landmark.Spring, maze.spawn + Vector2.down * 1.35f);
        AddLandmark(Landmark.Altar, maze.spawn + Vector2.down * 1.35f + Vector2.right * 1.1f, Potion.Guard, 1);

        MazeRoom watch = ReserveRoom(reserved, new Vector2(0.40f, 0.22f), RoomKind.Watch);
        MazeRoom crypt = ReserveRoom(reserved, new Vector2(0.34f, 0.63f), RoomKind.Crypt);
        MazeRoom keep = ReserveRoom(reserved, new Vector2(0.70f, 0.60f), RoomKind.Keep);
        MazeRoom exit = ReserveRoom(reserved, new Vector2(0.88f, 0.83f), RoomKind.Keep);
        PlaceRune(watch, EnemyKind.Archer, EnemyKind.Spearman, EnemyKind.Archer);
        PlaceRune(crypt, EnemyKind.Spirit, EnemyKind.Demon, EnemyKind.Spirit);
        PlaceRune(keep, EnemyKind.Spearman, EnemyKind.Demon, EnemyKind.Succubus);
        AddLandmark(Landmark.Gate, maze.World(exit.Center));

        Vector2[] refuges = { new Vector2(0.25f, 0.44f), new Vector2(0.69f, 0.32f), new Vector2(0.65f, 0.81f) };
        for (int i = 0; i < refuges.Length; i++)
        {
            MazeRoom refuge = ReserveRoom(reserved, refuges[i], RoomKind.Courtyard);
            if (refuge == null) break;
            Vector2 center = maze.World(refuge.Center);
            AddLandmark(Landmark.Spring, center);
            AddLandmark(Landmark.Altar, center + Vector2.right * 1.1f, i == 0 ? Potion.Fury : Supply(random),
                random.Next(4) == 0 ? 2 : 1);
        }
        PlaceFind(reserved, new Vector2(0.47f, 0.49f), Landmark.Chest, Potion.Heal);
        PlaceFind(reserved, new Vector2(0.36f, 0.32f), Landmark.Vitality);
        PlaceFind(reserved, new Vector2(0.67f, 0.70f), Landmark.Vitality);
        PlaceFind(reserved, new Vector2(0.22f, 0.67f), Landmark.Power);
        PlaceFind(reserved, new Vector2(0.72f, 0.49f), Landmark.Power);

        var rareRooms = new List<MazeRoom>();
        foreach (MazeRoom room in maze.Rooms)
            if (!reserved.Contains(room) && (room.Kind == RoomKind.Crypt || room.Kind == RoomKind.Keep) &&
                Vector2.Distance(maze.World(room.Center), maze.spawn) > 22)
                rareRooms.Add(room);

        for (int i = 0; i < 4 && rareRooms.Count > 0; i++)
        {
            int index = random.Next(rareRooms.Count);
            MazeRoom den = rareRooms[index];
            rareRooms.RemoveAt(index);
            reserved.Add(den);
            SpawnEnemy(maze.World(den.Center), EnemyKind.Succubus);
        }

        foreach (MazeRoom room in maze.Rooms)
        {
            if (reserved.Contains(room)) continue;
            Vector2 center = maze.World(room.Center);
            float fromStart = Vector2.Distance(center, maze.spawn);
            if (fromStart < 6) continue;
            if (random.NextDouble() < 0.23)
                AddLandmark(Landmark.Chest, center + Vector2.down * 1.45f, Supply(random));
            if (random.NextDouble() > 0.61) continue;
            EnemyKind enemy = room.Kind switch
            {
                RoomKind.Watch => EnemyKind.Archer,
                RoomKind.Crypt => EnemyKind.Spirit,
                RoomKind.Keep => EnemyKind.Spearman,
                _ => EnemyKind.Demon
            };
            SpawnEnemy(center, enemy);
            if (fromStart > 20 && random.Next(4) == 0)
                SpawnEnemy(center + Vector2.right * 1.5f, enemy == EnemyKind.Archer ? EnemyKind.Spearman : EnemyKind.Demon);
        }

        foreach (Vector2Int junction in maze.junctions)
        {
            Vector2 position = maze.World(junction);
            if (random.NextDouble() < 0.09 && Vector2.Distance(position, maze.spawn) > 6 &&
                !landmarks.Exists(item => Vector2.Distance(item.transform.position, position) < 2.5f) &&
                !enemies.Exists(enemy => Vector2.Distance(enemy.transform.position, position) < 3))
                SpawnEnemy(position, EnemyKind.Demon);
        }
    }

    static Potion Supply(System.Random random) => random.Next(2) == 0 ? Potion.Guard : Potion.Fury;

    MazeRoom ReserveRoom(HashSet<MazeRoom> reserved, Vector2 normalized, RoomKind kind)
    {
        Vector2 target = normalized * maze.Size;
        MazeRoom best = null;
        float score = float.PositiveInfinity;
        foreach (MazeRoom room in maze.Rooms)
        {
            if (reserved.Contains(room)) continue;
            float cost = Vector2.Distance(room.Center, target) + (room.Kind == kind ? 0 : maze.Size);
            if (cost >= score) continue;
            best = room;
            score = cost;
        }
        reserved.Add(best);
        return best;
    }

    void PlaceFind(HashSet<MazeRoom> reserved, Vector2 location, Landmark kind, Potion reward = Potion.Guard)
    {
        MazeRoom room = ReserveRoom(reserved, location, RoomKind.Crypt);
        if (room == null) return;
        Vector2 center = maze.World(room.Center);
        AddLandmark(kind, center + Vector2.down * 0.9f, reward);
        SpawnEnemy(center + Vector2.up * 1.35f, EnemyKind.Demon);
    }

    void PlaceRune(MazeRoom room, params EnemyKind[] kinds)
    {
        AddLandmark(Landmark.Rune, maze.World(room.Center));
        PlaceGuards(room.Center, kinds);
    }

    void PopulateCitadel()
    {
        gateGuards.AddRange(PlaceGuards(maze.Rooms[0].Center, EnemyKind.Archer, EnemyKind.Spearman, EnemyKind.Spirit));
        AddLandmark(Landmark.Chest, maze.spawn + Vector2.down * 1.2f, Potion.Guard);
        Boss = SpawnEnemy(maze.World(maze.Throne) + Vector2.right * 2, EnemyKind.Boss);
        Boss.sleeping = true;
        for (int i = 0; i < Run.Companions.Count; i++)
            SpawnEnemy(maze.spawn + Vector2.up * (i == 0 ? 1 : -1), Run.Companions[i])?.Charm();
    }

    List<Demon> PlaceGuards(Vector2Int cell, params EnemyKind[] kinds)
    {
        Vector2 center = maze.World(cell);
        Vector2[] offsets = { Vector2.up * 1.65f, Vector2.right * 1.2f, Vector2.left * 1.2f };
        var guards = new List<Demon>(kinds.Length);
        for (int i = 0; i < kinds.Length; i++)
        {
            Demon guard = SpawnEnemy(center + offsets[i], kinds[i]);
            if (guard != null) guards.Add(guard);
        }
        return guards;
    }

    void AddLandmark(Landmark kind, Vector2 position, Potion reward = Potion.Guard, int stock = 0)
    {
        WorldLandmark item = WorldLandmark.Create(this, kind, position, reward, stock);
        landmarks.Add(item);
        if (kind == Landmark.Spring) springs.Add(item);
    }

    public Demon SpawnEnemy(Vector2 position, EnemyKind kind)
    {
        if (!maze.CanStand(position) || IsSheltered(position) ||
            enemies.Exists(enemy => Vector2.Distance(enemy.transform.position, position) < 0.72f))
            return null;
        GameObject obj = new GameObject(kind.ToString());
        obj.transform.SetParent(actors, false);
        obj.transform.position = position;
        Demon demon = obj.AddComponent<Demon>();
        demon.Setup(this, kind);
        enemies.Add(demon);
        return demon;
    }

    public void EnemyKilled(Demon enemy)
    {
        if (!enemy.Allied)
        {
            Run.Kills++;
            if (!citadel) WorldLoot.Drop(this, enemy.transform.position, enemy.SoulValue);
            player.OnKill();
        }
        if (enemy == Boss) BeginFinish(true);
    }

    public bool IsSheltered(Vector2 position)
    {
        foreach (WorldLandmark spring in springs)
            if (((Vector2)spring.transform.position - position).sqrMagnitude < 1.35f * 1.35f)
                return true;
        return false;
    }

    public void EmitNoise(Vector2 position, float radius)
    {
        if (radius <= 0 || player == null || !player.Fighting && IsSheltered(position)) return;
        int limit = Mathf.CeilToInt(radius * 1.3f / maze.tileSize) + 1;
        soundField = maze.BuildDistances(position, limit, soundField, sound: true);
        foreach (Demon enemy in enemies)
        {
            if (enemy == null || enemy.Dead || enemy.Allied || enemy.sleeping || enemy.Kind == EnemyKind.Boss) continue;
            Vector2Int cell = maze.TileAt(enemy.transform.position);
            int steps = soundField[cell.x, cell.y];
            if (steps > 0 && (steps - 1) * maze.tileSize <= radius * enemy.Hearing)
                enemy.Hear(position);
        }
    }

    public void BeginFinish(bool victory)
    {
        if (finishing) return;
        finishing = true;
        finishTimer = victory ? 2.4f : 1.5f;
        Run.Victory = victory;
    }

    void Update()
    {
        if (player == null || entering) return;
        if (IntroducingBoss)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) FinishBossIntro();
            else AdvanceBossIntro(Time.unscaledDeltaTime);
            return;
        }
        if (!finishing && !player.Dead && Shop == null && Input.GetKeyDown(KeyCode.F1))
        {
            ToggleCheats();
            return;
        }
        if (CheatsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ToggleCheats();
            return;
        }
        if (Shop != null)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)) CloseShop();
            return;
        }
        if (!finishing && Input.GetKeyDown(KeyCode.Escape)) SetPaused(!Paused);
        if (Paused) return;
        if (finishing)
        {
            finishTimer -= Time.deltaTime;
            if (finishTimer <= 0) SceneManager.LoadScene("Result");
            return;
        }
        if (Input.GetKeyDown(KeyCode.E)) Nearby?.Use();
        worldTimer -= Time.deltaTime;
        if (worldTimer <= 0)
        {
            RefreshWorld();
            worldTimer = 0.22f;
        }
    }

    void RefreshWorld()
    {
        Vector2 position = player.transform.position;
        if (citadel && !gateOpened && !gateGuards.Exists(guard => guard != null && !guard.Dead && !guard.Allied))
        {
            gateOpened = true;
            maze.SetCitadelGate(true);
        }
        if (citadel && gateOpened && Boss.sleeping && !IntroducingBoss && position.x > maze.World(maze.Gate).x + 1.5f)
            BeginBossIntro();

        Nearby = null;
        float closest = 1.25f;
        foreach (WorldLandmark item in landmarks)
        {
            float distance = Vector2.Distance(item.transform.position, position);
            if (item.Used || distance >= closest || !maze.ClearLine(item.transform.position, position)) continue;
            Nearby = item;
            closest = distance;
        }
        squad.Refresh();
    }

    public Vector2 Pursuit(Demon enemy) => squad.Direction(enemy);

    public bool EnterCitadel()
    {
        if (citadel || entering || Run.Runes < RequiredRunes) return false;
        LoadCitadel(false);
        return true;
    }

    void LoadCitadel(bool warp)
    {
        entering = true;
        warpOnArrival = warp;
        Run.Health = player.health;
        Run.Companions.Clear();
        foreach (Demon enemy in enemies)
            if (enemy != null && !enemy.Dead && enemy.Allied) Run.Companions.Add(enemy.Kind);
        Time.timeScale = 1;
        SceneManager.LoadScene("Citadel");
    }

    void ArriveAtBoss()
    {
        warpOnArrival = false;
        gateOpened = true;
        maze.SetCitadelGate(true);
        player.transform.position = maze.World(maze.Throne) + Vector2.left * 2;
        if (Boss.sleeping) BeginBossIntro();
        else mazeCamera.Follow(player.transform);
    }

    void BeginBossIntro()
    {
        IntroducingBoss = true;
        BossIntroTime = 0;
        mazeCamera.LookAt(player.transform.position);
        SyncPause();
    }

    void AdvanceBossIntro(float dt)
    {
        BossIntroTime += dt;
        if (BossIntroTime >= BossIntroDuration)
        {
            FinishBossIntro();
            return;
        }
        float pan = BossIntroTime < 1.1f
            ? Mathf.SmoothStep(0, 1, BossIntroTime / 1.1f)
            : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.2f, BossIntroDuration, BossIntroTime));
        mazeCamera.LookAt(Vector2.Lerp(player.transform.position, Boss.transform.position, pan));
    }

    void FinishBossIntro()
    {
        IntroducingBoss = false;
        Boss.sleeping = false;
        mazeCamera.Follow(player.transform);
        SyncPause();
    }

    public void ToggleCheats()
    {
        if (player.Dead || finishing || entering || IntroducingBoss || Shop != null) return;
        CheatsOpen = !CheatsOpen;
        SyncPause();
    }

    public void GrantRunes()
    {
        if (!CheatsOpen || citadel) return;
        Run.Runes = RequiredRunes;
        foreach (WorldLandmark item in landmarks)
            if (item.Kind == Landmark.Rune) item.HideRune();
    }

    public void WarpToBoss()
    {
        if (!CheatsOpen || entering) return;
        CheatsOpen = false;
        paused = false;
        SyncPause();
        if (citadel) ArriveAtBoss();
        else LoadCitadel(true);
    }

    public void OpenShop(WorldLandmark item)
    {
        if (Paused || finishing || entering) return;
        Shop = item;
        SyncPause();
    }

    public void CloseShop()
    {
        Shop = null;
        SyncPause();
    }

    public void SetPaused(bool value)
    {
        paused = value;
        SyncPause();
    }

    void SyncPause() => Time.timeScale = Paused ? 0 : 1;

    public void ShowMenu() => OpenMenu();

    public Vector2 MoveActor(Transform actor, Vector2 delta, float radius, bool stopOnContact = false, bool hostile = false)
    {
        Vector2 position = actor.position;
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.1f));
        Vector2 step = delta / steps;
        for (int i = 0; i < steps; i++)
        {
            bool contact = false;
            Vector2 horizontal = position + new Vector2(step.x, 0);
            if (step.x != 0 && maze.CanStand(horizontal, radius) && (!hostile || !IsSheltered(horizontal)))
            {
                contact = TouchesBody(actor, position, horizontal, radius);
                if (!contact) position = horizontal;
            }
            Vector2 vertical = position + new Vector2(0, step.y);
            if (step.y != 0 && maze.CanStand(vertical, radius) && (!hostile || !IsSheltered(vertical)))
            {
                bool blocked = TouchesBody(actor, position, vertical, radius);
                contact |= blocked;
                if (!blocked) position = vertical;
            }
            if (contact && stopOnContact) break;
        }
        return position;
    }

    bool TouchesBody(Transform actor, Vector2 oldPosition, Vector2 position, float radius)
    {
        if (player != null && !player.Dead && player.transform != actor &&
            Overlaps(player.transform.position, oldPosition, position, radius + PlayerController.Radius))
            return true;
        foreach (Demon enemy in enemies)
        {
            if (enemy == null || enemy.Dead || enemy.transform == actor) continue;
            if (Overlaps(enemy.transform.position, oldPosition, position, radius + enemy.Radius)) return true;
        }
        return false;
    }

    static bool Overlaps(Vector2 body, Vector2 previous, Vector2 next, float reach)
    {
        float after = (body - next).sqrMagnitude;
        return after < reach * reach && after < (body - previous).sqrMagnitude;
    }
}
