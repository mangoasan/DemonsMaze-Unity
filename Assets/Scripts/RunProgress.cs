using UnityEngine;
using System.Collections.Generic;

public enum Potion
{
    Heal,
    Guard,
    Fury
}

public sealed class RunProgress
{
    public readonly CharacterData Character;
    public readonly int Seed;
    public readonly int[] Potions = new int[3];
    public readonly List<EnemyKind> Companions = new();
    public int Health;
    public int Souls;
    public int Kills;
    public int Power;
    public int Vitality;
    public int Runes;
    public bool FlaskUsed;
    public float Shield;
    public float WardTime;
    public float Fury;
    public bool Victory;

    public int MaxHealth => Character.health + Vitality * 9;

    public RunProgress(CharacterData character, int seed)
    {
        Character = character;
        Seed = seed;
        Health = MaxHealth;
    }

    public bool Spend(int price)
    {
        if (Souls < price) return false;
        Souls -= price;
        return true;
    }
}
