using UnityEngine;

[CreateAssetMenu(menuName = "Demon's Maze/Character")]
public class CharacterData : ScriptableObject
{
    public string title;
    public Sprite idle;
    public Sprite[] walk;
    public Sprite[] attack;
    public Sprite[] secondAttack;
    public Sprite death;
    public int health = 100;
    public float speed = 3.4f;
    public float walkFps = 7.5f;
    public int damage = 30;
    public float attackTime = 0.45f;
    public float reach = 1.25f;
    public float staminaCost = 20;
    public bool ranged;
    public bool bottle;
}
