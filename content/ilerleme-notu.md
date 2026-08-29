# Ploofy — İlerleme Notu

Son güncelleme: 29.08.2026
Depo: https://github.com/alikiratli/Ploofy (public)

## 1. Nerede kaldık

**Faz 2 bitti: on oyunun onu da oynanabilir.** Uygulama Android tablette
uçtan uca çalışıyor — çocuk profili oluşturuluyor, oyun seçiliyor,
oynanıyor, yıldız kazanılıyor ve kayıt cihazda tutuluyor. Katalogda artık
"yakında" olarak duran hiçbir oyun yok ve kütüphane beş etkileşim türünün
beşini de kapsıyor.

**Faz 4 sürüyor: uygulama Play'in 2026 kapılarına uyduruldu.** Bu oturumda
yeni özellik yok denecek kadar az; yapılan iş uygulamayı mağazanın bugün
geçerli zorunluluklarına taşımak ve ilk kez gerçek bir **sürüm (Release)**
paketi üretip çalıştırmak oldu.

**Son oturumda yapılan:** .NET 10'a geçiş, API 36 hedefi, yatay kilidin
korunması, MAUI 10'un kaldırdığı çağrılar, SQLite'ta bir güvenlik açığı ve
yanlış paketlenmiş bir yerel kütüphane. Sırayla:

### Neden .NET 10

31 Ağustos 2026'dan itibaren Play'e yeni uygulama yalnızca **API 36**
hedefliyorsa kabul ediliyor. .NET 9 en fazla API 35 hedefleyebiliyor — yani
bu geçiş bir tercih değil, kapıdan geçmenin tek yolu. Bütün projeler
`net10.0`'a alındı, MAUI iş yükü 10.0.400 ile kuruldu ve paket artık
`targetSdkVersion 36` / `compileSdkVersion 36` ile çıkıyor.

### Yatay kilit ve appCategory

API 36'yı hedefleyen uygulamalarda Android, **sw600dp'den geniş ekranlarda
`screenOrientation`'ı yok sayıyor**. Yani geçişin sessiz bedeli tam hedef
cihazımızda ödenecekti: tablette yatay kilit düşecek, her oyun için ikinci
bir dikey yerleşim gerekecekti.

Oyunlar bu değişiklikten muaf ve muafiyet `android:appCategory` ile
belirleniyor. Manifest'e bir satır eklendi; etiket zaten doğru, uygulama on
mini oyundan oluşuyor. Emülatörde doğrulandı: etkinlik hâlâ 1600×1200'lük
yatay banda yerleşiyor (`topActivityBoundsLetterboxed=true`), yani kilit
yerinde duruyor. API 37'de bu muafiyetin de kalkacağı biliniyor — dikey
yerleşim sorusu ertelendi, kapanmadı.

### MAUI 10'un kaldırdığı çağrılar

`ScaleTo`, `TranslateTo`, `RotateYTo`, `FadeTo` ve `DisplayAlert` kullanımdan
kalktı; yerlerine `...Async` sürümleri geldi. On dokuz çağrı yeri değişti.
İmzalar aynı, hepsi zaten `await` ediliyordu, davranış değişmiyor.

Bu sırada `Directory.Build.props`'ta bir tutarsızlık ortaya çıktı:
"uyarıları hata sayma kuralı MAUI projelerinde geçerli değil" diyen koşul
**hiç çalışmıyor**. Koşul `$(UseMaui)`'ye bakıyor ama Directory.Build.props
projenin kendi gövdesinden önce okunuyor ve o an değişken boş. Yani
`TreatWarningsAsErrors` bütün projelerde açık. Şimdiye kadar sorun
çıkarmamıştı, çünkü .NET 9'da o projeler uyarı üretmiyordu. Dokunulmadı:
kuralın fiilen açık olması bu geçişte işe yaradı — kaldırılacaksa önce
karar, sonra yorumun düzeltilmesi gerekiyor.

### SQLite: bir açık, bir de yanlış kütüphane

