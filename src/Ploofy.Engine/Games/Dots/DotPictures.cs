namespace Ploofy.Engine.Games;

/// <summary>Resimdeki tek nokta. Koordinatlar 0-1 arası; x sağa, <b>y aşağı</b>.</summary>
public readonly record struct DotPoint(float X, float Y);

/// <summary>
/// Noktaları birleştirilerek çizilen bir resim.
/// </summary>
/// <param name="Id">Sabit anahtar; arayüz adını ve simgesini buradan eşliyor.</param>
/// <param name="Dots">
/// Noktalar <b>çizim sırasıyla</b>: birinci nokta 1, ikincisi 2. Sıra
/// keyfi değil, çocuğun eli resmi bu sırayla dolaşıyor.
/// </param>
public sealed record DotPicture(string Id, IReadOnlyList<DotPoint> Dots)
{
    public int Count => Dots.Count;
}

/// <summary>
/// Noktaları Birleştir'in resimleri.
/// </summary>
/// <remarks>
/// <para>
/// Her resim <b>kapalı</b> bir dış hat: son nokta birinciye bağlanınca hayvan
/// tamamlanıyor. Kapanışı çocuğa yaptırmıyoruz — son noktaya dokunduğunda hat
/// kendiliğinden kapanıyor, çünkü "şimdi tekrar 1'e dön" kuralı bu yaşta
/// oyunun geri kalanından daha zor.
/// </para>
/// <para>
/// Nokta sayısı resmin kendi özelliği, bandın değil. Bandı, hangi resimleri
/// göreceği ayırıyor: Fidan az noktalı olanları, Meşe çok noktalıları. Aynı
/// resmi bandına göre seyreltmek denenebilirdi ama balığın kuyruğunu ya da
/// kedinin kulağını atan bir seyreltme hayvanı tanınmaz yapıyor — noktaların
/// hepsi taşıyıcı.
/// </para>
/// <para>
/// Hepsi <b>yan görünüş</b> ya da <b>ön yüz</b>; ikisi karışmıyor. Bir hayvanı
/// tek kapalı hatla anlatmanın yolu bu: perspektif giren her resim (kıskacı
/// öne uzanan bir yengeç gibi) kırık çizgi yığınına dönüyor.
/// </para>
/// <para>
/// Koordinatlar kenarlardan uzak tutuluyor: nokta ekranın tam kenarındaysa
/// parmak oraya rahat basamıyor. İki nokta arası da en az 0,12 — en dar
/// bandın dokunma yarıçapı 0,06, yani daha yakın iki noktanın hedefleri
/// çakışır ve çocuk doğru bastığı hâlde yanlış nokta seçilirdi.
/// Bu iki kural <c>DotToDotRoundTests</c> içinde sınanıyor.
/// </para>
/// </remarks>
public static class DotPictures
{
    /// <summary>Bütün resimler.</summary>
    public static readonly IReadOnlyList<DotPicture> All =
    [
        // --- Az noktalı: Fidan bandı ---

        // Balık. Kuyruk dörtgeni (4-5-6-7) gövdeden ayrı okunuyor.
        Picture("fish",
            (0.86f, 0.50f),
            (0.72f, 0.29f),
            (0.48f, 0.25f),
            (0.31f, 0.35f),
            (0.11f, 0.22f),
            (0.11f, 0.78f),
            (0.31f, 0.65f),
            (0.56f, 0.75f)),

        // Yıldız. Hayvan değil ama kütüphanenin en okunaklı dış hattı bu: beş
        // uç, beş iç köşe, hiçbir noktası tartışmalı değil. Çocuğun kuralı
        // ilk kez öğrendiği resim genelde bu oluyor.
        Picture("star",
            (0.50f, 0.10f),
            (0.60f, 0.36f),
            (0.88f, 0.38f),
            (0.66f, 0.55f),
            (0.74f, 0.82f),
            (0.50f, 0.67f),
            (0.26f, 0.82f),
            (0.34f, 0.55f),
            (0.12f, 0.38f),
            (0.40f, 0.36f)),

        // Kedi başı: iki sivri kulak, iki yanak, çene.
        Picture("cat",
            (0.28f, 0.34f),
            (0.22f, 0.12f),
            (0.42f, 0.24f),
            (0.58f, 0.24f),
            (0.78f, 0.12f),
            (0.72f, 0.34f),
            (0.84f, 0.55f),
            (0.63f, 0.84f),
            (0.37f, 0.84f),
            (0.16f, 0.55f)),

        // Ördek: suda yüzüyor, gaga sağda, kuyruk solda. Boyun (10-1) gövdeden
        // ayrı bir yükseliş, ördeği ördek yapan da o.
        Picture("duck",
            (0.60f, 0.14f),
            (0.74f, 0.24f),
            (0.90f, 0.32f),
            (0.72f, 0.40f),
            (0.68f, 0.56f),
            (0.56f, 0.82f),
            (0.28f, 0.82f),
            (0.10f, 0.62f),
            (0.34f, 0.52f),
            (0.48f, 0.34f)),

        // Kaplumbağa: baş sağda, kabuk kubbe, altta iki ayak. Karın çizgisi
        // (5) fazla yükselirse kabuk ısırılmış gibi duruyor — 0,68 sınırı.
        Picture("turtle",
            (0.80f, 0.40f),
            (0.92f, 0.50f),
            (0.76f, 0.58f),
            (0.66f, 0.80f),
            (0.50f, 0.68f),
            (0.32f, 0.82f),
            (0.16f, 0.60f),
            (0.14f, 0.42f),
            (0.34f, 0.22f),
            (0.62f, 0.26f)),

        // --- Çok noktalı: Meşe bandı ---

        // Kuş: yandan, gaga sağda. Kuyruk çatal (8-9), gövdeden belirgin
        // biçimde ayrılıyor; tek uçlu bir kuyruk balığa benziyordu.
        Picture("bird",
            (0.54f, 0.16f),
            (0.70f, 0.26f),
            (0.90f, 0.34f),
            (0.68f, 0.42f),
            (0.62f, 0.58f),
            (0.56f, 0.80f),
            (0.32f, 0.86f),
            (0.08f, 0.78f),
            (0.14f, 0.62f),
            (0.34f, 0.52f),
            (0.40f, 0.30f)),

        // Balina: yandan, kuyruk solda. Kuyruk çentiği (8) iki yüzgeci
        // ayırıyor; onsuz kuyruk tek bir sivri uç oluyor.
        Picture("whale",
            (0.72f, 0.34f),
            (0.86f, 0.44f),
            (0.90f, 0.58f),
            (0.72f, 0.66f),
            (0.52f, 0.76f),
            (0.28f, 0.74f),
            (0.10f, 0.84f),
            (0.16f, 0.62f),
            (0.08f, 0.44f),
            (0.30f, 0.48f),
            (0.50f, 0.36f)),

        // Tavşan başı: iki uzun kulak, aralarında bir çukur (4).
        Picture("rabbit",
            (0.36f, 0.42f),
            (0.26f, 0.10f),
            (0.42f, 0.12f),
            (0.50f, 0.38f),
            (0.58f, 0.12f),
            (0.74f, 0.10f),
            (0.64f, 0.42f),
            (0.78f, 0.56f),
            (0.71f, 0.82f),
            (0.50f, 0.90f),
            (0.28f, 0.80f),
            (0.22f, 0.56f)),

        // Köpek başı. Kulaklar <b>çenenin altına kadar</b> iniyor (4-5 ve
        // 11-12) ve iç kenarları yanağa doğru geri yükseliyor (6, 10). İlk
        // denemede kulaklar yüzün yanında kısa birer çıkıntıydı ve resim
        // kediden ayırt edilemiyordu; köpeği köpek yapan kulağın uzunluğu.
        Picture("dog",
            (0.36f, 0.24f),
            (0.50f, 0.16f),
            (0.64f, 0.24f),
            (0.86f, 0.30f),
            (0.84f, 0.76f),
            (0.68f, 0.52f),
            (0.64f, 0.80f),
            (0.50f, 0.90f),
            (0.36f, 0.80f),
            (0.32f, 0.52f),
            (0.16f, 0.76f),
            (0.14f, 0.30f)),

        // Fil: <b>önden</b>, iki kocaman kulak ve ortada aşağı inen hortum
        // (6-7-8-9). Yandan çizilmesi denendi ve olmadı: dört ayak, tek
        // hatla iki nokta genişliğinde birer dikene dönüyor ve hortum
        // beşinci bir ayak gibi duruyor. Önden bakınca fili fil yapan iki
        // şey — kulaklar ve hortum — resmin tamamı oluyor.
        Picture("elephant",
            (0.50f, 0.14f),
            (0.72f, 0.20f),
            (0.90f, 0.34f),
            (0.86f, 0.66f),
            (0.68f, 0.62f),
            (0.64f, 0.76f),
            (0.58f, 0.92f),
            (0.42f, 0.92f),
            (0.36f, 0.76f),
            (0.32f, 0.62f),
            (0.14f, 0.66f),
            (0.10f, 0.34f),
            (0.28f, 0.20f)),

        // Kelebek: dört kanat, ortada iki kez daralan bir bel (5 ve 11).
        // Bel noktaları merkeze fazla yaklaşırsa kanatlar küçülüp resim
        // papyona dönüyor. Kütüphanenin en çok noktalı resmi.
        Picture("butterfly",
            (0.50f, 0.28f),
            (0.32f, 0.10f),
            (0.08f, 0.28f),
            (0.20f, 0.46f),
            (0.42f, 0.52f),
            (0.12f, 0.64f),
            (0.26f, 0.88f),
            (0.50f, 0.74f),
            (0.74f, 0.88f),
            (0.88f, 0.64f),
            (0.58f, 0.52f),
            (0.80f, 0.46f),
            (0.92f, 0.28f),
            (0.68f, 0.10f)),
    ];

    /// <summary>Nokta sayısı verilen aralıkta olan resimler.</summary>
    public static IReadOnlyList<DotPicture> Between(int minDots, int maxDots) =>
        [.. All.Where(p => p.Count >= minDots && p.Count <= maxDots)];

    public static DotPicture? Find(string id) => All.FirstOrDefault(p => p.Id == id);

    private static DotPicture Picture(string id, params (float X, float Y)[] dots) =>
        new(id, [.. dots.Select(d => new DotPoint(d.X, d.Y))]);
}
