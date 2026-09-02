using Ploofy.Engine.Access;
using Ploofy.Engine.Catalog;

namespace Ploofy.Engine.Tests;

public class EntitlementsTests
{
    [Fact]
    public void The_free_tier_opens_exactly_the_showcase_games()
    {
        var free = Entitlements.Free;
        var playable = GameCatalog.Games.Where(free.CanPlay).ToList();

        Assert.Equal(GameCatalog.Free, playable);
        Assert.Equal(2, playable.Count);
        Assert.Contains(playable, g => g.Id == GameCatalog.MemoryMatch);
        Assert.Contains(playable, g => g.Id == GameCatalog.BubblePop);
    }

    [Fact]
    public void A_subscription_opens_everything_including_games_added_later()
    {
        var subscribed = Entitlements.Subscribed;

        Assert.All(GameCatalog.Games, g => Assert.True(subscribed.CanPlay(g)));
        Assert.Empty(subscribed.LockedGames());
    }

    [Fact]
    public void A_billing_problem_keeps_the_games_open()
    {
        // Çocuğun oyununu ödeme uyarısıyla kesmek doğru değil; uyarı yalnızca
        // ebeveyn ekranında görünür.
        var grace = new Entitlements(SubscriptionStatus.Grace);

        Assert.True(grace.HasFullAccess);
        Assert.True(grace.NeedsBillingAttention);
        Assert.False(Entitlements.Subscribed.NeedsBillingAttention);
    }

    [Fact]
    public void Ending_a_subscription_keeps_the_games_open_until_the_period_runs_out()
    {
        // İptal yenilemeyi kapatır, ödenmiş günleri geri almaz. Erişimi iptal
        // anında kesmek hem ebeveynin parasını yakardı hem de mağazanın kendi
        // davranışıyla çelişirdi.
        var canceled = new Entitlements(SubscriptionStatus.Canceled);

        Assert.True(canceled.HasFullAccess);
        Assert.False(canceled.AutoRenews);
        Assert.True(canceled.AccessEndsAfterPeriod);
        Assert.False(canceled.NeedsBillingAttention);
    }

    [Fact]
    public void Only_a_running_subscription_can_be_ended()
    {
        // Ödeme sorunlu abonelik de bitirilebilir olmalı: ebeveyn tam da o
        // noktada vazgeçmek isteyebilir.
        Assert.True(Entitlements.Subscribed.CanCancel);
        Assert.True(new Entitlements(SubscriptionStatus.Grace).CanCancel);

        // Zaten bitirilmiş ya da hiç başlamamış bir aboneliği bitirmek yok.
        Assert.False(new Entitlements(SubscriptionStatus.Canceled).CanCancel);
        Assert.False(Entitlements.Free.CanCancel);
    }

    [Fact]
    public void Profile_limits_follow_the_tier()
    {
        Assert.Equal(1, Entitlements.Free.ProfileLimit);
        Assert.Equal(4, Entitlements.Subscribed.ProfileLimit);

        Assert.True(Entitlements.Free.CanAddProfile(0));
        Assert.False(Entitlements.Free.CanAddProfile(1));
        Assert.True(Entitlements.Subscribed.CanAddProfile(3));
        Assert.False(Entitlements.Subscribed.CanAddProfile(4));
    }

    [Fact]
    public void No_tier_ever_shows_ads()
    {
        // "Reklamsız ve güvenli" pazarlamanın merkezinde; bu testin kırılması
        // ürün vaadinin kırılması demek.
        Assert.False(Entitlements.Free.ShowsAds);
        Assert.False(Entitlements.Subscribed.ShowsAds);
        Assert.False(new Entitlements(SubscriptionStatus.Grace).ShowsAds);
        Assert.False(new Entitlements(SubscriptionStatus.Canceled).ShowsAds);
    }
}

public class SubscriptionInfoTests
{
    private static readonly DateOnly Today = new(2026, 9, 2);

    [Fact]
    public void A_running_subscription_reports_a_renewal_date_not_an_end_date()
    {
        var info = new SubscriptionInfo(SubscriptionStatus.Active, Today.AddMonths(1));

        Assert.Equal(new DateOnly(2026, 10, 2), info.RenewsOn);
        Assert.Null(info.AccessEndsOn);
        Assert.Equal(30, info.DaysLeft(Today));
        Assert.False(info.HasExpired(Today));
    }