`SQLitePCLRaw.bundle_green` 2.1.11'in taşıdığı SQLite'ta yüksek önem
dereceli bir açık var (GHSA-2m69-gcr7-jv3q) ve .NET 10 bunu **derleme
hatasına** çeviriyor. Paketin kendisinin yenisi yok, taşıdığı yerel
kütüphanenin var: `lib.e_sqlite3` 2.1.13 doğrudan referansla sabitlendi.

Sabitleme yeni bir kusur doğurdu ve yakalandı. Paketin içinde Android yapısı
yok, ama RID grafiğinde `android-x64` `linux-x64`'ün altında duruyor; NuGet
masaüstü Linux derlemesini Android için uygun sayıp AAR'dan gelen gerçek
Android kütüphanesinin önüne geçirdi. APK bir anda glibc'ye bağlı bir `.so`
taşımaya başladı. Gözle görülmüyordu — md5 karşılaştırmasıyla çıktı:
paketlenen dosya, paketin `runtimes/linux-x64/native/` kopyasıyla bit bit
aynıydı. `ExcludeAssets="native"` ile yol kapatıldı.

Sonuç yine **sayıyla** doğrulandı: APK'daki 230 `.so` dosyasının 230'u 16 KB
sayfa hizalı ve `libe_sqlite3.so` artık SourceGear 3.53.3'ün Android yapısı.
Hizalama önemli, çünkü Android 15'ten beri 16 KB sayfa desteği Play'in kabul
şartlarından biri; hizalanmamış tek bir kütüphane paketi geri çeviriyor.

### İlk sürüm paketi

Bugüne kadar yalnızca Debug derlenmişti. Release başka bir şey: trimming ve
AOT açık, MAUI'de XAML ile bağımlılık enjeksiyonunu kıran klasik yer orası.

- AAB üretildi (40 MB), API 36 tablet emülatörüne kuruldu ve açıldı; profil
  seçme ekranı düzgün çizildi, çökme yok
- Türkçe uydu kaynağı (`tr/Ploofy.App.resources.dll`) derleme blob'unun
  içinde duruyor — trimming yemedi, üç dil sürümde de sağlam
- Windows geliştirme hedefi de .NET 10'da derleniyor

### Yayın imzası ve sürüm 1.0

Sürüm `0.1` → **`1.0`**; `ApplicationVersion` (Android'de versionCode) 1'de
kaldı, Play'e her yüklemede artması gerekiyor.

İmzalama yapılandırması kuruldu. Dört değer (`PloofyKeystore`,
`PloofyKeystoreAlias`, `PloofyKeystorePassword`, `PloofyKeyPassword`)
dışarıdan geliyor: ya `Ploofy.local.props` ya da aynı adlı ortam
değişkenleri. Anahtar ve parola depoya girmiyor.

Değerler yoksa derleme durmuyor — sürüm paketi hata ayıklama anahtarıyla
çıkıyor, cihazda denemek için bu yeterli. Ama o durumda derleme **uyarı
veriyor**, çünkü asıl tehlike paketin kırılması değil, farkında olmadan
mağazaya yüklenmesi.

İki yol da atılabilir bir anahtarla denendi: anahtar verildiğinde sertifika
`CN=Ploofy Test`, verilmediğinde `CN=Android Debug` ve uyarı görünüyor.
Deneme anahtarı sonra silindi — **gerçek yayın anahtarı henüz üretilmedi**,
o parolayla birlikte size ait.

### Gizlilik politikası

Üç dil tek bir sayfada toplandı ve **ayrı bir depoya** kondu — diğer
uygulamalarda olduğu gibi, GitHub Pages'ten yayımlanacak. Metin uydurulmadı,
koddan çıkarıldı — hangi tablonun hangi alanı tuttuğu (`Entities.cs`), hangi
iznin neden istendiği (manifest), ebeveyn kilidinin neyi kapattığı
(`ParentalGateReason` çağrı yerleri) ve profil silinince nelerin gittiği
(`DeleteProfileAsync` yıldızları ve rozetleri de siliyor) tek tek bakılarak
yazıldı.

İki iddia bu yüzden düzeltildi: README "uygulamadan dışarı çıkan her bağlantı
kilidin arkasında" diyor ama `ExternalLink` gerekçesinin **hiç çağrıldığı yer
yok** — uygulamada şu an dışarı açılan bağlantı da yok, o yüzden politikada
yalnızca gerçekten kilitli olan üç yol sayıldı. İkincisi, kilit "basit bir
aritmetik" değil: iki basamaklı çarpma + toplama, kasıtlı olarak 6–9 bandının
üstünde.

Sayfa tek dosya: dil değiştirici, açık/koyu tema, derleme adımı yok, Google
Fonts dışında dış bağımlılık yok. Renkleri de uydurmadım —
`PloofyPalette.cs` ve `PloofyStyles.xaml`'dan aldım: mürekkep `#402A1E`,
vurgu Sunny'nin koyu ucu `#E08600` (parlak `#FFC733` metin olarak okunmuyor,
yalnızca dolgu), bağlantılar Ocean gölgesi `#0F6FC4`.

