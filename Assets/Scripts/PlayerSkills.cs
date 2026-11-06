using UnityEngine;

sealed class PlayerSkills
{
    readonly PlayerController player;
    readonly GameManager game;
    readonly string hero;
    float cooldown;
    float potionTimer = 16;
    float mana = 100;
    int killsNeeded;
    int executions;

    public float Rage { get; private set; }
    public float Fear { get; private set; }
    public float Veil { get; private set; }
    public string Status => hero switch
    {
        "Knight" => Rage > 0 ? $"Ярость  {Rage:0.0} с" : $"Q  Ярость  {Recharge}",
        "Alchemist" => $"Зелье через  {potionTimer:0} с",
        "Samurai" => executions > 0 ? $"Казни  {executions}" : $"Q  Страх  {(killsNeeded > 0 ? killsNeeded + " убийств" : "готов")}",
        "Mage" => $"Q  Подчинение  {(int)mana}%",
        _ => Veil > 0 ? $"Невидимость  {Veil:0.0} с" : $"Q  Невидимость  {Recharge}"
    };

    string Recharge => cooldown > 0 ? cooldown.ToString("0") + " с" : "готово";

    public PlayerSkills(PlayerController owner, GameManager manager, CharacterData data)
    {
        player = owner;
        game = manager;
        hero = data.name;
    }

    public void Tick(float dt)
    {
        Rage = Mathf.Max(0, Rage - dt);
        Fear = Mathf.Max(0, Fear - dt);
        Veil = Mathf.Max(0, Veil - dt);
        cooldown = Mathf.Max(0, cooldown - dt);
        mana = Mathf.Min(100, mana + dt * 5.5f);
        if (hero != "Alchemist") return;
        potionTimer -= dt;
        if (potionTimer > 0) return;
        potionTimer = 24;
        WorldLoot.Drop(game, player.transform.position, potion: Random.Range(0, 2) == 0 ? Potion.Guard : Potion.Fury);
    }

    public bool Activate()
    {
        if (cooldown > 0 || player.Dead || player.Attacking) return false;
        switch (hero)
        {
            case "Knight":
                Rage = 5;
                cooldown = 16;
                return true;
            case "Samurai":
                if (killsNeeded > 0) return false;
                killsNeeded = 5;
                executions = 2;
                Fear = 5;
                foreach (Demon enemy in game.enemies)
                    if (enemy != null) enemy.Frighten(Fear);
                return true;
            case "Mage":
                return Charm();
            case "Rogue":
                Veil = 5;
                cooldown = 14;
                return true;
            default:
                return false;
        }
    }

    bool Charm()
    {
        if (mana < 100) return false;
        int allies = 0;
        Demon nearest = null;
        float range = 6.5f;
        foreach (Demon enemy in game.enemies)
        {
            if (enemy == null || enemy.Dead) continue;
            if (enemy.Allied)
            {
                allies++;
                continue;
            }
            if (enemy.Kind == EnemyKind.Boss) continue;
            Vector2 delta = enemy.transform.position - player.transform.position;
            float distance = delta.magnitude;
            if (distance >= range || Vector2.Dot(delta.normalized, player.facing) < 0.4f ||
                !game.maze.ClearLine(player.transform.position, enemy.transform.position)) continue;
            nearest = enemy;
            range = distance;
        }
        if (allies >= 2 || nearest == null || !nearest.Charm()) return false;
        mana = 0;
        return true;
    }

    public float AttackMultiplier()
    {
        float modifier = Rage > 0 ? 1.40f : 1;
        if (Veil <= 0) return modifier;
        Veil = 0;
        return modifier * 2.75f;
    }

    public bool Execute(Demon enemy)
    {
        if (executions <= 0 || enemy.Kind == EnemyKind.Boss) return false;
        executions--;
        return true;
    }

    public void OnKill()
    {
        if (killsNeeded > 0) killsNeeded--;
    }
}
