# Ploofy

[Deutsch](README.md) · **Türkçe**

2-9 yaş çocuklar için reklamsız mini oyun koleksiyonu. .NET MAUI
(Android + iOS).

Sekiz eğlendirici, dokuz öğretici mini oyun; tek bir zorluk ekseni (üç yaş
bandı), tek bir yıldız koleksiyonu, aylık abonelik. Sunucu yok, hesap yok,
reklam yok: çocuğa ait hiçbir veri cihazdan çıkmıyor.

## Depo düzeni

```
Ploofy.sln
Directory.Build.props          Bütün projelerin ortak derleme ayarları
src/Ploofy.Engine/             Oyun mantığı — arayüze sıfır bağımlılık, net10.0
src/Ploofy.Data/               SQLite ilerleme deposu (sqlite-net), net10.0
src/Ploofy.Ui/                 Ortak MAUI arayüz katmanı (tema, ses/titreşim,
                               ebeveyn kilidi, yıldız denetimleri)
src/Ploofy.App/                MAUI uygulaması (Android + iOS; Windows yalnızca
                               geliştirirken hızlı denemek için)
content/strings.tsv            Üç dilin bütün metinleri — tek kaynak
docs/store/                    Gizlilik politikası, Impressum ve açılış sayfası —
                               yayımlanan sayfaların kaynağı
tools/build_strings.py         strings.tsv -> Resources/Strings/*.resx
tools/build_sounds.py          geri bildirim seslerini üretir -> Resources/Raw/sounds/
tests/Ploofy.Engine.Tests/     xUnit — motor + depo testleri
```

`Ploofy.Engine` bilerek MAUI'den bağımsız: kurallar masaüstünde saniyeler
içinde sınanabiliyor ve ileride ikinci bir oyun ailesi (ör. okul öncesi
matematik için ayrı bir uygulama) motora dokunmadan bunun üstüne kurulabilir.

## Motorun temel kavramları

| Kavram | Dosya | Ne yapıyor |
|---|---|---|
| `AgeBand` | `Engine/AgeBand.cs` | Filiz (2-4), Fidan (4-6), Meşe (6-9). Uygulamanın zorluk ekseni ve ebeveynin ayarladığı tek şey. |
| `BandValue<T>` | `Engine/Difficulty/` | Bir oyunun her knob'unun banda göre değeri. Zorluk tablosu tek satırda duruyor. |
| `DifficultyProfile` | `Engine/Difficulty/` | Her oyunun uyduğu ortak sözleşme: kaybedilebilir mi, süre görünüyor mu, yazı kullanılıyor mu. |
| `AdaptiveDifficulty` | `Engine/Difficulty/` | Bandın **içindeki** kademe: aynı oyunu üst üste üç kez hatasız bitiren onu bir kademe zor oynuyor. Tur geçmişinden türetiliyor, saklanmıyor. |
| `GameCatalog` | `Engine/Catalog/` | Bütün oyunların tek kaydı. Kilit, bant filtresi ve ebeveyn ekranı buradan besleniyor. |
| `TurnController` | `Engine/Sessions/` | Sırayı, turları ve puanı yürüten tek yer. Tek kişilik oyunda da aynı sınıf çalışıyor. |
| `ISessionTransport` | `Engine/Sessions/` | Oturum olaylarının kanalı. Bugün cihaz içi; yerel ağ ve aile bağlantısı arkasına takılacak. |
| `Entitlements` | `Engine/Access/` | Katman kurallarının tek karar yeri. Hiçbir ekran "abone mi" sorusuna kendi cevap vermiyor. |
| `ParentalGateChallenge` | `Engine/Access/` | Ebeveyn kilidi. Meşe bandının üstünde bir aritmetik sorusu. |
| `StarRating` | `Engine/Progress/` | Turdan yıldız çıkaran tek yer. Kural bantla birlikte değişiyor. |
| `BubblePopRound` | `Engine/Games/` | Balonların doğması, yükselmesi ve patlaması. Konumlar normalize, yani ekran boyutundan bağımsız; testte saat elle ileri alınıyor. |
| `TracePath` | `Engine/Games/Tracing/` | Parmakla takip edilen çizgi: tolerans, geri gitmeyen ilerleme ve çıkış sayımı. Yolu Bul ile Harf Yazma bu mekaniği paylaşıyor. |
| `GlyphShapes` | `Engine/Games/Tracing/` | Büyük harflerin ve rakamların yazım yolları: öğretilen sırayla darbeler, ayrıca çizilmeyen işaretler (İ'nin noktası, Ç'nin çengeli). |
| `PatternRound` | `Engine/Games/` | Tekrar eden dizide bir boşluk. Birim (AB, AAB, ABC, AABB) ve boşluğun yeri banda bağlı; boşluktan önce her zaman en az bir tam birim duruyor. |
| `PlayReport` | `Engine/Progress/` | Ebeveyn raporu: gün çubukları, toplamlar ve dönemin oyun listesi. Her tur ayrı ayrı 15 dakikada kırpılıyor ki unutulmuş bir uygulama raporu yiyip bitirmesin. |
| `LineUpRound` | `Engine/Games/` | Sıralama ve karşılaştırma. En küçük bant boyuta göre diziyor (saymadan), ortadan itibaren miktara göre; her soruda yalnızca sıralanan özellik değişiyor. Tur kendiliğinden ilerlemiyor — `NextPuzzle` arayüzün işi. |