Sayfa **taslak** ve iki şeyle öyle olduğunu söylüyor: başlıktaki "Taslak"
rozeti ve sarı işaretli on iki alan (her dilde tarih, yayıncı adı-adresi,
iletişim e-postası). İkisi de yayımdan önce temizlenecek.

Depo yerel olarak hazır ve ilk commit'i atıldı; uzak adresi eklenmedi çünkü
adres henüz belli değil. Yeri: `../ploofy-privacy` (bu deponun kardeşi).

**Sayılar:** 229 test geçiyor · 10 oyun tanımlı, 10'u oynanabilir ·
13 ses dosyası · AAB 40 MB · targetSdk 36 · sürüm 1.0.

**Buradan başla: gerçek tablet.** Emülatör her şeyi gösterdi ama iki şeyi
ölçemiyor — parmağı ve hoparlörü. Tableti USB'den tak, hata ayıklamayı aç,
`dotnet build src/Ploofy.App/Ploofy.App.csproj -f net10.0-android -t:Run`.
Bakılacaklar:

- **Sesler.** Hiçbiri henüz kulakla duyulmadı. Ses seviyeleri birbirine
  göre dengeli mi (dokunuş sesi tekrar tekrar çalıyor, en alçağı o olmalı),
  tuş notaları ezgi gibi mi duyuluyor, tablet hoparlöründe tiz sesler
  (yıldız) cırlıyor mu
- Yolu Bul'da Meşe toleransı (0,055) parmak ucuyla tutturulabiliyor mu —
  bunların en riskli olanı bu
- Sepeti Tut'ta düşme hızı Fidan bandında adil mi, sepet parmağı
  gecikmeden takip ediyor mu
- Sırayı Tekrarla'da 750 ms gösterim hızı Filiz bandında takip edilebiliyor mu
- Yapboz'da Meşe'nin on altı parçası bir turu fazla uzatıyor mu
- İkon başlatıcıda nasıl duruyor (uyarlanabilir maske yüzü kırpıyor mu)

Sonrası 4. bölümdeki öncelik sırası.

## 2. Depo düzeni

- **src/Ploofy.Engine** — oyun mantığı, UI'ya sıfır bağımlılık (net10.0)
- **src/Ploofy.Data** — SQLite ilerleme deposu (net10.0)
- **src/Ploofy.Ui** — ortak MAUI arayüz katmanı: tema, çizim, ses/haptik, ebeveyn kilidi
- **src/Ploofy.App** — MAUI uygulaması (Android + iOS; Windows sadece geliştirme için)
- **tests/Ploofy.Engine.Tests** — xUnit, motor + depo
- **content/strings.tsv** — üç dilin metinleri, tek kaynak
- **tools/build_strings.py** — strings.tsv'den resx üretir
- **tools/build_sounds.py** — geri bildirim seslerini sentezler

## 3. Şu an çalışan

- Profil akışı: oluşturma, seçme, düzenleme, silme; ücretsiz katmanda tek
  profil sınırı. Ad, avatar ve yaş bandı sonradan değiştirilebiliyor
