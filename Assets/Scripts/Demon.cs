using UnityEngine;

public class Demon : MonoBehaviour
{
    public int health = 65;
    public bool Dead => health <= 0;
    public bool sleeping;
    GameManager game;
    CharacterData data;
    SpriteRenderer view;
    CharacterAnimation animation;
    float attackTime;
    float cooldown;
    float hurt;
    float corpseTime;
    Vector2 strikeDirection;

    public void Setup(GameManager manager)
    {
        game = manager;
        data = Resources.Load<CharacterData>("Classes/Demon");
        view = gameObject.AddComponent<SpriteRenderer>();
        view.sprite = data.idle;
        view.sortingOrder = 9;
        view.spriteSortPoint = SpriteSortPoint.Pivot;
        animation = new CharacterAnimation(data, view);
    }

    void Update()
    {
        if (game.Paused) return;
        if (Dead)
        {
            corpseTime += Time.deltaTime;
            view.sprite = data.death;
            view.sortingOrder = 1;
            view.color = new Color(0.65f, 0.65f, 0.65f, Mathf.Clamp01(9 - corpseTime));
            if (corpseTime > 10) Destroy(gameObject);
            return;
        }
        if (sleeping || game.player == null || game.player.Dead)
        {
            animation.Walk(0, Time.deltaTime);
            return;
        }
        float dt = Time.deltaTime;
        cooldown -= dt;
        hurt -= dt;
        Vector2 delta = game.player.transform.position - transform.position;
        if (delta.magnitude > 14)
        {
            animation.Walk(0, dt);
            return;
        }
        if (attackTime > 0)
        {
            animation.Walk(0, dt);
            attackTime -= dt;
            view.color = new Color(1.4f, 0.45f, 0.35f);
            if (attackTime <= 0)
            {
                transform.position = game.maze.Move(transform.position, strikeDirection * 0.24f);
                if (Vector2.Distance(transform.position, game.player.transform.position) < 1.05f && game.maze.ClearLine(transform.position, game.player.transform.position)) game.player.TakeDamage(19);
                cooldown = 0.85f;
                view.color = Color.white;
            }
            return;
        }
        view.color = hurt > 0 ? new Color(2, 1.2f, 1.2f) : Color.white;
        if (hurt > 0) return;
        if (delta.magnitude < 0.95f && cooldown <= 0 && game.maze.ClearLine(transform.position, game.player.transform.position))
        {
            attackTime = 0.4f;
            strikeDirection = delta.normalized;
            animation.Walk(0, dt);
        }
        else if (delta.magnitude > 0.72f)
        {
            Vector2 direction = game.maze.ChaseDirection(transform.position, game.player.transform.position);
            Vector2 separation = Vector2.zero;
            foreach (Demon other in game.enemies)
            {
                if (other == null || other == this || other.Dead) continue;
                Vector2 apart = transform.position - other.transform.position;
                if (apart.sqrMagnitude < 0.5f && apart.sqrMagnitude > 0.001f) separation += apart.normalized * 0.4f;
            }
            Vector2 previousPosition = transform.position;
            transform.position = game.maze.Move(transform.position, (direction + separation).normalized * dt * data.speed);
            float movement = dt > 0 ? Vector2.Distance(previousPosition, transform.position) / (data.speed * dt) : 0;
            animation.Walk(movement, dt);
            if (direction.sqrMagnitude > 0)
            {
                Quaternion targetRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 540 * dt);
            }
        }
        else animation.Walk(0, dt);
    }

    public void TakeDamage(int amount, Vector2 push)
    {
        if (Dead) return;
        health = Mathf.Max(0, health - amount);
        hurt = 0.2f;
        attackTime = 0;
        transform.position = game.maze.Move(transform.position, push);
        if (Dead)
        {
            view.sprite = data.death;
            view.sortingOrder = 1;
        }
    }

    void OnDestroy()
    {
        if (game != null) game.enemies.Remove(this);
    }
}
