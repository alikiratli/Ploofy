using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Ui.Feedback;

namespace Ploofy.App.ViewModels;

/// <summary>Koleksiyondaki tek avatar.</summary>
public sealed partial class CollectedAvatar(string emoji, int requiredStars, bool isUnlocked)
    : ObservableObject
{
    public string Emoji { get; } = emoji;

    /// <summary>Açılması için gereken toplam yıldız; başlangıçtan açıksa sıfır.</summary>
    public int RequiredStars { get; } = requiredStars;

    public bool IsUnlocked { get; } = isUnlocked;

    public bool IsLocked => !IsUnlocked;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Kilitli avatarın altındaki "5 ★" etiketi.</summary>
    public string RequirementText => IsUnlocked ? string.Empty : $"{RequiredStars} ★";

    /// <summary>
    /// Kilitli avatar soluk duruyor ama <b>gizlenmiyor</b>: görünmeyen ödül
    /// ödül değil, çocuk neyi kazanacağını görmeden yıldız toplamıyor.
    /// </summary>
    public double Opacity => IsUnlocked ? 1.0 : 0.4;
}

/// <summary>Koleksiyon ekranındaki tek grup.</summary>
public sealed class CollectedGroup(string nameKey, IReadOnlyList<CollectedAvatar> avatars)
{
    public string Name => LocalizationService.Instance[nameKey];

    public IReadOnlyList<CollectedAvatar> Avatars { get; } = avatars;
}

/// <summary>
/// Koleksiyon: yıldızın karşılığı.
/// </summary>
/// <remarks>
/// <para>
/// Yıldızlar uzun süre birikip hiçbir şey açmadı; ana ekranda bir sayı olarak
/// duruyorlardı. Bu ekran o sayıya karşılık veriyor — kaç arkadaş açıldı,
/// sıradaki kim, ona kaç yıldız kaldı.
/// </para>
/// <para>
/// Ekran <b>ebeveyn kilidinin arkasında değil</b>. Kilit, geri alınamayan ya
/// da para harcatan işler için; koleksiyona bakmak ve açılmış bir avatarı
/// seçmek ikisi de değil. Çocuk kazandığı ödülü kullanmak için ebeveyn
/// çağırmak zorunda kalsaydı ödülün yarısı kaybolurdu.
/// </para>
/// <para>
/// Bant değiştiren çocuk yıldızlarını koruyor
/// (<c>ProgressRepository.TotalStarsAsync</c> bütün bantları topluyor), yani
/// büyümek koleksiyonu geri almıyor.
/// </para>
/// </remarks>
public sealed partial class CollectionViewModel(
    ProgressRepository repository,
    AppState state,
    IFeedbackService feedback) : ObservableObject
{
    [ObservableProperty]
    public partial string ChildName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChildAvatar { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalStars { get; set; }

    /// <summary>"7 / 20 arkadaş".</summary>
    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    /// <summary>Sıradaki ödülün simgesi; hepsi açıldıysa boş.</summary>
    [ObservableProperty]
    public partial string NextAvatar { get; set; } = string.Empty;

    /// <summary>"Yeni arkadaş için 2 yıldız" ya da hepsi açıldıysa kutlama.</summary>
    [ObservableProperty]
    public partial string NextText { get; set; } = string.Empty;

    /// <summary>Sıradaki ödüle giden çubuk, 0-1.</summary>
    [ObservableProperty]
    public partial double NextProgress { get; set; }

    [ObservableProperty]
    public partial bool HasNext { get; set; }

    public ObservableCollection<CollectedGroup> Groups { get; } = [];

    public async Task LoadAsync()
    {
        var profile = state.ActiveProfile;
        if (profile is null)
        {
            await Shell.Current.GoToAsync("//profiles");
            return;
        }

        var l = LocalizationService.Instance;

        ChildName = profile.DisplayName;
        ChildAvatar = profile.AvatarId;
        TotalStars = await repository.TotalStarsAsync(profile.Id);

        var progress = AvatarCatalog.Progress(TotalStars);
        CountText = l.Format("CollectionCount", progress.Unlocked, progress.Total);

        HasNext = !progress.IsComplete;
        NextProgress = progress.FractionToNext;

        if (progress.IsComplete)
        {
            NextAvatar = string.Empty;
            NextText = l["CollectionAllUnlocked"];
        }
        else
        {
            NextAvatar = AvatarCatalog.UnlockOrder[progress.Unlocked];
            NextText = l.Format("CollectionNext", progress.StarsToNext);
        }

        Groups.Clear();
        foreach (var group in AvatarCatalog.Groups)
        {
            Groups.Add(new CollectedGroup(
                group.NameKey,
                [.. group.Avatars.Select(emoji => new CollectedAvatar(
                    emoji,
                    AvatarCatalog.RequiredStars(emoji),
                    // Çocuğun o an takındığı avatar her zaman açık sayılıyor.
                    // Katalog sırası ileride değişirse ya da bir avatar
                    // kilitlenirse, çocuk kendi simgesini kaybetmiş gibi
                    // görmemeli.
                    AvatarCatalog.IsUnlocked(emoji, TotalStars)
                        || emoji == profile.AvatarId)
                {
                    IsSelected = emoji == profile.AvatarId,
                })]));
        }

        // Kutlama borcu varsa burada kapanıyor: çocuk ödülleri bu ekranda
        // gördü, tur sonu ekranının ikinci kez kutlamasına gerek yok.
        await repository.SetRewardWatermarkAsync(profile.Id, TotalStars);
    }

    /// <summary>
    /// Açılmış bir avatarı çocuğun profiline takar.
    /// </summary>
    /// <remarks>
    /// Ödülün kullanılabilir olması bütün mesele. Kilitli avatara dokunmak
    /// hata değil: kilit sesi çalıyor ve gereken yıldız zaten altında yazılı.
    /// </remarks>
    [RelayCommand]
    private async Task ChooseAsync(CollectedAvatar? choice)
    {
        var profile = state.ActiveProfile;
        if (choice is null || profile is null)
        {
            return;
        }

        if (choice.IsLocked)
        {
            await feedback.PlayAsync(FeedbackCue.Locked);
            return;
        }

        if (choice.Emoji == profile.AvatarId)
        {
            return;
        }

        profile.AvatarId = choice.Emoji;
        await repository.UpdateProfileAsync(profile);
        await state.RefreshActiveProfileAsync();

        ChildAvatar = choice.Emoji;

        foreach (var avatar in Groups.SelectMany(g => g.Avatars))
        {
            avatar.IsSelected = ReferenceEquals(avatar, choice);
        }

        await feedback.PlayAsync(FeedbackCue.Correct);
    }

    [RelayCommand]
    private static async Task BackAsync() => await Shell.Current.GoToAsync("..");
}