- Ebeveyn kilidi: iki basamaklı aritmetik, yanlış cevapta soru değişiyor, beş dakika açık kalıyor
- Üç dil (tr/en/de), ayarlardan çalışırken değiştirilebiliyor
- Ekran yatayda kilitli; ana ekran sütun sayısını genişliğe göre seçiyor
- Üç yaş bandı (Filiz/Fidan/Meşe) her oyunun parametrelerini gerçekten ölçekliyor
- Eşleştirme Kartları: kart çevirme animasyonu, eşleşme zıplaması, sıralı oyun
- Balon Patlatma: SkiaSharp yüzeyi, parlayan balonlar, patlama parçacıkları, hedef renk, süre
- Şekil Ayırma: parmağı kare kare takip eden sürükleme, hayalet kutular, yanlış kutuda silkelenme
- Harf/Sayı Avı: dile göre alfabe (tr/en/de), Meşe bandında küçük harfler ve benzer çeldiriciler
- Say ve Eşleştir: nesne kümesi sürüklenip rakama bırakılıyor; miktar aralığı,
  çeldirici uzaklığı ve nesne dizilişi banda göre değişiyor
- Sırayı Tekrarla: ekran diziyi oynatıyor, çocuk tekrarlıyor; tuşlar renk,
  şekil ve kendi notasını taşıyor, dizi her seviyede bir uzuyor
- Sepeti Tut: düşen nesneler, ekranın her yerinden sürüklenen sepet; sepet
  darlığı, düşme hızı ve Meşe'de savrulma banda göre değişiyor
- Yolu Bul: parmakla yol takibi; şerit kalınlığı, biçim havuzu ve kıvrım
  sayısı banda göre değişiyor
- Yapboz: tohumdan üretilen resim, geçmeli tırnaklı kesim; hayalet ve parça
  sırası banda göre değişiyor
- Avatarlar: 32 emoji, üç tematik grup, her yerde renkli rozet olarak
- Sıralı oyun (pass-and-play): devir katmanı, her çocuk kendi bandında
- Sonuç ekranı: kupa animasyonu, yıldızlar, konfeti
- Yıldız ve rozet kaydı; bant değişince eski yıldızlar korunuyor
- Ses ve titreşim: yedi geri bildirim sesi, altı tuş notası; ayarlardan
  ikisi de kapatılabiliyor
- Uygulama ikonu ve açılış ekranı: gülen mavi kabarcık, sarı zemin
- Abonelik akışı: paywall → ebeveyn kilidi → kilitlerin açılması (mağaza bağlantısı hariç)

## 4. Sıradaki işler

Oyun kütüphanesi ve varlıklar bitti; kalan her iş bu bilgisayarın dışında
bir şey istiyor.

**Öncelik 1 — Fiziksel cihaz testi.** Artık en acil olan bu ve iki başlığı
var: parmak (isabet, gerçek kare hızı) ve hoparlör (seslerin dengesi).
Bakılacaklar 1. bölümün sonunda.

**Öncelik 2 — Abonelik.** `LocalSubscriptionService` şu an satın almayı
başarılı sayıyor. Gerçek mağaza bağlantısı (Plugin.InAppBilling) aynı
`ISubscriptionService` arayüzünün arkasına takılacak; ekranlar
değişmeyecek. Play Console'da ürün tanımı ve test hesabı gerekiyor —
onlar olmadan yazılacak kod denenemez, o yüzden mağaza hesabı ilk adım.

**Öncelik 3 — Yayın anahtarını üret.** Yapılandırma hazır (1. bölüm);
eksik olan tek şey anahtarın kendisi. Komut README'de, "Yayın imzası"
başlığı. Anahtarı kaybetmek uygulamayı kaybetmek demek: aynı anahtarla
imzalanmayan bir güncelleme Play'e yüklenemiyor. Play App Signing
açılırsa yükleme anahtarı yenilenebilir kalıyor — çocuk uygulamasında
uzun ömür beklendiği için açılması mantıklı.

**Öncelik 4 — Gizlilik politikasını yayımla.** Sayfa hazır ve ayrı
deposunda duruyor (`../ploofy-privacy`, ilk commit atılmış). Kalanlar:
GitHub'da depoyu açıp uzak adresi bağlamak, Pages'i açmak, on iki alanı
doldurup "Taslak" rozetini kaldırmak, sonra adresi hem buradaki README'ye
hem Play Console'a yazmak.

