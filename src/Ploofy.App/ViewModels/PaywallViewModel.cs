using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Engine.Access;
using Ploofy.Ui.Parental;

namespace Ploofy.App.ViewModels;

/// <summary>
/// Abonelik ekranı.
/// </summary>
/// <remarks>
/// Ekranın merkezinde fiyat değil vaat var: reklamsız ve güvenli. Bu, hedef
/// kitlenin (ebeveyn) satın alma kararında en ağır basan madde; oyun sayısı
/// ondan sonra geliyor.
///
/// Satın alma düğmesi ebeveyn kilidinin arkasında — çocuğun yanlışlıkla
/// satın almasını önlemek platform kurallarının da gereği.
/// </remarks>
public sealed partial class PaywallViewModel(
    ISubscriptionService subscriptions,
    IParentalGateService parentalGate) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool AlreadySubscribed { get; set; }

    public ObservableCollection<string> Benefits { get; } = [];

    public void Load()
    {
        var l = LocalizationService.Instance;

        AlreadySubscribed = subscriptions.Current.HasFullAccess;

        Benefits.Clear();
        Benefits.Add(l["SubscriptionBenefitNoAds"]);
        Benefits.Add(l["SubscriptionBenefitAllGames"]);
        Benefits.Add(l["SubscriptionBenefitNewGames"]);
        Benefits.Add(l.Format("SubscriptionBenefitProfiles", Entitlements.SubscribedProfileLimit));
        Benefits.Add(l["SubscriptionBenefitOffline"]);
    }

    [RelayCommand]
    private async Task SubscribeAsync()
    {
        if (IsBusy || !await parentalGate.RequestAsync(ParentalGateReason.Purchase))
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (await subscriptions.PurchaseAsync())
            {
                await Navigation.GoHomeAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        IsBusy = true;
        try
        {
            if (await subscriptions.RestoreAsync())
            {
                await Navigation.GoHomeAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static async Task CloseAsync() => await Shell.Current.GoToAsync("..");
}
