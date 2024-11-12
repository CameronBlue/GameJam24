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

        DontDestroyOnLoad(gameObject);

        foreach (Sound s in m_sounds)
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
        }
    }

<<<<<<< Updated upstream
    public static void Play (string name)
=======
    private void Update()
    {

        if (Input.GetMouseButtonDown(0))
        {
            AudioManager.Play("ding");
        }
    }

    public static void Play(string name, bool reset=true)
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
        s.source.Play();
=======

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
>>>>>>> Stashed changes
    }

    public static void PlayAtPoint(string name, Vector2 point)
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

