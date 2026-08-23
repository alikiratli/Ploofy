# Ploofy

2-9 yaş çocuklar için reklamsız mini oyun koleksiyonu. .NET MAUI (Android + iOS).

Yedi eğlendirici, üç öğretici mini oyun; tek bir zorluk ekseni (üç yaş bandı),
tek bir yıldız koleksiyonu, aylık abonelik. Sunucu yok, hesap yok, reklam yok:
çocuğa ait hiçbir veri cihazdan çıkmıyor.

## Depo düzeni

```
Ploofy.sln
Directory.Build.props          Bütün projelerin ortak derleme ayarları
src/Ploofy.Engine/             Oyun mantığı — UI'ya sıfır bağımlılık, net9.0
src/Ploofy.Data/               SQLite ilerleme deposu (sqlite-net), net9.0
src/Ploofy.Ui/                 Ortak MAUI arayüz katmanı (tema, ses/haptik,
                               ebeveyn kilidi, yıldız kontrolü)
src/Ploofy.App/                MAUI uygulaması (Android + iOS; Windows yalnızca
                               geliştirme sırasında denemek için)
content/strings.tsv            Üç dilin metinleri — tek kaynak
tools/build_strings.py         strings.tsv -> Resources/Strings/*.resx
tests/Ploofy.Engine.Tests/     xUnit — motor + depo testleri
```

`Ploofy.Engine` bilerek MAUI'den bağımsız: kurallar masaüstünde saniyeler içinde
test edilebiliyor ve ileride ikinci bir oyun ailesi (ör. okul öncesi matematik
odaklı ayrı bir uygulama) aynı motoru bozmadan üzerine kurulabiliyor.

## Motorun temel kavramları

| Kavram | Dosya | Ne yapar |
|---|---|---|
| `AgeBand` | `Engine/AgeBand.cs` | Filiz (2-4), Fidan (4-6), Meşe (6-9). Uygulamanın **tek** zorluk ekseni. |
| `BandValue<T>` | `Engine/Difficulty/` | Oyundaki her knob'un banda göre değeri. Zorluk tablosu tek satırda okunur. |
| `DifficultyProfile` | `Engine/Difficulty/` | Her oyunun uyması gereken ortak sözleşme: kaybetme var mı, süre görünür mü, yazı kullanılır mı. |
| `GameCatalog` | `Engine/Catalog/` | Bütün oyunların tek kaydı. Kilit, bant filtresi, ebeveyn ekranı buradan beslenir. |
| `TurnController` | `Engine/Sessions/` | Sırayı, turları ve puanları yürüten tek yer. Tek kişilik oyunda da aynı sınıf çalışır. |
| `ISessionTransport` | `Engine/Sessions/` | Oturum olaylarının kanalı. Bugün cihaz içi; yerel ağ ve aile bağlantısı buranın arkasına takılacak. |
| `Entitlements` | `Engine/Access/` | Katman kurallarının tek karar noktası. Hiçbir ekran "abone mi?" diye kendi karar vermez. |
| `ParentalGateChallenge` | `Engine/Access/` | Ebeveyn kilidi. Meşe bandının üstünde bir aritmetik engel. |
| `StarRating` | `Engine/Progress/` | Turdan yıldıza çeviren tek yer. Kural bantla değişir. |
| `BubblePopRound` | `Engine/Games/` | Balonların doğması, yükselmesi, patlaması. Konumlar normalleştirilmiş, yani ekran boyutundan bağımsız ve testte saat elle ilerletilerek doğrulanıyor. |

### Yeni mini oyun eklemek

1. `Engine/Catalog/GameCatalog.cs` içine bir satır (id, etkileşim türü, katman,
   en küçük bant, çizim tekniği).
2. Kuralları `Engine/Games/` altında, arayüzden bağımsız bir sınıf olarak yaz;
   zorluk knob'larını `BandValue<T>` ile tanımla.
3. Uygulama tarafında id'ye karşılık bir sayfa, `GamePresentation` içinde bir
   satır (ad, simge, rota) ve `content/strings.tsv` içinde üç dilde bir ad.