### Yeni bir mini oyun eklemek

1. `Engine/Catalog/GameCatalog.cs` içine bir satır (id, etkileşim türü, katman,
   en küçük bant, çizim tekniği).
2. Kuralları `Engine/Games/` altında arayüzden bağımsız bir sınıf olarak yaz;
   zorluk knob'larını `BandValue<T>` ile tanımla. Görünüm modelinde `ForBand`
   çağrısına **`player.KnobBand`** ver, `player.Band` değil — bant içi uyarlama
   tam orada devreye giriyor.
3. Uygulama tarafında id'ye karşılık bir sayfa, `GamePresentation` içine bir
   satır (ad, simge, rota) ve `content/strings.tsv` içine üç dilde ad.

Oyun sürekli hareket hâlindeyse çizimi `Ploofy.Ui/Controls` altında
`SKCanvasView` olarak yaz ve `Painting/` içindeki hazır parçaları kullan
(`BubblePainter`, `ParticleField`, `PloofyPalette`).

Kilit, bant filtresi, yıldız kaydı ve ebeveyn ekranı kendiliğinden çalışıyor.

## Oyun koleksiyonu

**Eğlendirici oyunlar (8):** Eşleştirme Kartları · Balon Patlatma ·
Şekil Ayırma · Sırayı Tekrarla · Sepeti Tut · Yolu Bul · Yapboz · Boyama

**Öğretici oyunlar (9):** Harf Avı · Sayı Avı · Say ve Eşleştir ·
Harf Yazma · Örüntü · Sırala · Noktaları Birleştir · Kategori Ayırma ·
Basit Toplama

On yedisi de oynanabiliyor.

**Boyama** tek serbest oyun: doğru cevap yok, yanlış yok, süre yok. Bir alanı
istediği renge boyayan çocuk hiçbir zaman yanlış yapmıyor. Bu yaşta —
özellikle Filiz bandında — oyunun işi tam olarak bu.

Beş ayrı etkileşim türünü kapsıyorlar (dokunma, sürükleme, çizgi takibi,
hafıza, sıra) — bu ölçü "her şey aynı hissettiriyor" sorununu en baştan
çözüyor.

**Çizim tekniği:** kart ve kutucuk tabanlı oyunlar MAUI denetimleriyle;
sürekli hareket, parçacık ya da serbest çizim isteyen her şey (Balon
Patlatma, Yolu Bul, Yapboz, Sepeti Tut, Harf Yazma, Sırala) SkiaSharp ile.

Yerleşimin kendisi bir soru olduğunda — ebeveyn raporundaki çubuk, Sırala'daki
dizi — çizim `Ploofy.Ui/Painting` altında MAUI'siz duruyor (`TrendPainter`,
`LineUpPainter`). Aynı sınıflar küçük bir konsol programından PNG'ye çizilip
gözle bakılabiliyor; orada birkaç yerleşim hatası tam olarak böyle bulundu.

