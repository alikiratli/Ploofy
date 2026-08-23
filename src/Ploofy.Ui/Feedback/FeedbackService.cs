using Plugin.Maui.Audio;

namespace Ploofy.Ui.Feedback;

/// <summary>
/// <see cref="IFeedbackService"/> uygulaması: ses + titreşim.
/// </summary>
/// <remarks>
/// <para>
/// Ses dosyaları uygulamanın <c>Resources/Raw/sounds</c> klasöründen okunuyor.
/// Dosya yoksa ses sessizce atlanıyor: eksik bir ses efekti oyunu durdurmamalı,
/// üstelik ses varlıkları içerik üretimiyle birlikte parça parça geliyor.
/// </para>
/// <para>
/// Çalıcılar önbelleğe alınıyor. Kart çevirme sesi saniyede birkaç kez
/// çalınabiliyor; her seferinde dosyayı yeniden açmak bu yaş grubunun
/// cihazlarında duyulur bir gecikme yaratıyor.
/// </para>
/// </remarks>
public sealed class FeedbackService(IAudioManager audioManager) : IFeedbackService, IDisposable
{
    private readonly Dictionary<FeedbackCue, IAudioPlayer?> _players = [];
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public bool SoundEnabled { get; set; } = true;

    public bool HapticsEnabled { get; set; } = true;

    public async ValueTask PlayAsync(FeedbackCue cue)
    {
        Vibrate(cue);

        if (!SoundEnabled)
        {
            return;
        }

        var player = await GetPlayerAsync(cue);
        if (player is null)
        {
            return;
        }

        try
        {
            // Aynı ses üst üste binebilir (hızlı ardışık dokunuşlar);
            // baştan başlatmak kesik kesik çalmasından daha iyi duyuluyor.
            if (player.IsPlaying)
            {
                player.Stop();
            }

            player.Play();
        }
        catch (Exception)
        {
            // Ses donanımı meşgulse ya da platform reddettiyse oyun devam eder.
        }
    }

    private void Vibrate(FeedbackCue cue)
    {
        if (!HapticsEnabled)
        {
            return;
        }

        try
        {
            // Kutlama ve devir daha uzun, gündelik dokunuşlar kısa titriyor.
            var type = cue is FeedbackCue.RoundComplete or FeedbackCue.StarEarned or FeedbackCue.Handoff
                ? HapticFeedbackType.LongPress
                : HapticFeedbackType.Click;

            HapticFeedback.Default.Perform(type);
        }
        catch (FeatureNotSupportedException)
        {
            // Titreşimi olmayan cihaz (çoğu tablet). Ses ve animasyon yeterli.
        }
    }

    private async Task<IAudioPlayer?> GetPlayerAsync(FeedbackCue cue)
    {
        if (_players.TryGetValue(cue, out var cached))
        {
            return cached;
        }

        await _loadLock.WaitAsync();
        try
        {
            if (_players.TryGetValue(cue, out cached))
            {
                return cached;
            }

            IAudioPlayer? player = null;
            try
            {
                var stream = await FileSystem.OpenAppPackageFileAsync(FileNameFor(cue));
                player = audioManager.CreatePlayer(stream);
            }
            catch (FileNotFoundException)
            {
                // Ses varlığı henüz eklenmemiş; sessiz devam.
            }

            _players[cue] = player;
            return player;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static string FileNameFor(FeedbackCue cue) => cue switch
    {
        FeedbackCue.Tap => "sounds/tap.mp3",
        FeedbackCue.Correct => "sounds/correct.mp3",
        FeedbackCue.Retry => "sounds/retry.mp3",
        FeedbackCue.RoundComplete => "sounds/round_complete.mp3",
        FeedbackCue.StarEarned => "sounds/star.mp3",
        FeedbackCue.Handoff => "sounds/handoff.mp3",
        FeedbackCue.Locked => "sounds/locked.mp3",
        _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null),
    };

    public void Dispose()
    {
        foreach (var player in _players.Values)
        {
            player?.Dispose();
        }

        _players.Clear();
        _loadLock.Dispose();
    }
}