Sürekli hareketli bir oyunsa çizimi `Ploofy.Ui/Controls` altında bir
`SKCanvasView` olarak yaz ve `Painting/` içindeki hazır parçaları kullan
(`BubblePainter`, `ParticleField`, `PloofyPalette`).

Kilit, bant filtresi, yıldız kaydı ve ebeveyn ekranı otomatik çalışır.

## Oyun kütüphanesi

**Eğlendirici (7):** Eşleştirme Kartları ✅ · Balon Patlatma ✅ · Şekil Ayırma ·
Yolu Bul · Yapboz · Sırayı Tekrarla · Sepeti Tut

**Öğretici (3):** Harf Avı · Sayı Avı · Say ve Eşleştir

Beş farklı etkileşim türünü kapsıyor (dokunma, sürükleme, çizgi takibi, hafıza,
sıra) — "hepsi aynı hissettiriyor" sorununu baştan çözen ölçüt bu.

**Çizim tekniği:** kart/kutucuk tabanlı oyunlar MAUI kontrolleriyle; sürekli
hareket, parçacık ve serbest çizim gerektirenler (balon patlatma, yolu bul,
yapboz, sepeti tut) SkiaSharp ile.

## Görsel dil

Kategorinin en iyileri (Sago Mini, Toca Boca, Khan Academy Kids) tek bir şeyde
ayrışıyor: ekrandaki hiçbir şey durağan değil. Ploofy'nin görsel kuralları
`Ploofy.Ui` içinde ve bütün oyunlar için ortak:

| Kural | Nerede | Neden |
|---|---|---|
| Hiçbir yüzey düz renk değil | `Theme/PloofyStyles.xaml` degradeleri | Degrade + gölge, kutucuğu "basılı resim" olmaktan çıkarıp dokunulacak nesneye çeviriyor |
| Nesneler belirmez, yaylanarak gelir | `BubbleSurface.BirthScale`, `MemoryCardView` | Doğrusal büyüme "beliriyor" gibi duruyor; hafif taşma "pat diye çıkıyor" hissi veriyor |
| Duran nesne bile nefes alır | `BubbleSurface` esneme fazı | Her balonun kendi faz kayması var; hepsi aynı anda esnerse mekanik görünüyor |
| Her dokunuşun görünür bir sonucu var | `ParticleField` | Balon yok olmuyor, dağılıyor — tatmin hissinin tamamı burada |
| Yanlış cezalandırmaz, uyarır | Yanlış renk patlamaz, silkelenir | Patlasaydı yanlış da bir ödül olurdu |
| Dokunma hedefi ≥ 64 birim | `TouchTarget` | Küçük çocuğun parmağı büyük, isabeti düşük |
| Karanlık tema yok | `PloofyColors.xaml` | Küçük ekranda karanlık zemin renk ayrımını ve okunurluğu düşürüyor |

Balonun kendisi dört katman: yumuşak gölge, ışığı sol üstten alan gövde
degradesi, ince kenar halkası, iki parlama lekesi. "Cam" hissini veren tek şey
son ikisi.

## Katmanlar

| | Ücretsiz | Abonelik |
|---|---|---|
| Oyun | 2 (Eşleştirme Kartları, Balon Patlatma) | 10 + sonra eklenenler |
| Çocuk profili | 1 | 4 |
| Reklam | **Yok** | **Yok** |
| Çevrimdışı | Var | Var + içerik paketleri |

## Çok oyunculu

Üç mod aynı `ISessionTransport` arayüzünün arkasında:

- **Aynı cihazda sırayla (pass-and-play)** — Faz 1'de çalışıyor. İnternet, hesap
  ya da eşleşme gerektirmiyor. Her turdan önce bir devir ekranı var; bu ara adım
  olmadan çocuk kardeşinin turunu yanlışlıkla oynuyor.
