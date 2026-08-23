using Ploofy.Engine;

namespace Ploofy.Engine.Tests;

public class AgeBandTests
{
    [Theory]
    [InlineData(2, AgeBand.Filiz)]
    [InlineData(3, AgeBand.Filiz)]
    // Bantlar uçlarda örtüşüyor; sınır yaş bilinçli olarak büyük banda gidiyor.
    [InlineData(4, AgeBand.Fidan)]
    [InlineData(5, AgeBand.Fidan)]
    [InlineData(6, AgeBand.Mese)]
    [InlineData(9, AgeBand.Mese)]
    public void ForAge_picks_the_band_the_child_grows_into(int age, AgeBand expected) =>
        Assert.Equal(expected, AgeBandExtensions.ForAge(age));

    [Theory]
    [InlineData(AgeBand.Filiz)]
    [InlineData(AgeBand.Fidan)]
    [InlineData(AgeBand.Mese)]
    public void Id_round_trips(AgeBand band) =>
        Assert.Equal(band, AgeBandExtensions.FromId(band.ToId()));

    [Fact]
    public void Unknown_id_falls_back_to_the_middle_band()
    {
        // Kayıt bozulmuşsa çocuğu hatayla değil oynanabilir bir zorlukla karşıla.
        Assert.Equal(AgeBand.Fidan, AgeBandExtensions.FromId("bilinmeyen"));
    }

    [Fact]
    public void Ids_are_stable_strings_because_saved_progress_depends_on_them()
    {
        Assert.Equal("filiz", AgeBand.Filiz.ToId());
        Assert.Equal("fidan", AgeBand.Fidan.ToId());
        Assert.Equal("mese", AgeBand.Mese.ToId());
    }
}
