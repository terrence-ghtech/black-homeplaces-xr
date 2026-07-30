using System.Collections.Generic;
using UnityEngine;

namespace BCaT.Production.Settings
{
    /// <summary>Logical audio routing channels for the settings mixer.</summary>
    public enum AudioChannel
    {
        Narration, // spoken exhibit narratives
        Ambience,  // environmental/spatial ambience
        Effects,   // UI and incidental effects
        Media,     // video exhibit soundtracks
    }

    /// <summary>
    /// Centralized audio routing (the project previously had no AudioMixer and
    /// AudioMixer assets cannot be created outside the editor UI, so this is the
    /// documented "equivalent centralized audio routing"). Media/narrative/
    /// ambience sources register here — each source's authored volume is
    /// captured once as its artistic baseline and the channel + master values
    /// scale that baseline. Master additionally applies through
    /// AudioListener.volume so unregistered incidental sources still respond to
    /// the master slider without altering their relative artistic balance.
    /// </summary>
    public static class AudioChannelService
    {
        sealed class Entry
        {
            public AudioSource Source;
            public AudioChannel Channel;
            public float BaseVolume;
        }

        static readonly Dictionary<AudioSource, Entry> entries =
            new Dictionary<AudioSource, Entry>();

        static ApplicationSettingsData.AudioSettings applied =
            new ApplicationSettingsData.AudioSettings();

        /// <summary>
        /// Register (or re-classify) a source. The current source volume is
        /// captured as its baseline on first registration.
        /// </summary>
        public static void Register(AudioSource source, AudioChannel channel)
        {
            if (source == null) return;
            if (entries.TryGetValue(source, out var existing))
            {
                existing.Channel = channel;
            }
            else
            {
                entries[source] = new Entry
                {
                    Source = source,
                    Channel = channel,
                    BaseVolume = source.volume,
                };
            }
            ApplyTo(entries[source]);
        }

        /// <summary>
        /// Inform the service that a source's authored volume changed by design
        /// (e.g. Black Kitchen ambience ducking), so the new value becomes the
        /// baseline that user volume scales.
        /// </summary>
        public static void UpdateBaseVolume(AudioSource source, float baseVolume)
        {
            if (source != null && entries.TryGetValue(source, out var e))
            {
                e.BaseVolume = baseVolume;
                ApplyTo(e);
            }
        }

        public static void Unregister(AudioSource source)
        {
            if (source != null) entries.Remove(source);
        }

        /// <summary>The channel-scaled volume a registered source should currently use.</summary>
        public static float ScaledVolume(AudioSource source, float baseVolume)
        {
            float channelScale = 1f;
            if (source != null && entries.TryGetValue(source, out var e))
                channelScale = ChannelValue(e.Channel);
            return baseVolume * channelScale;
        }

        public static void Apply(ApplicationSettingsData.AudioSettings audio)
        {
            applied = audio;
            AudioListener.volume = Mathf.Clamp01(audio.master);

            var dead = new List<AudioSource>();
            foreach (var e in entries.Values)
            {
                if (e.Source == null) { dead.Add(e.Source); continue; }
                ApplyTo(e);
            }
            foreach (var d in dead)
                entries.Remove(d);
        }

        static void ApplyTo(Entry e)
        {
            if (e.Source == null) return;
            e.Source.volume = Mathf.Clamp01(e.BaseVolume * ChannelValue(e.Channel));
        }

        static float ChannelValue(AudioChannel channel) => channel switch
        {
            AudioChannel.Narration => applied.narration,
            AudioChannel.Ambience => applied.ambience,
            AudioChannel.Effects => applied.effects,
            AudioChannel.Media => applied.media,
            _ => 1f,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            entries.Clear();
            applied = new ApplicationSettingsData.AudioSettings();
        }
    }
}
