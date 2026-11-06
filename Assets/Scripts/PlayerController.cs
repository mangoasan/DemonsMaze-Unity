using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public const float Radius = 0.28f;
    public const float ShieldDuration = 8;
    public CharacterData data;
    public int health;
    public float stamina = 100;
    public bool Dead => health <= 0;
    public bool Attacking => attackTimer > 0;
    public bool Invisible => skills.Veil > 0;
    public bool Fighting => combatTimer > 0;
    public bool manualControl;
    public Vector2 manualMovement;
    public Vector2 facing = Vector2.down;
    public Vector2 TravelDirection { get; private set; }
    public string AbilityStatus => skills.Status;
    public bool Empowered => skills.Rage > 0 || game.Run.Fury > 0;
    public bool FearActive => skills.Fear > 0;
    public int hitsDealt;
    SpriteRenderer view;
    Sprite[] attackFrames;
    CharacterAnimation animation;
    CharacterAnimation hiddenAnimation;
    CharacterData hiddenData;
    PlayerSkills skills;
    float lookVelocity;
    float attackTimer;
    float dodgeTimer;
    float invulnerable;
    float recovery;
    float combatTimer;
    float stepDistance;
    bool hitDone;
    bool alternate;
    Vector2 dodgeDirection;
    GameManager game;

    float Noise => data.name switch
    {
        "Knight" => 1.22f,
        "Alchemist" => 1.08f,
        "Samurai" => 0.94f,
        "Mage" => 0.84f,
        _ => Invisible ? 0.30f : 0.64f
    };

    public void Setup(CharacterData character, GameManager manager)
    {
        data = character;
        game = manager;
        health = game.Run.Health;
        view = gameObject.AddComponent<SpriteRenderer>();
        view.sprite = data.idle;
        view.sortingOrder = 10;
        view.spriteSortPoint = SpriteSortPoint.Pivot;
        animation = new CharacterAnimation(data, view);
        skills = new PlayerSkills(this, game, data);
        if (data.name == "Rogue")
        {
            hiddenData = Resources.Load<CharacterData>("Enemies/RogueHidden");
            hiddenAnimation = new CharacterAnimation(hiddenData, view);
        }
    }

    void Update()
    {
        if (game.Paused || Dead || game.Finishing) return;
        float dt = Time.deltaTime;
        invulnerable -= dt;
        recovery -= dt;
        combatTimer -= dt;
        game.Run.Fury = Mathf.Max(0, game.Run.Fury - dt);
        game.Run.WardTime = Mathf.Max(0, game.Run.WardTime - dt);
        if (game.Run.WardTime <= 0) game.Run.Shield = 0;
        skills.Tick(dt);
        if (recovery <= 0) stamina = Mathf.Min(100, stamina + dt * 30);

        Vector2 input = manualMovement.normalized;
        bool sneaking = !manualControl && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        if (!manualControl)
        {
            input = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)).normalized;
            Aim();
            if (Input.GetKeyDown(KeyCode.Q)) skills.Activate();
            if (Input.GetKeyDown(KeyCode.Alpha1)) UsePotion(Potion.Heal);
            if (Input.GetKeyDown(KeyCode.Alpha2)) UsePotion(Potion.Guard);
            if (Input.GetKeyDown(KeyCode.Alpha3)) UsePotion(Potion.Fury);
            if (Input.GetMouseButtonDown(0)) BeginAttack();
            if (Input.GetKeyDown(KeyCode.Space)) BeginDodge(input.sqrMagnitude > 0 ? input : facing);
        }

        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + 90);
        Vector2 previous = transform.position;
        if (dodgeTimer > 0)
        {
            dodgeTimer -= dt;
            Vector2 destination = game.MoveActor(transform, dodgeDirection * 9 * dt, Radius, true);
            if (Vector2.Dot(destination - previous, dodgeDirection) < 8.5f * dt) dodgeTimer = 0;
            transform.position = destination;
        }
        else Move(input * data.speed * (Attacking ? 0.3f : sneaking ? 0.54f : 1) * dt);

        Vector2 movement = (Vector2)transform.position - previous;
        if (movement.sqrMagnitude > 0.00001f)
        {
            TravelDirection = movement.normalized;
            if (dodgeTimer <= 0 && !Attacking)
            {
                stepDistance += movement.magnitude;
                if (stepDistance >= 0.48f)
                {
                    game.EmitNoise(transform.position, Noise * (sneaking ? 1.10f : 3.35f));
                    stepDistance %= 0.48f;
                }
            }
        }
        if (Attacking) AnimateAttack(dt);
        else if (dodgeTimer <= 0)
        {
            CharacterAnimation gait = Invisible && hiddenAnimation != null ? hiddenAnimation : animation;
            gait.Walk(movement.magnitude / Mathf.Max(0.001f, data.speed * dt), dt);
        }
        else view.sprite = Invisible && hiddenData != null ? hiddenData.idle : data.idle;

        view.color = Invisible ? new Color(1.1f, 1.2f, 1.5f, 0.73f) :
            invulnerable > 0 && Mathf.Sin(Time.time * 55) > 0 ? new Color(1, 0.55f, 0.5f) :
            skills.Rage > 0 || game.Run.Fury > 0 ? new Color(1.28f, 0.76f, 0.54f) :
            game.Run.Shield > 0 ? new Color(0.65f, 0.88f, 1) : Color.white;
    }

    void Aim()
    {
        if (Attacking || dodgeTimer > 0) return;
        Vector2 mouse = game.gameCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mouse - (Vector2)transform.position;
        if (direction.sqrMagnitude < 0.0025f) return;
        float angle = Mathf.SmoothDampAngle(Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, ref lookVelocity, 0.065f);
        facing = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }

    void AnimateAttack(float dt)
    {
        attackTimer -= dt;
        float progress = 1 - Mathf.Max(0, attackTimer) / data.attackTime;
        animation.Attack(attackFrames, Mathf.SmoothStep(0, 1, progress));
        if (!hitDone && progress >= 0.5f)
        {
            hitDone = true;
            Strike();
        }
    }

    public void Move(Vector2 delta)
    {
        transform.position = game.MoveActor(transform, delta, Radius);
    }

    public bool BeginAttack()
    {
        if (Dead || game.Paused || game.Finishing || Attacking || dodgeTimer > 0 || stamina < data.staminaCost)
            return false;
        stamina -= data.staminaCost;
        recovery = 0.6f;
        attackTimer = data.attackTime;
        combatTimer = 4.2f;
        game.EmitNoise(transform.position, 5.8f * Noise);
        hitDone = false;
        animation.Reset();
        CharacterData art = Invisible && hiddenData != null ? hiddenData : data;
        attackFrames = alternate && art.secondAttack.Length > 0 ? art.secondAttack : art.attack;
        alternate = !alternate;
        return true;
    }

    void Strike()
    {
        int baseDamage = data.damage + game.Run.Power * 4;
        float fury = game.Run.Fury > 0 ? 1.38f : 1;
        int damage = Mathf.RoundToInt(baseDamage * skills.AttackMultiplier() * fury);
        game.EmitNoise(transform.position, 4.6f * Noise);
        if (data.ranged)
        {
            Projectile.Create(game, (Vector2)transform.position + facing * 0.5f, facing, data, damage);
            return;
        }
        foreach (Demon enemy in game.enemies)
        {
            if (enemy == null || enemy.Dead || enemy.Allied) continue;
            Vector2 difference = enemy.transform.position - transform.position;
            if (difference.magnitude > data.reach || Vector2.Dot(difference.normalized, facing) <= 0.2f ||
                !game.maze.ClearLine(transform.position, enemy.transform.position)) continue;
            enemy.TakeDamage(skills.Execute(enemy) ? enemy.health : damage, facing * 0.23f);
            hitsDealt++;
        }
    }

    public bool BeginDodge(Vector2 direction)
    {
        if (Dead || game.Paused || Attacking || dodgeTimer > 0 || stamina < 28 || direction.sqrMagnitude < 0.001f)
            return false;
        stamina -= 28;
        recovery = 0.7f;
        dodgeTimer = 0.18f;
        invulnerable = 0.22f;
        dodgeDirection = direction.normalized;
        game.EmitNoise(transform.position, 4.2f * Noise);
        animation.Reset();
        return true;
    }

    public bool ActivateAbility() => !game.Paused && skills.Activate();

    public void OnKill() => skills.OnKill();

    public void Heal(int amount)
    {
        health = Mathf.Min(game.Run.MaxHealth, health + amount);
        game.Run.Health = health;
    }

    public bool UsePotion(Potion kind)
    {
        if (Dead || game.Paused || game.Run.Potions[(int)kind] == 0) return false;
        if (kind == Potion.Heal && (game.Run.FlaskUsed || health >= game.Run.MaxHealth)) return false;
        if (kind == Potion.Guard && game.Run.Shield >= 32) return false;
        game.Run.Potions[(int)kind]--;
        switch (kind)
        {
            case Potion.Heal:
                game.Run.FlaskUsed = true;
                Heal(game.Run.MaxHealth);
                break;
            case Potion.Guard:
                game.Run.Shield = 32;
                game.Run.WardTime = ShieldDuration;
                break;
            case Potion.Fury:
                game.Run.Fury = 10;
                break;
        }
        return true;
    }

    public void TakeDamage(int damage)
    {
        if (Dead || game.Paused || game.Finishing || invulnerable > 0 || !Fighting && game.IsSheltered(transform.position))
            return;
        float resistance = skills.Rage > 0 ? 0.64f : 1;
        int reduced = Mathf.Max(1, Mathf.RoundToInt(damage * resistance));
        int absorbed = Mathf.Min(reduced, Mathf.CeilToInt(game.Run.Shield));
        game.Run.Shield = Mathf.Max(0, game.Run.Shield - absorbed);
        if (game.Run.Shield <= 0) game.Run.WardTime = 0;
        health = Mathf.Max(0, health - (reduced - absorbed));
        game.Run.Health = health;
        combatTimer = Mathf.Max(combatTimer, 2.6f);
        invulnerable = 0.48f;
        if (!Dead) return;
        attackTimer = dodgeTimer = 0;
        view.sprite = data.death;
        view.sortingOrder = 1;
        view.color = Color.white;
        game.BeginFinish(false);
    }
}
