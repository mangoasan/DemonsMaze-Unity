using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public CharacterData data;
    public int health;
    public float stamina = 100;
    public bool Dead => health <= 0;
    public bool Attacking => attackTimer > 0;
    public bool manualControl;
    public Vector2 manualMovement;
    public Vector2 facing = Vector2.down;
    public int hitsDealt;
    SpriteRenderer view;
    Sprite[] attackFrames;
    CharacterAnimation animation;
    float lookVelocity;
    float attackTimer;
    float dodgeTimer;
    float invulnerable;
    float recovery;
    float deathTimer;
    bool hitDone;
    bool alternate;
    Vector2 dodgeDirection;
    GameManager game;

    public void Setup(CharacterData character, GameManager manager)
    {
        data = character;
        game = manager;
        health = data.health;
        view = gameObject.AddComponent<SpriteRenderer>();
        view.sprite = data.idle;
        view.sortingOrder = 10;
        view.spriteSortPoint = SpriteSortPoint.Pivot;
        animation = new CharacterAnimation(data, view);
    }

    void Update()
    {
        if (game.Paused) return;
        float dt = Time.deltaTime;
        if (Dead)
        {
            view.sprite = data.death;
            view.color = Color.white;
            deathTimer += dt;
            if (deathTimer > 1.8f && !manualControl) game.ShowMenu();
            return;
        }
        invulnerable -= dt;
        recovery -= dt;
        if (recovery <= 0) stamina = Mathf.Min(100, stamina + dt * 30);
        Vector2 input = manualMovement.normalized;
        if (!manualControl)
        {
            input = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)).normalized;
            if (!Attacking && dodgeTimer <= 0)
            {
                Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                if (Vector2.Distance(mouse, transform.position) > 0.05f)
                {
                    Vector2 direction = mouse - (Vector2)transform.position;
                    float angle = Mathf.SmoothDampAngle(Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, ref lookVelocity, 0.065f);
                    facing = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                }
            }
            if (Input.GetMouseButtonDown(0)) BeginAttack();
            if (Input.GetKeyDown(KeyCode.Space)) BeginDodge(input.sqrMagnitude > 0 ? input : facing);
        }
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + 90);
        Vector2 previousPosition = transform.position;
        if (dodgeTimer > 0)
        {
            dodgeTimer -= dt;
            Move(dodgeDirection * 9 * dt);
        }
        else
        {
            Move(input * data.speed * (Attacking ? 0.3f : 1) * dt);
        }
        if (Attacking)
        {
            attackTimer -= dt;
            float progress = 1 - Mathf.Max(0, attackTimer) / data.attackTime;
            float frameProgress = Mathf.SmoothStep(0, 1, progress);
            view.sprite = attackFrames[Mathf.Min(attackFrames.Length - 1, Mathf.FloorToInt(frameProgress * attackFrames.Length))];
            if (!hitDone && progress >= 0.5f)
            {
                hitDone = true;
                Strike();
            }
            if (attackTimer <= 0) view.sprite = data.idle;
        }
        else if (dodgeTimer <= 0)
        {
            float movement = dt > 0 ? Vector2.Distance(previousPosition, transform.position) / (data.speed * dt) : 0;
            animation.Walk(movement, dt);
        }
        else view.sprite = data.idle;
        view.color = invulnerable > 0 && Mathf.Sin(Time.time * 55) > 0 ? new Color(1, 0.55f, 0.5f) : Color.white;
    }

    public void Move(Vector2 delta)
    {
        transform.position = game.maze.Move(transform.position, delta);
    }

    public bool BeginAttack()
    {
        if (Dead || game.Paused || Attacking || dodgeTimer > 0 || stamina < data.staminaCost) return false;
        stamina -= data.staminaCost;
        recovery = 0.6f;
        attackTimer = data.attackTime;
        hitDone = false;
        animation.Reset();
        attackFrames = alternate && data.secondAttack.Length > 0 ? data.secondAttack : data.attack;
        alternate = !alternate;
        return true;
    }

    void Strike()
    {
        if (data.ranged)
        {
            Projectile.Create(game, (Vector2)transform.position + facing * 0.5f, facing, data);
            return;
        }
        foreach (Demon enemy in game.enemies.ToArray())
        {
            if (enemy == null || enemy.Dead) continue;
            Vector2 difference = enemy.transform.position - transform.position;
            if (difference.magnitude <= data.reach && Vector2.Dot(difference.normalized, facing) > 0.2f && game.maze.ClearLine(transform.position, enemy.transform.position))
            {
                enemy.TakeDamage(data.damage, facing * 0.23f);
                hitsDealt++;
            }
        }
    }

    public bool BeginDodge(Vector2 direction)
    {
        if (Dead || game.Paused || Attacking || dodgeTimer > 0 || stamina < 28) return false;
        stamina -= 28;
        recovery = 0.7f;
        dodgeTimer = 0.18f;
        invulnerable = 0.22f;
        dodgeDirection = direction.normalized;
        animation.Reset();
        return true;
    }

    public void TakeDamage(int damage)
    {
        if (Dead || game.Paused || invulnerable > 0) return;
        health = Mathf.Max(0, health - damage);
        invulnerable = 0.5f;
        if (Dead)
        {
            attackTimer = 0;
            dodgeTimer = 0;
            view.sprite = data.death;
        }
    }
}