İki uyarı: 4. bölüm (abonelik) gerçek billing bağlanmadan doğru değil,
çünkü satın almanın Play üzerinden yürüdüğünü anlatıyor. Ve metnin bir
hukukçuya okutulması yerinde olur — iddialar koda dayanıyor ama yasal
biçim ayrı bir iş.

**Öncelik 5 — iOS.** Hiç denenmedi. Mac gerektiriyor.

**Öncelik 6 — Mağaza vitrini.** Play Console yaş beyanı, App Store Kids
kategorisi, Data safety formu, içerik derecelendirme anketi, ekran
görüntüleri ve üç dilde tanıtım metinleri. İkon ve açılış ekranı tamam.
Sürüm numarası da ilk yayından önce `1.0` olmalı; şu an `0.1` / kod `1`.

## 5. Bilinen eksikler

- Gerçek satın alma yok; abonelik cihazda sahte olarak açılıyor
- Yayın anahtarı henüz üretilmedi; imzalama yapılandırması hazır ama
  anahtar yokken paket hata ayıklama sertifikasıyla çıkıyor
- Gizlilik politikası sayfası hazır ama yayımlanmadı: depo yerelde, uzak
  adresi yok, on iki alan hâlâ boş ve "Taslak" rozeti duruyor
- Mağaza vitrini (görseller, tanıtım metinleri, formlar) hiç başlamadı
- Pakette yalnızca `arm64-v8a` ve `x86_64` var. Gerçek tabletlerin hepsi
  arm64 olduğu için sorun değil, ama manifest `minSdk 21` diyor ve o
  çağın 32 bit ARM cihazları bu paketi kuramaz — beyan olduğundan geniş
- Yerel ağ eşleşmesi ve aile bağlantısı yok (arayüzde "yakında" olarak duruyor)
- iOS derlenmedi
- Öğretici oyunlarda sesli yönerge yok; şu an yönerge tamamen görsel (avda
  aranan işaret büyük gösteriliyor, Say ve Eşleştir'de küme ve rakamlar aynı
  ekranda duruyor). Bunun için sentez yetmiyor: üç dilde insan kaydı gerekiyor.
  Üçü de sessiz hâliyle tam çalışıyor
