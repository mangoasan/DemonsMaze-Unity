using UnityEngine;

public enum EnemyKind
{
    Demon,
    Archer,
    Spearman,
    Spirit,
    Succubus,
    Boss
}

public class Demon : MonoBehaviour
{
    public int health;
    public bool sleeping;
    public bool Dead => health <= 0;
    public bool Allied { get; private set; }
    public EnemyKind Kind { get; private set; }
    public float Radius => Kind == EnemyKind.Boss ? 0.43f : 0.29f;
    public int MaxHealth { get; private set; }
    public int SoulValue => Kind is EnemyKind.Succubus or EnemyKind.Spirit ? 2 : 1;
    public float Hearing => Kind switch
    {
        EnemyKind.Archer => 0.78f,
        EnemyKind.Spearman => 0.9f,
        EnemyKind.Spirit => 1.20f,
        EnemyKind.Succubus => 1.30f,
        _ => 1
    };

    GameManager game;
    CharacterData data;
    SpriteRenderer view;
    SpriteRenderer trail;
    CharacterAnimation animation;
    Transform target;
    Demon rival;
    Sprite[] frames;
    Vector2 strikeDirection;
    float duration;
    float attackTime;
    float cooldown;
    float hurt;
    float fear;
    float corpseTime;
    int attacks;
    int move;
    float searchTime;
    Vector2 lastContact;
    int[,] searchRoute;
    Vector2Int routedContact;
    bool routeReady;
    bool hitDone;

    public void Setup(GameManager manager, EnemyKind kind)
    {
        game = manager;
        Kind = kind;
        data = Resources.Load<CharacterData>(kind == EnemyKind.Demon ? "Classes/Demon" : "Enemies/" + kind);
        MaxHealth = health = data.health;
        if (kind == EnemyKind.Boss) transform.localScale = Vector3.one * 1.24f;
        view = gameObject.AddComponent<SpriteRenderer>();
        view.sprite = data.idle;
        view.sortingOrder = 10;
        view.spriteSortPoint = SpriteSortPoint.Pivot;
        animation = new CharacterAnimation(data, view);
        if (kind == EnemyKind.Succubus)
        {
            GameObject effect = new GameObject("Dash Trail");
            effect.transform.SetParent(transform, false);
            effect.transform.localPosition = new Vector3(0, 0.62f, 0);
            trail = effect.AddComponent<SpriteRenderer>();
            trail.sortingOrder = 8;
            trail.enabled = false;
        }
    }

