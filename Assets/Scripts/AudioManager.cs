using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{

    public static AudioManager Me;

    public List<Sound> m_sounds = new();

    private void Awake()
    {
        Me = this;
        foreach (Sound s in m_sounds)
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
        }
    }

    public static void Play (string name)
    {
        Sound s = Me.m_sounds.Find(sound => sound.name == name);
        if (s==null)
        {
            Debug.LogWarning("Sound: " + name + " not found");
            return;
        }
        if (s.source.clip == null)
        {
            Debug.LogWarning("Sound: " + name + " has no audio");
            return;
        }
        s.source.Play();
    }

    public static void PlayAtPoint (string name, Vector2 point)
    {
        Sound s = Me.m_sounds.Find(sound => sound.name == name);
        if (s == null)
        {
            Debug.LogWarning("Sound: " + name + " not found");
            return;
        }
        if (s.source.clip == null)
        {
            Debug.LogWarning("Sound: " + name + " has no audio");
            return;
        }
        AudioSource.PlayClipAtPoint(s.source.clip, point, s.source.volume);
    }
}