    [Fact]
    public void An_ended_subscription_reports_an_end_date_not_a_renewal_date()
    {
        // Ekrandaki cümle bu ikisinden hangisinin dolu olduğuna göre kuruluyor;
        // ikisini birden doldurmak "hem yenilenecek hem bitecek" derdi.
        var info = new SubscriptionInfo(SubscriptionStatus.Canceled, new DateOnly(2026, 9, 20));

        Assert.Null(info.RenewsOn);
        Assert.Equal(new DateOnly(2026, 9, 20), info.AccessEndsOn);
        Assert.Equal(18, info.DaysLeft(Today));
    }

    [Fact]
    public void A_missing_date_is_not_invented()
    {
        // Çevrimdışı ilk açılışta mağazaya sorulamıyor. Tarih yoksa ekran
        // "mağaza henüz bildirmedi" diyor; hiçbir kural tarihe bağlı değil.
        var info = new SubscriptionInfo(SubscriptionStatus.Active);

        Assert.Null(info.RenewsOn);
        Assert.Null(info.AccessEndsOn);
        Assert.Null(info.DaysLeft(Today));
        Assert.False(info.HasExpired(Today));
        Assert.True(info.Entitlements.HasFullAccess);
    }

    [Fact]
    public void The_period_can_be_over_without_access_being_cut()
    {
        // Süre dolduğunu görmek erişimi kapatmıyor: o kararı mağaza veriyor.
        // Cihazın saatini ileri almak da oyunları kilitlemiyor.
        var info = new SubscriptionInfo(SubscriptionStatus.Active, new DateOnly(2026, 8, 1));

        Assert.True(info.HasExpired(Today));
        Assert.Equal(0, info.DaysLeft(Today));
        Assert.True(info.Entitlements.HasFullAccess);
    }

    [Fact]
    public void The_free_tier_has_no_period_at_all()
    {
        Assert.Null(SubscriptionInfo.Free.PeriodEndsOn);
        Assert.Null(SubscriptionInfo.Free.RenewsOn);
        Assert.False(SubscriptionInfo.Free.Entitlements.HasFullAccess);
    }
}

public class ParentalGateTests
{
    [Fact]
    public void The_challenge_is_beyond_the_oldest_band_but_trivial_for_an_adult()
    {
        // Meşe bandı 9 yaşa kadar; iki basamaklı çarpma + elde ile toplama
        // bu yaşta kafadan hızlıca yapılamıyor.
        for (var seed = 0; seed < 200; seed++)
        {
            var challenge = ParentalGateChallenge.Generate(new Random(seed));

            Assert.InRange(challenge.Left, 6, 9);
            Assert.InRange(challenge.Right, 6, 9);
            Assert.InRange(challenge.Addend, 11, 29);
            Assert.Equal((challenge.Left * challenge.Right) + challenge.Addend, challenge.Answer);
        }
    }

    [Fact]
    public void Only_the_exact_answer_is_accepted()
    {
        var challenge = new ParentalGateChallenge(7, 8, 15);

        Assert.Equal(71, challenge.Answer);
        Assert.True(challenge.Accepts("71"));
        Assert.True(challenge.Accepts("  71 "));
        Assert.False(challenge.Accepts("7"));
        Assert.False(challenge.Accepts("yetmişbir"));
        Assert.False(challenge.Accepts(""));
        Assert.False(challenge.Accepts(null));
    }

    [Fact]
    public void Unlocking_lasts_long_enough_to_browse_settings_but_not_longer()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var gate = new ParentalGateState(time);

        Assert.False(gate.IsUnlocked);

        gate.MarkUnlocked();
        Assert.True(gate.IsUnlocked);

        // Ebeveyn hâlâ ayarlarda gezinebiliyor.
        time.Advance(TimeSpan.FromMinutes(4));
        Assert.True(gate.IsUnlocked);

        // Cihaz çocuğa geri döndüğünde kilit yeniden kapanmış olmalı.
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.False(gate.IsUnlocked);
    }

    [Fact]
    public void Locking_takes_effect_immediately()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var gate = new ParentalGateState(time);

        gate.MarkUnlocked();
        gate.Lock();

        Assert.False(gate.IsUnlocked);
    }
}

/// <summary>Testlerde saati ileri saran basit zaman kaynağı.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