## Görsel dil

Kategorinin en iyileri (Sago Mini, Toca Boca, Khan Academy Kids) tek bir
noktada ayrılıyor: ekranda hiçbir şey kıpırtısız durmuyor. Ploofy'nin tasarım
kuralları `Ploofy.Ui` içinde ve bütün oyunlar için ortak:

| Kural | Nerede | Neden |
|---|---|---|
| Hiçbir yüzey tek renk değil | `Theme/PloofyStyles.xaml` içindeki degradeler | Degrade + gölge, kutucuğu "basılmış bir resim" olmaktan çıkarıp dokunulacak bir şeye çeviriyor |
| Şeyler belirmez, zıplayarak gelir | `BubbleSurface.BirthScale`, `MemoryCardView` | Doğrusal büyüme solma gibi duruyor; hafif bir aşma "hop, geldi" hissi veriyor |
| Duran şey de nefes alır | `BubbleSurface` içindeki gerinme evresi | Her balonun kendi faz kayması var; hepsi aynı anda gerinirse mekanik duruyor |
| Her dokunuşun görünür bir sonucu var | `ParticleField` | Balon yok olmuyor, dağılıyor — başarı hissinin tamamı orada |
| Yanlış cezalandırılmaz, gösterilir | Yanlış renk patlamıyor, silkeleniyor | Patlasaydı yanlış da bir ödül olurdu |
| Dokunma hedefi ≥ 64 birim | `TouchTarget` | Küçük çocuğun parmağı büyük, isabeti düşük |
| Koyu tema yok | `PloofyColors.xaml` | Küçük ekranda koyu zemin renk ayrımını ve okunabilirliği düşürüyor |

Balonun kendisi dört katman: yumuşak gölge, sol üstten ışık alan gövde
degradesi, ince kenar halkası ve iki parlama lekesi. Cam hissi yalnızca son
ikisinden geliyor.

## Yıldızlar ve koleksiyon

Yıldızlar uzun süre bir karşılığı olmadan birikti. Artık **toplam** sayı yeni
avatarlar açıyor — üç yıldızda bir, toplam yirmi tane. İlki kusursuz bir
turdan sonra geliyor ki kural açıklamasız görünür olsun.

Merdiven `Engine/Progress/RewardLadder.cs` içinde, avatarların sırası
`App/Services/AvatarCatalog.cs` içinde. **Yeni tablo yok:** neyin açıldığı her
zaman toplam yıldızdan türetiliyor. Saklanan tek şey kutlamanın en son nerede
kaldığı (ayarlarda `rewards_seen:<profil-id>`) — böylece hiçbir ödül
kaybolmuyor ve hiçbiri iki kez kutlanmıyor.

Koleksiyon ekranı ebeveyn kilidinin arkasında **değil**: hak ettiği figür için
ebeveyn çağırmak zorunda kalan çocuk ödülün yarısını kaybediyor.

## Oyun süresi sınırı

Ebeveyn her çocuk için **günlük bir oyun süresi** koyabiliyor. Süre dolduğunda
ana ekran oyun listesi yerine dinlenme kartı gösteriyor; ertesi gün
kendiliğinden açılıyor. Böylece her akşam yeniden pazarlık edilmiyor.

Meseleyi taşıyan üç kural:

- **Varsayılan kapalı.** Sınır açık gelseydi güncellemeden sonra bütün çocuklar
  birden kilitlenir ve kimse sebebini bilmezdi. Açarken uygulama banda göre bir
  değer öneriyor (15 / 20 / 30 dakika) — öneri, dayatma değil.
- **Tur ortasında asla kesilmiyor.** Kontrol yalnızca turlar *arasında*.
  Yapbozun ortasında kilitlenen çocuk uygulamayı haksız buluyor — ve o his
  sınırın kendisinden uzun kalıyor.
- **Görünür geri sayım yok.** Bu yaşta azalan bir saat baskı üretiyor. Yerine
  tur sonu ekranı bir kez "bir oyun daha var", sonra "bugünlük bu kadar"
  diyor.