    void Update()
    {
        if (game.Paused) return;
        float dt = Time.deltaTime;
        if (trail != null) trail.enabled = false;
        searchTime -= dt;
        if (Dead)
        {
            corpseTime += dt;
            view.color = new Color(0.72f, 0.69f, 0.68f, Mathf.Clamp01(10 - corpseTime));
            if (corpseTime > 10) Destroy(gameObject);
            return;
        }
        if (sleeping || game.player == null || game.player.Dead) return;
        if (!Allied && searchTime <= 0 && attackTime <= 0 &&
            ((Vector2)(game.player.transform.position - transform.position)).sqrMagnitude > 196)
            return;
        cooldown -= dt;
        hurt -= dt;
        fear -= dt;
        view.color = Allied ? new Color(0.56f, 1, 0.90f) : Color.white;
        if (fear > 0)
        {
            view.color = new Color(0.79f, 0.71f, 1);
            animation.Walk(0, dt);
            return;
        }
        if (hurt > 0)
        {
            view.color = new Color(1, 0.54f, 0.52f);
            return;
        }
        if (attackTime > 0)
        {
            AdvanceAttack(dt);
            return;
        }
        if (!FindTarget())
        {
            Vector2 travel = Vector2.zero;
            if (Allied && Vector2.Distance(transform.position, game.player.transform.position) > 1.75f)
                travel = game.Pursuit(this);
            else if (!Allied && searchTime > 0)
                travel = Investigate();
            MoveAndAnimate(travel, dt);
            if (travel.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Rotation(travel), dt * 600);
            return;
        }

        Vector2 delta = target.position - transform.position;
        float distance = delta.magnitude;
        if (distance > 12f)
        {
            animation.Walk(0, dt);
            return;
        }
        int next = Kind == EnemyKind.Boss ? attacks % 4 : 0;
        float reach = BeginReach(next);
        bool clear = distance <= reach && game.maze.ClearLine(transform.position, target.position);
        if (clear && cooldown <= 0)
        {
            BeginAttack(delta, next);
            return;
        }

        Vector2 direction = Vector2.zero;
        if (Kind == EnemyKind.Archer && clear && distance < 2.8f)
            direction = -delta.normalized;
        else if (!clear || distance > StopRange())
            direction = rival != null ? (game.maze.ClearMovementLine(transform.position, target.position) ? delta.normalized : Vector2.zero)
                : game.Pursuit(this);
        MoveAndAnimate(direction, dt);
        if (delta.sqrMagnitude > 0.01f)
        {
            Quaternion rotation = Rotation(delta);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, dt * 600);
        }
    }

    bool FindTarget()
    {
        rival = null;
        target = null;
        float best = 8;
        if (SeesHero())
        {
            target = game.player.transform;
            best = Vector2.Distance(target.position, transform.position);
            Hear(target.position);
        }
        foreach (Demon enemy in game.enemies)
        {
            if (enemy == null || enemy.Dead || enemy.sleeping || enemy.Allied == Allied) continue;
            float distance = Vector2.Distance(enemy.transform.position, transform.position);
            if (distance >= best || !game.maze.ClearLine(transform.position, enemy.transform.position)) continue;
            best = distance;
            rival = enemy;
            target = enemy.transform;
        }
        return target != null;
    }

    public bool SeesHero()
    {
        if (Allied || Dead || sleeping || game.player.Invisible) return false;
        if (Kind == EnemyKind.Boss)
            return game.maze.ClearLine(transform.position, game.player.transform.position);
        if (!game.player.Fighting) return false;
        Vector2 delta = game.player.transform.position - transform.position;
        return delta.sqrMagnitude <= 100 &&
            (searchTime > 0 || delta.sqrMagnitude < 2.25f ||
                Vector2.Dot(transform.rotation * Vector2.down, delta.normalized) > 0.1f) &&
            game.maze.ClearLine(transform.position, game.player.transform.position);
    }

    public void Hear(Vector2 position)
    {
        lastContact = position;
        searchTime = 3.8f;
    }

    Vector2 Investigate()
    {
        Vector2 position = transform.position;
        if ((lastContact - position).sqrMagnitude < 0.08f)
        {
            searchTime = 0;
            return Vector2.zero;
        }
        if (game.maze.ClearMovementLine(position, lastContact)) return (lastContact - position).normalized;
        Vector2Int cell = game.maze.TileAt(lastContact);
        if (!routeReady || cell != routedContact)
        {
            searchRoute = game.maze.BuildDistances(lastContact, 42, searchRoute);
            routedContact = cell;
            routeReady = true;
        }
        return game.maze.ChaseDirection(position, lastContact, searchRoute);
    }

    void MoveAndAnimate(Vector2 direction, float dt)
    {
        Vector2 previous = transform.position;
        if (direction.sqrMagnitude > 0.001f)
            transform.position = game.MoveActor(transform, direction * dt * data.speed, Radius, hostile: !Allied);
        animation.Walk(Vector2.Distance(previous, transform.position) / Mathf.Max(0.001f, data.speed * dt), dt);
    }

    float BeginReach(int next) => Kind switch
    {
        EnemyKind.Succubus => 3.35f,
        EnemyKind.Boss when next == 0 => 4.5f,
        EnemyKind.Boss when next == 3 => 8,
        _ => data.reach
    };

    float StopRange() => Kind switch
    {
        EnemyKind.Archer => 6.5f,
        EnemyKind.Spearman => 1.5f,
        _ => 0.75f
    };

    void BeginAttack(Vector2 delta, int next)
    {
        move = next;
        frames = next switch
        {
            1 => data.secondAttack,
            2 => data.thirdAttack,
            3 => data.magic,
            _ => Kind == EnemyKind.Demon && attacks % 2 == 1 ? data.secondAttack : data.attack
        };
        attacks++;
        duration = data.attackTime;
        attackTime = duration;
        hitDone = false;
        strikeDirection = delta.normalized;
        if (delta.sqrMagnitude > 0.001f) transform.rotation = Rotation(delta);
        animation.Reset();
        animation.Attack(frames, 0);
    }

    void AdvanceAttack(float dt)
    {
        float previous = 1 - attackTime / duration;
        attackTime -= dt;
        float progress = Mathf.Clamp01(1 - attackTime / duration);
        float impact = Kind == EnemyKind.Spirit ? 0.88f : Kind == EnemyKind.Archer ? 0.8f : 0.75f;
        bool charge = Kind == EnemyKind.Succubus || Kind == EnemyKind.Boss && move == 0;
        float pose = progress;
        if (Kind == EnemyKind.Spirit)
            pose = progress < impact
                ? Mathf.Lerp(0.12f, 0.52f, Mathf.InverseLerp(0.60f, impact, progress))
                : Mathf.Lerp(0.52f, 0.99f, Mathf.InverseLerp(impact, 1, progress));
        animation.Attack(frames, pose);
        if (progress < impact)
            view.color = Kind == EnemyKind.Boss && move == 3 ? new Color(0.45f, 1, 1) : new Color(1.3f, 0.72f, 0.56f);

        if (charge)
        {
            float portion = Mathf.Max(0, Mathf.Min(progress, impact) - Mathf.Max(previous, 0.48f)) / (impact - 0.48f);
            float length = Kind == EnemyKind.Boss ? 3.35f : 2.35f;
            Vector2 before = transform.position;
            transform.position = game.MoveActor(transform, strikeDirection * (portion * length), Radius, true, !Allied);
            if (trail != null && data.dashTrail.Length > 0 && Vector2.Distance(before, transform.position) > 0.001f)
            {
                int index = Mathf.Min(data.dashTrail.Length - 1,
                    (int)(Mathf.InverseLerp(0.48f, impact, progress) * data.dashTrail.Length));
                trail.sprite = data.dashTrail[index];
                trail.enabled = true;
            }
        }
        if (!hitDone && progress >= impact)
        {
            hitDone = true;
            AttackTarget();
        }
        if (attackTime <= 0)
            cooldown = Kind == EnemyKind.Boss ? 0.70f : Kind == EnemyKind.Archer ? 1.15f : 0.65f;
    }

    void AttackTarget()
    {
        int damage = data.damage;
        if (Kind == EnemyKind.Archer || Kind == EnemyKind.Boss && move == 3)
        {
            string sprite = Kind == EnemyKind.Archer ? "enemy_arrow" : "boss_orb";
            Projectile.CreateEnemy(game, this, strikeDirection, damage, sprite);
            if (Kind == EnemyKind.Boss)
            {
                Projectile.CreateEnemy(game, this, Quaternion.Euler(0, 0, -18) * strikeDirection, damage, sprite);
                Projectile.CreateEnemy(game, this, Quaternion.Euler(0, 0, 18) * strikeDirection, damage, sprite);
            }
            return;
        }
        if (target == null || rival == null && game.player.Invisible) return;
        Vector2 delta = target.position - transform.position;
        float reach = data.reach + (Kind == EnemyKind.Boss && move != 0 ? 0.45f : 0.15f);
        if (delta.magnitude > reach || Vector2.Dot(delta.normalized, strikeDirection) < 0.18f ||
            !game.maze.ClearLine(transform.position, target.position)) return;
        if (rival != null) rival.TakeDamage(damage, strikeDirection * 0.2f);
        else game.player.TakeDamage(damage);
    }

    public void Frighten(float seconds)
    {
        if (Dead || Allied || Kind == EnemyKind.Boss) return;
        fear = seconds;
        attackTime = 0;
        cooldown = 0.4f;
    }

    public bool Charm()
    {
        if (Dead || Allied || Kind == EnemyKind.Boss) return false;
        Allied = true;
        fear = 0;
        attackTime = 0;
        target = null;
        cooldown = 0.25f;
        return true;
    }

    public void TakeDamage(int amount, Vector2 push)
    {
        if (Dead) return;
        health = Mathf.Max(0, health - amount);
        if (Kind != EnemyKind.Boss)
        {
            hurt = 0.16f;
            attackTime = 0;
            transform.position = game.MoveActor(transform, push, Radius, hostile: !Allied);
        }
        if (!Dead) return;
        view.sprite = data.death;
        view.sortingOrder = 1;
        attackTime = 0;
        game.EnemyKilled(this);
    }

    static Quaternion Rotation(Vector2 dir) =>
        Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90);

    void OnDestroy()
    {
        if (game != null) game.enemies.Remove(this);
    }
}
