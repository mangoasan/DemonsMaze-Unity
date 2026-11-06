using UnityEngine;

public class CharacterAnimation
{
    CharacterData data;
    SpriteRenderer view;
    float walkPhase;

    public CharacterAnimation(CharacterData character, SpriteRenderer renderer)
    {
        data = character;
        view = renderer;
    }

    public void Walk(float movement, float dt)
    {
        if (movement > 0.02f)
        {
            walkPhase = (walkPhase + dt * data.walkFps * Mathf.Clamp01(movement)) % data.walk.Length;
            view.sprite = data.walk[(int)walkPhase];
            return;
        }
        float halfCycle = data.walk.Length * 0.5f;
        float rest = Mathf.Round(walkPhase / halfCycle) * halfCycle;
        walkPhase = Mathf.MoveTowards(walkPhase, rest, dt * data.walkFps * 1.75f);
        if (Mathf.Approximately(walkPhase, rest))
        {
            walkPhase %= data.walk.Length;
            view.sprite = data.idle;
        }
        else view.sprite = data.walk[(int)walkPhase % data.walk.Length];
    }

    public void Reset()
    {
        walkPhase = 0;
    }

    public void Attack(Sprite[] frames, float progress)
    {
        int index = Mathf.Min(frames.Length - 1, (int)(Mathf.Clamp01(progress) * frames.Length));
        view.sprite = index >= 0 ? frames[index] : data.idle;
    }
}