Sayılan şey **oyun içinde** geçen süre, menüdeki değil: kaynak
`round_history`, yani ebeveyn raporuyla aynı tablo — iki ayrı sayı er geç
birbiriyle çelişirdi. Hesap `Engine/Progress/ScreenTimeBudget.cs` içinde,
sınır ayarlarda profil başına (`screen_time:<profil-id>`).

Sınır aboneliğe **bağlı değil**. Bir çocuk koruma özelliğini ödeme duvarının
arkasına koymak, bir çocuk uygulamasında savunulabilir bir şey olmazdı.

## Bant içi uyarlama

Üç bant kaba. Aynı oyunu üst üste üç kez **hatasız** bitiren çocuk ondan artık
bir şey öğrenmiyor — o yüzden tam olarak o oyun bir kademe zorlaşıyor ve
oradan sonra bir üst bandın knob'larını okuyor (`AdaptiveDifficulty`,
`Player.KnobBand`).

Bu sırada **değişmeyenler:**

- **Bandın kendisi.** Bant ebeveynindir; uygulama onu asla kaydırmıyor.
- **`DifficultyProfile`.** Yukarı çıkan bir Filiz çocuğu daha çok parça
  görüyor ama yine saat, yazı ve kaybetme görmüyor — bunlar zorluk değil, yaş
  kuralları.
- **Yıldızlar.** Değerlendirme gerçek bantla yapılıyor, yani zorlaşan tur
  ödülü kısmıyor.
- **İçerik.** Harf ve sayı havuzları gerçek banttan gelmeye devam ediyor;
  yalnızca knob'lar yürüyor.

Meseleyi taşıyan dört karar:

- **Saklanmıyor, türetiliyor.** Kademe her oyun açılışında son turlardan
  yeniden hesaplanıyor. Yanlış duran saklanmış bir değer kendi kendine
  düzelmiyor; türetilen ise ilk zor turda kendiliğinden geri iniyor.
- **Ölçüt kusursuz tur, yıldız değil.** Yıldız üç bantta üç ayrı şey ifade
  ediyor — Filiz'de bitiren herkes üç yıldız alıyor. Üç yıldız **ve** sıfır
  hata üçünde de aynı şeyi söylüyor.
- **Çocuk başına değil, oyun başına.** Yapbozda sağlam olan Yolu Bul'da daha
  uzun süre sağlam değil.
