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
        if (Me != null)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Me = this;
        DontDestroyOnLoad(gameObject);

        foreach (Sound s in m_sounds)
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Play("ding");
        }
    }

    public static void Play (string name, bool reset=true)
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

        if (!s.source.isPlaying||reset)
        {
           s.source.Play();
        }
       
    }

    public static void Stop(string name)
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
        s.source.Stop();
    }

    public static void StopAll()
    {
        foreach (Sound s in Me.m_sounds)
        {
            Stop(s.name);
        }
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

