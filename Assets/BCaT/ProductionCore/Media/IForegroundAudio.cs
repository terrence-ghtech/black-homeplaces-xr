namespace BCaT.Production.Media
{
    /// <summary>A foreground listening experience with authoritative playback state.</summary>
    public interface IForegroundAudio
    {
        bool IsPlaying { get; }
        void Play();
        void Stop();
    }
}