- **Görünür.** Sessizce zorlaşan bir oyun ebeveyne hata gibi görünüyor ("dün
  yapabiliyordu ama"). Bu yüzden: kutucukta bir ↑, tur sonu ekranında bir
  cümle, ebeveyn raporunda bir işaret — ve profilde bunu tümüyle kapatan bir
  anahtar (varsayılan: açık).

Sınır: Meşe en üst bant, orada kaçılacak bir sütun yok. Dördüncü bir sayı
uydurmak, eksik kademeden kötü olurdu.

## Katmanlar

| | Ücretsiz | Abonelik |
|---|---|---|
| Oyunlar | 2 (Eşleştirme Kartları, Balon Patlatma) | 17 + sonradan eklenen her şey |
| Çocuk profili | 1 | 4 |
| Reklam | **Yok** | **Yok** |
| Çevrimdışı | Evet | Evet + içerik paketleri |

Abonelik kendi ekranından yönetiliyor (`SubscriptionPage`, `subscription`
rotası): durum, ödenmiş dönemin sonu, mağazanın abonelik yönetimine çıkış ve
**bitirme**. Bitirme yalnızca otomatik yenilemeyi kapatıyor — oyunlar ödenmiş
dönem sonuna kadar açık kalıyor (`SubscriptionStatus.Canceled`), yıldızlar,
rozetler ve profiller her koşulda duruyor. Gerçek billing bağlandığında
uygulama kendisi iptal etmiyor: Play ve App Store buna yalnızca kendi
abonelik merkezlerinde izin veriyor, o yüzden yol oraya çıkıyor.

## Çok oyunculu mod

Aynı `ISessionTransport` arayüzünün arkasında üç mod:

- **Aynı cihazda sırayla (pass-and-play)** — 1. fazdan beri çalışıyor. Ne
  internet, ne hesap, ne eşleşme istiyor. Her turdan önce bir devir ekranı
  var; bu ara adım olmadan çocuk yanlışlıkla kardeşinin turunu oynuyor.
- **Yerel ağ** — 2. faz. QR kod ya da yakındaki cihaz keşfiyle, yalnızca
  fiziksel olarak aynı odadaki biriyle. İnternetten yabancı yok.
- **Ebeveyn onaylı aile bağlantısı** — 3. faz.

Bir oturumdaki her çocuk **kendi bandında** oynuyor: küçük kardeş Filiz,
büyük kardeş Meşe olarak aynı turu paylaşabiliyor.

## Platform ve yasal gereklilikler

Bu maddeler en baştan mimariye giriyor, sonradan yapıştırılmıyor:

- **Reklam yok** — hiçbir katmanda. `Entitlements.ShowsAds` sabit olarak
  `false`; bunu değiştirmek ürün vaadini değiştirmek olur ve testte yakalanır.
- **Veri toplanmıyor** — ne reklam kimliği (AAID/IDFA), ne seri numarası,
  MAC/IMEI ya da konum toplanıyor veya gönderiliyor. Profiller yalnızca
  cihazda kalıyor; takma ad kullanılıyor, gerçek ad sorulmuyor.
- **Oyun raporu cihazda kalıyor.** Rapor var olduğundan beri biten her tur
  `round_history` içine yazılıyor (oyun, yıldız, süre, zaman). Tablo yalnızca
  yerelde ve ebeveyn kilidinin arkasında okunuyor, profille birlikte siliniyor
  (`DeleteProfileAsync`). Hiçbir yere gitmiyor — gizlilik politikası olduğu
  gibi geçerli.
- **Ebeveyn kilidi** — satın alma, aboneliği bitirme, ayarlar, profil yönetimi
  ve uygulamadan dışarı çıkan her bağlantı (gizlilik politikası, Impressum,
  mağazanın abonelik yönetimi; adresler `Services/PloofyLinks.cs` içinde)
  `ParentalGateChallenge` arkasında.
- **Abonelik** — mağazanın hesabına bağlı; uygulamanın kendi hesabı ve kendi
  sunucusu yok.
- **Yaş beyanı** — Play Console'daki hedef yaş grubu ve App Store'daki Kids
  kategorisi yayından önce doğru şekilde beyan ediliyor.
- **Gizlilik politikası** — yayımlandı. Diğer uygulamalarda olduğu gibi
  **ayrı bir depoda** duruyor ve oradan GitHub Pages ile sunuluyor; Play bunun
  için herkese açık bir URL istiyor:

  - <https://alikiratli.github.io/ploofy-web/privacy-policy.html> (tek sayfada
    de/tr/en) — Play Console'a kökü değil bu bağlantı giriyor
  - <https://alikiratli.github.io/ploofy-web/impressum.html> (de/en)
  - <https://alikiratli.github.io/ploofy-web/> — mağaza kaydındaki "Website"
    alanı için kök

  Üç sayfanın kaynağı burada, `docs/store/` altında;
  <https://github.com/alikiratli/ploofy-web> deposu yalnızca Play'in okuduğu
  kopya. Bir değişiklikten sonra iki yer de eşitlenmeli — yöntem o deponun
  README'sinde.

## Kurulum

```bash
# MAUI workload — YÖNETİCİ olarak açılmış bir terminal gerekiyor
dotnet workload install maui

dotnet restore
dotnet test

# Windows'ta hızlı deneme (geliştirme hedefi, mağazaya gitmiyor)
dotnet build src/Ploofy.App/Ploofy.App.csproj -f net10.0-windows10.0.19041.0
```

### Android

