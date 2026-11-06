using UnityEngine;

public static class GameMusic
{
    public static void Play(GameObject owner, string track, float volume)
    {
        AudioClip clip = Resources.Load<AudioClip>("Music/" + track);
        if (clip == null) return;
        AudioSource music = owner.AddComponent<AudioSource>();
        music.clip = clip;
        music.loop = true;
        music.volume = volume;
        music.Play();
    }
}
