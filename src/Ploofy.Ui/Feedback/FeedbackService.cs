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
        catch (Exception)
        {
            // Titreşimi olmayan cihaz, izni kısıtlanmış cihaz, meşgul donanım…
            // Sebep ne olursa olsun oyun devam etmeli: geri bildirimin süs
            // katmanı yüzünden çocuğun oyununu düşürmek kabul edilemez.
            // (Bu yakalama bir kez gerçek bir çökmeyi önledi: VIBRATE izni
            // bildirilmediğinde MAUI PermissionException fırlatıyor.)
            HapticsEnabled = false;
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

    /// <remarks>
    /// Sesler WAV, MP3 değil: <c>tools/build_sounds.py</c> onları sentezleyerek
    /// üretiyor ve saf PCM yazmak için kodlayıcı gerekmiyor. Hepsi bir
    /// saniyenin altında, toplamı bir megabaytın altında kalıyor — bu boy için
    /// sıkıştırmanın kazandıracağı yer, çözücünün ilk çalmada getirdiği
    /// gecikmeye değmiyor.
    /// </remarks>
    private static string FileNameFor(FeedbackCue cue) => cue switch
    {
        FeedbackCue.Tap => "sounds/tap.wav",
        FeedbackCue.Correct => "sounds/correct.wav",
        FeedbackCue.Retry => "sounds/retry.wav",
        FeedbackCue.RoundComplete => "sounds/round_complete.wav",
        FeedbackCue.StarEarned => "sounds/star.wav",
        FeedbackCue.Handoff => "sounds/handoff.wav",
        FeedbackCue.Locked => "sounds/locked.wav",
        FeedbackCue.Pad1 => "sounds/pad1.wav",
        FeedbackCue.Pad2 => "sounds/pad2.wav",
        FeedbackCue.Pad3 => "sounds/pad3.wav",
        FeedbackCue.Pad4 => "sounds/pad4.wav",
        FeedbackCue.Pad5 => "sounds/pad5.wav",
        FeedbackCue.Pad6 => "sounds/pad6.wav",
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