Android SDK ve **JDK 17** gerekiyor — sistemdeki daha yeni bir JDK kabul
edilmiyor. Yollar makineye bağlı olduğu için depoda durmuyorlar; kök dizine
bir `Ploofy.local.props` koy:

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
dotnet build src/Ploofy.App/Ploofy.App.csproj -t:InstallAndroidDependencies -f net10.0-android -p:AcceptAndroidSDKLicenses=True
```

Cihaza ya da emülatöre kurup çalıştırmak için:

```bash
dotnet build src/Ploofy.App/Ploofy.App.csproj -f net10.0-android -t:Run
```

### Yayın imzası

Anahtar tanımlı değilse sürüm paketi de **hata ayıklama sertifikasıyla**
imzalanıyor. Cihazda denemeye yetiyor, Play reddediyor. Derleme bu durumda
uyarı veriyor.

Anahtar bir kez üretiliyor — depoya **girmiyor** (`.gitignore` `*.keystore` ve
`*.jks` dosyalarını zaten dışarıda tutuyor) ve kaybedilmesi, uygulamanın bir
daha güncellenememesi demek:

```bash
keytool -genkeypair -v -keystore ploofy-release.keystore -alias ploofy \
  -keyalg RSA -keysize 2048 -validity 10000
```

Dört değer dışarıdan geliyor, ya `Ploofy.local.props` ile ya da aynı adlı
ortam değişkenleriyle (CI için):

```xml
<Project>
  <PropertyGroup>
    <PloofyKeystore>C:\...\ploofy-release.keystore</PloofyKeystore>
    <PloofyKeystoreAlias>ploofy</PloofyKeystoreAlias>
    <PloofyKeystorePassword>...</PloofyKeystorePassword>
    <PloofyKeyPassword>...</PloofyKeyPassword>
  </PropertyGroup>
</Project>
```

Play için paketi üretmek ve imzayı doğrulamak:

```bash
dotnet publish src/Ploofy.App/Ploofy.App.csproj -c Release -f net10.0-android -p:AndroidPackageFormat=aab
apksigner verify --print-certs src/Ploofy.App/bin/Release/net10.0-android/io.ploofy.app-Signed.apk
```

Orada `CN=Android Debug` yazıyorsa derleme anahtarı görmemiş demektir.

Metin değişikliğinden sonra resx dosyaları yeniden üretiliyor (elle
düzenlenmiyorlar):

```bash
python tools/build_strings.py content/strings.tsv src/Ploofy.App/Resources/Strings
```

Sesler de üretiliyor — depoda hazır duruyorlar, yalnızca tını değişirse
yeniden çalıştır:

```bash
python tools/build_sounds.py
```

## İlerleme notu

Nerede kalındığı ve sırada ne olduğu `ilerleme notu.docx` içinde; **her
oturumun sonunda güncelleniyor.** Kaynağı `content/ilerleme-notu.md` — docx
üretilmiş çıktı ve elle düzenlenmiyor:

```bash
python tools/build_progress_note.py
```

## Yol haritası

- **1. Faz — İskelet.** Motor + veri katmanı + testler ✅ · MAUI kabuğu, üç
  dil, profil akışı, ana ekran, Eşleştirme Kartları uçtan uca (sıralı oyun ve
  yıldız kaydı dahil), ebeveyn kilidi, ayarlar, abonelik ekranı ✅ · Balon
  Patlatma ve ortak görsel dil ✅ · Android tablette uçtan uca doğrulandı ✅ ·
  ses dosyaları ✅
- **2. Faz — Çeşitlilik.** Kalan 9 mini oyun, hepsi aynı bant API'siyle.
  Sonunda "10 oyun, 3 bant, 1 yıldız koleksiyonu" duruyor.
- **3. Faz — Ebeveyn ve uyumluluk.** Ayarlar, abonelik akışı, veri toplama
  denetimi, yerel ağda eşleşme.
- **4. Faz — Rötuş ve yayın.** Uygulama simgesi ve açılış ekranı ✅ · gizlilik
  politikası ve Impressum yayımlandı ✅ · yayın anahtarı üretildi, sürüm
  paketi onunla imzalandı ✅ · mağaza metinleri, Data safety ve yaş
  derecelendirme cevapları `docs/store/listing.md` içinde ✅ · gerçek cihaz
  testi, gerçek satın alma (Plugin.InAppBilling), iOS, tema paketleri, mağaza
  görselleri, yayın ⏳
