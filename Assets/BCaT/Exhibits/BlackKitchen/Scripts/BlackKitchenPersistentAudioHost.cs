using UnityEngine;

public class BlackKitchenPersistentAudioHost : MonoBehaviour
{
    private const string HostName = "BlackKitchenPersistentAudioHost";
    private static BlackKitchenPersistentAudioHost instance;
    private AudioSource source;

    public static BlackKitchenPersistentAudioHost Instance
    {
        get
        {
            if (instance != null)
                return instance;

            GameObject existing = GameObject.Find(HostName);
            if (existing != null)
                instance = existing.GetComponent<BlackKitchenPersistentAudioHost>();

            if (instance == null)
            {
                GameObject host = new GameObject(HostName);
                instance = host.AddComponent<BlackKitchenPersistentAudioHost>();
                DontDestroyOnLoad(host);
            }

            return instance;
        }
    }

    public AudioSource Source
    {
        get
        {
            if (source == null)
            {
                source = GetComponent<AudioSource>();
                if (source == null)
                    source = gameObject.AddComponent<AudioSource>();

                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
            }

            return source;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public AudioSource PrepareFrom(AudioSource template, AudioClip clip, float volume)
    {
        AudioSource persistentSource = Source;
        if (template != null)
        {
            persistentSource.outputAudioMixerGroup = template.outputAudioMixerGroup;
            persistentSource.priority = template.priority;
            persistentSource.pitch = template.pitch;
        }

        persistentSource.clip = clip;
        persistentSource.loop = false;
        persistentSource.volume = Mathf.Clamp01(volume);
        persistentSource.spatialBlend = 0f;
        return persistentSource;
    }
}