- Gerçek tablette hiç denenmedi; şimdiye kadar yalnızca emülatör. Parmak
  isabeti emülatörde ölçülemiyor (özellikle Yolu Bul'un Meşe toleransı)
- Uygulama yalnızca **yatay** çalışıyor. Dikey desteklenmiyor ve
  desteklenecekse her oyun için ikinci bir yerleşim gerekiyor
- Masal grubundaki bazı emojiler (peri, büyücü, deniz kızı, süper kahraman)
  Unicode 11 ile geldi; çok eski Android sürümlerinde boş kutu görünebilir.
  Uygulamanın alt sınırı API 21 ama depoda zaten 🦕 vardı ve tablette
  sorunsuz çıkıyordu. Gerçek cihaz testinde bakılacak
- Sesler hiçbir hoparlörde duyulmadı. Sayısal olarak doğrulandılar ve
  emülatörde çalındıkları günlükten görüldü, ama seviyelerinin birbirine
  göre dengesi ancak kulakla anlaşılır

## 6. Yeni makinede kurulum

1. **.NET 10 SDK** (10.0.400 ile kuruldu) — API 36 hedefi .NET 9'da yok
2. `dotnet workload install maui` — **yönetici** terminal gerektiriyor
3. Android SDK ve **JDK 17** (daha yeni JDK kabul edilmiyor)
4. Kök dizine `Ploofy.local.props` oluştur (depoya girmiyor):
   AndroidSdkDirectory ve JavaSdkDirectory özellikleri
5. `dotnet restore && dotnet test`

Hızlı deneme (Windows): `dotnet build src/Ploofy.App/Ploofy.App.csproj -f net10.0-windows10.0.19041.0`

## 7. Tekrar tuzağa düşmemek için

Bunların hepsi bir kez derlemeyi ya da uygulamayı kırdı; çözümleri koddan
okunmuyor:

- **`appCategory="game"` manifestten çıkmamalı.** Bir sınıflandırma etiketi
  gibi duruyor ama işlevi başka: API 36'yı hedefleyen uygulamalarda geniş
  ekranlarda `screenOrientation` yok sayılıyor, oyunlar bundan muaf ve
  muafiyet bu bayrakla belirleniyor. Silinirse yatay kilit tablette düşer.
- **Yerel kütüphane hangi paketten geldiğini söylemez; md5 söyler.** Bir
  NuGet paketinin Android yapısı yoksa RID grafiği `linux-x64` kopyasını
  `android-x64` için uygun sayabiliyor ve APK'ya masaüstü kütüphanesi
  giriyor. Derleme bunu `XA4301` ("zaten içeriyor, yok sayılıyor") diye
  geçiştiriyor. Şüphe varsa APK'daki `.so`'yu paketteki kopyalarla md5
  karşılaştır; hizalamayı da ELF program başlığındaki `p_align` söylüyor
  (16384 olmalı).
- **Uyarılar bütün projelerde hata sayılıyor.** `Directory.Build.props`
  bunu MAUI projelerinde kapatmayı amaçlıyor ama koşulu çalışmıyor
  (`$(UseMaui)` dosya okunurken henüz boş). Yani MAUI tarafında da her
  uyarı derlemeyi kırar; bunu bilmeden bir bağımlılık yükseltmek şaşırtır.
- **Partial property zorunlu.** MVVM üreticisi WinUI hedefinde alan biçimini
  MVVMTK0045 ile reddediyor, partial property'yi de yalnızca
  `LangVersion=preview` ile üretiyor (ayar Ploofy.App.csproj içinde).
  Partial property'ye başlangıç değeri verilemiyor — varsayılanlar kurucuya
  ya da Load metoduna gidiyor.
- **XAML işaretleme uzantıları** `[AcceptEmptyServiceProvider]` istiyor,
  yoksa XC0103 ile derleme kırılıyor.
- **Kaynak sözlükleri kendi başına çözülebilir olmalı.** Android'de yükleme
  sırası Windows'takinden farklı; PloofyStyles renkleri kendi içinde
  birleştirmezse uygulama açılışta çöküyor.
- **Android izinleri manifestte bildirilmeli.** VIBRATE eksikken MAUI
  haptik çağrısında PermissionException fırlatıp uygulamayı kapatıyordu.
- **Windows hedefi yeterli kanıt değil.** Yukarıdaki son iki madde Windows'ta
  sessizce geçiyordu. Değişiklik sonrası Android'de de çalıştır.
- **Canvas'a yazı SKFont ile çiziliyor.** SkiaSharp 3'te `SKPaint.TextSize`
  ve paint üzerinden yazı çizme kalktı; doğru çağrı
  `canvas.DrawText(metin, x, y, SKTextAlign.Center, font, paint)` ve font
  ayrı bir `SKFont` nesnesi (atılması gerekiyor). Dikey ortalama da elde
  yapılıyor: taban çizgisine göre çizmek 1 ile 8'i farklı yüksekliklere
  düşürüyor, `font.Metrics` ile ortalanmalı. Örnek: `CountMatchSurface`.
- **Gözle doğrulanamayan geometri sayıyla doğrulanır.** Yapbozun tırnak
  kesimi doğru mu, ancak ekrana bakarak anlaşılırdı — ve ekran yoktu.
  Yerine matematiğin kopyası küçük bir konsol programına alınıp üç şey
  sınandı: tırnak doğru yöne taşıyor mu, komşu iki kenar birebir örtüşüyor
  mu, ve dairenin büyük yayı mı seçiliyor. Üçü de tek satırlık bir işaret
  hatasıyla bozulabilecek şeylerdi. Aynı yol, çizim koduna dokunan her
  değişiklikte işe yarar.
- **Seçili öğe referansla bulunuyor.** `CollectionView.SelectedItem`'a
  listedekinin eşdeğerini vermek seçimi ekranda göstermiyor; listedeki
  örneğin kendisi verilmeli. Profil ekranında varsayılan yaş bandı bu
  yüzden seçilmemiş görünüyordu.
- **Düğmedeki emoji VS16 ister.** `✏` (U+270F) tek başına metin biçiminde
  çiziliyor: soluk, ince, düğme boş görünüyor. `&#x270F;&#xFE0F;` renkli
  emoji veriyor. Depodaki 🗑 gibi zaten emoji varsayılanı olan işaretler
  etkilenmiyor; ayrım işaretin Unicode'daki varsayılan gösterimi.
- **İkon SVG'lerinde degrade, süzgeç ve yazı kullanma.** Resizetizer bu
  dosyaları kendi rasterleştiricisiyle çeviriyor ve sade olmayan her
  özellik sürprize açık. Hacim için üst üste iki düz dolgu yeter. Ayrıca
  saydam renk altındakiyle karışıyor: mavinin üstündeki saydam pembe mor
  çıktı.
- **Git Bash adb'nin cihaz yollarını bozuyor.** `adb push x /data/local/tmp/x`
  çağrısında Git Bash `/data/...` yolunu Windows yoluna çeviriyor ve
  "remote secure_mkdirs() failed" hatası geliyor. Başına
  `MSYS_NO_PATHCONV=1` koymak yetiyor. `adb shell input tap` gibi yol
  içermeyen çağrılar etkilenmiyor.
- **Emülatörde `adb shell input motionevent` ile eğri çizilebiliyor.**
  `input swipe` yalnızca düz çizgi; eğri bir yolu takip etmek için
  DOWN/MOVE/UP dizisini bir betiğe yazıp cihaza gönderip `sh` ile çalıştırmak
  gerekiyor. Yolu Bul'un tur tamamlaması böyle doğrulandı.
- **Emülatörün SystemUI'ı bu makinede takılıyor.** Takıldığında bütün ekran
  donuyor: ekran görüntüsü hep aynı kareyi gösteriyor, dokunuşlar işlemiyor ve
  uygulama kilitlenmiş gibi duruyor. Teşhis için
  `adb shell "dumpsys window | grep mCurrentFocus"` — "Application Not
  Responding: com.android.systemui" görünüyorsa sorun bizde değil, emülatörü
  yeniden başlat. Bir kez var olmayan bir kilitlenmeyi kovalamaya yol açtı.

## 8. Yeni mini oyun ekleme

1. `Engine/Catalog/GameCatalog.cs` içine bir satır: id, etkileşim türü,
   katman, en küçük bant, çizim tekniği
2. Kuralları `Engine/Games/` altında arayüzden bağımsız bir sınıf olarak yaz;
   zorluk knob'larını `BandValue<T>` ile tanımla ve testini yaz
3. `GamePresentation` içine ad anahtarı, simge ve rota
4. `content/strings.tsv` içine üç dilde ad, sonra `tools/build_strings.py`
5. `AppShell.xaml.cs` içinde rotayı kaydet, `MauiProgram` içinde sayfayı ve
   görünüm modelini DI'ya ekle
6. Sürekli hareketli ya da sürüklemeli bir oyunsa çizimi `Ploofy.Ui/Controls`
   altında SKCanvasView olarak yaz; `BubblePainter`, `ShapePainter`,
   `ParticleField`, `PloofyPalette` hazır. Sürükleme için MAUI'nin
   sürükle-bırak tanıyıcıları değil doğrudan dokunma olayları kullanılıyor:
   platformun sürükleme eşiği küçük çocuğun yavaş hareketinde aşılmıyor ve
   parça hiç kıpırdamıyor
7. Oyun kendi başına bir şey oynatıyorsa (Sırayı Tekrarla gibi) zamanlama
   görünüm modelinde durur, motorda değil — motor "nerede kalındı"yı bilir,
   "ne zaman"ı bilmez. Bu durumda **her bekleme iptal edilebilir olmalı**:
   görünüm modelinde bir ömür `CancellationTokenSource` tut, `Dispose` ve
   sıra devrinde iptal et, bütün `Task.Delay` çağrılarına token'ı geçir.
   Yalnızca gösterimi iptal etmek yetmiyor — aradaki beklemeler sayfa
   kapandıktan sonra da doluyor ve kapanmış ekranda yeni bir gösterim
   başlatıyor. Örnek: `SimonViewModel`

Kilit, bant filtresi, yıldız kaydı ve ebeveyn ekranı kendiliğinden çalışır.