- **Yerel ağ** — Faz 2. QR ya da yakındaki cihaz keşfiyle, yalnızca fiziksel
  olarak aynı odadaki biriyle. İnternetten yabancı bulma yok.
- **Ebeveyn onaylı aile bağlantısı** — Faz 3.

Oturumdaki her çocuk **kendi bandında** oynar: küçük kardeş Filiz, büyük kardeş
Meşe olarak aynı turu paylaşabilir.

## Platform ve yasal uyum

Bu maddeler mimariye baştan giriyor, sonradan eklenmiyor:

- **Reklam yok** — hiçbir katmanda. `Entitlements.ShowsAds` sabit `false`;
  değişmesi ürün vaadinin değişmesi demek ve teste takılır.
- **Veri toplama yok** — reklam kimliği (AAID/IDFA), cihaz seri no, MAC/IMEI ya
  da konum toplanmıyor/iletilmiyor. Profiller yalnızca cihazda; takma ad
  kullanılıyor, gerçek ad istenmiyor.
- **Ebeveyn kilidi** — satın alma, ayarlar, profil yönetimi ve uygulamadan çıkan
  her bağlantı `ParentalGateChallenge` arkasında.
- **Abonelik** — mağaza hesabına bağlanır; uygulamanın kendi hesabı/sunucusu yok.
- **Yaş beyanı** — Play Console'da hedef yaş grubu ve App Store Kids kategorisi
  yayın öncesi doğru beyan edilecek.

## Kurulum

```bash
# MAUI workload — YÖNETİCİ olarak açılmış bir terminal gerektirir
dotnet workload install maui

dotnet restore
dotnet test

# Windows'ta hızlı deneme (mağazaya çıkmayan geliştirme hedefi)
dotnet build src/Ploofy.App/Ploofy.App.csproj -f net9.0-windows10.0.19041.0
```

### Android

Android SDK ve **JDK 17** gerekiyor — sistemdeki daha yeni bir JDK kabul
edilmiyor. Yollar makineye özel olduğu için depoya girmiyor; kök dizine
`Ploofy.local.props` oluştur:

```xml
<Project>
  <PropertyGroup>
    <AndroidSdkDirectory>C:\Users\...\AppData\Local\Android\Sdk</AndroidSdkDirectory>
    <JavaSdkDirectory>C:\Users\...\.jdks\ms-17.0.15</JavaSdkDirectory>
  </PropertyGroup>
</Project>
```

Android SDK kurulu değilse:

```bash
dotnet build src/Ploofy.App/Ploofy.App.csproj -t:InstallAndroidDependencies -f net9.0-android -p:AcceptAndroidSDKLicenses=True
```

Cihaza ya da emülatöre kurup çalıştırmak:

```bash
dotnet build src/Ploofy.App/Ploofy.App.csproj -f net9.0-android -t:Run
```

Metinler değişince resx dosyalarını yeniden üret (elle düzenlenmiyorlar):

```bash
python tools/build_strings.py content/strings.tsv src/Ploofy.App/Resources/Strings
```

## Yol haritası

- **Faz 1 — İskelet.** Motor + veri katmanı + testler ✅ · MAUI kabuğu, üç dil,
  profil akışı, ana ekran, Eşleştirme Kartları uçtan uca (sıralı oyun ve yıldız
  kaydı dahil), ebeveyn kilidi, ayarlar, abonelik ekranı ✅ · Balon Patlatma ve
  ortak görsel dil ✅ · Android tablette uçtan uca doğrulandı ✅ ·
  ses varlıkları ⏳
- **Faz 2 — Çeşitlilik.** Kalan 9 mini oyun, hepsi aynı bant API'siyle. Sonunda
  "10 oyun, 3 bant, 1 yıldız koleksiyonu" tamam.
- **Faz 3 — Ebeveyn ve uyum.** Ayarlar, abonelik akışı, veri toplama denetimi,
  yerel ağ eşleşmesi.
- **Faz 4 — Cila ve yayın.** Ses/animasyon cilası, tema paketleri, mağaza
  görselleri, yaş beyanı, yayın.
