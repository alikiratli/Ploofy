# Ploofy — İlerleme Notu

Son güncelleme: 27.08.2026
Depo: https://github.com/alikiratli/Ploofy (public)

## 1. Nerede kaldık

**Faz 2 bitti: on oyunun onu da oynanabilir.** Uygulama Android tablette
uçtan uca çalışıyor — çocuk profili oluşturuluyor, oyun seçiliyor,
oynanıyor, yıldız kazanılıyor ve kayıt cihazda tutuluyor. Katalogda artık
"yakında" olarak duran hiçbir oyun yok ve kütüphane beş etkileşim türünün
beşini de kapsıyor.

**Faz 4 başladı: uygulamanın sesi ve yüzü var.** Bu oturumda kalan somut
varlıklar üretildi; geriye kalan işler artık bilgisayarda yapılamayanlar —
gerçek cihaz, gerçek mağaza, Mac.

**Son oturumda yapılan:** Sesler, Sırayı Tekrarla'nın nota tuşları, profil
düzenleme, uygulama ikonu ve açılış ekranı, bir de İngilizce/Almanca'daki
tekil-çoğul hatası. Sırayla:

### Sesler

Yedi geri bildirim sesi ve Sırayı Tekrarla'nın altı tuş notası artık depoda.
Hazır bir ses bankasından alınmadılar, **üretiliyorlar**:
`tools/build_sounds.py` her sesi harmoniklerin toplamı olarak sentezliyor ve
`Resources/Raw/sounds/` altına yazıyor. Sebebi lisans — hazır bankalar
çocuk uygulamasında atıf ve ticari kullanım şartı getiriyor, üretilmiş bir
dalga getirmiyor. Uygulama zaten hiçbir görsel varlık taşımıyordu; ses de
aynı yolu izliyor.

Biçim WAV, MP3 değil: kodlayıcı gerektirmiyor ve hepsi bir saniyenin altında,
toplamı 764 KB. Bu boyda sıkıştırmanın kazandıracağı yer, çözücünün ilk
çalmada getirdiği gecikmeye değmiyor.

İki karar tınıdan daha çok işe yaradı:

- **Hepsi do majör pentatonik içinde.** Çocuk oyununda sesler sürekli
  çakışıyor (yıldız + tur sonu, dokunuş + doğru) ve bu dizide hangi ikisi
  üst üste binerse binsin uyumsuz bir aralık çıkmıyor.
- **Kuyruklar kısa** (-28 dB'de kesiliyor). Uzun kuyruk bu oyunda zarar
  veriyordu: art arda düşen iki yıldız sesinden ikincisi birincisini
  kesiyor ve kesilen ses tıkırdıyor.

Sesi kulakla doğrulamak mümkün değildi (kulak yok), o yüzden yapbozun
tırnak geometrisinde izlenen yol tekrarlandı ve dosyalar **sayıyla**
sınandı: kırpılma yok, ilk ve son örnek sıfırda (kenarda tık yok) ve altı
tuşun altısında beklenen temel frekans komşu yarım tonlardan en az dört kat
güçlü. Gerçek dinleme testi cihazda.

### Sırayı Tekrarla artık çalıyor

Bilinen eksikler listesindeki en eski madde kapandı: her tuşun kendi notası
var. Klasik oyunun asıl işi burada — dizi kulakla da hatırlanıyor ve
gösterim bir ezgiye dönüşüyor. Özellikle Filiz bandında değerli: henüz
"üçüncü sıradaki" diye düşünemeyen çocuk üç notalık bir ezgiyi
tekrarlayabiliyor. Çocuk bir tuşa dokunduğunda gösterimde duyduğu notanın
aynısını duyuyor, dolayısıyla çaldığını duyduğuyla karşılaştırabiliyor.

### Profil düzenleme

Avatar, ad ve yaş bandı artık profil kurulduktan sonra da değiştirilebiliyor.
Ayarlardaki profil satırına bir kalem düğmesi geldi; aynı ekran hem ekleme
hem düzenleme yapıyor (`profileeditor?profileId=3`). Ayrı bir düzenleme
ekranı yazmak otuz iki avatarlık ızgarayı iki yere kopyalamak olurdu.

Yaş bandının da düzenlenebilir olması önemli: çocuk büyüyor ve tek yol
profili silmek olsaydı bütün yıldızları giderdi. Bant değişince eski
yıldızlar duruyor — ilerleme oyun **ve** bant başına tutuluyor.

Bu iş sırasında küçük bir kusur da çıktı: yeni profil ekranında varsayılan
yaş bandı seçili görünmüyordu. `SelectedBand` listedeki örneğe değil onun
eşdeğerine kuruluyordu; CollectionView seçili öğeyi referansla arıyor.

### İkon ve açılış ekranı

MAUI şablonunun mor ".NET" logosu gitti. Yerine Balon Patlatma'nın mavi
kabarcığından gelen gülen bir yüz: sarı zemin (paletteki Sunny), mavi gövde,
pembe yanaklar. Açılış ekranı aynı yüzün büyüğü.

SVG'lerde degrade, süzgeç ve yazı yok — yalnızca düz dolgu ve daireler.
Hacim, üst üste iki daireyle veriliyor (koyu olan altta bir milim taşıyor);
oyun yüzeylerindeki yol da bu. Yazı olmamasının ayrı bir sebebi var: üç
dilde açılan bir uygulamanın açılış ekranında tek bir dilin sözcüğü yanlış
duruyor. Ön katman merkezden 126 birim yarıçapın içinde kalıyor, çünkü
Android'in uyarlanabilir ikonu dış üçte biri maskeyle kırpıyor.

Üretilen PNG'ler gözle doğrulandı. İlk denemede yanaklar mor çıkmıştı:
saydam pembe mavinin üstünde mora kayıyor.

### Emülatörde doğrulama

Beşi de tablet emülatöründe çalıştırıldı. Profil düzenleme uçtan uca
denendi: ayarlardan kalem düğmesi, ekranın "Çocuğu düzenle" başlığıyla ve
dolu alanlarla açılması, avatarın tilkiden pandaya değişmesi, kaydetme ve
değişikliğin hem ayarlarda hem profil seçme ekranında görünmesi. Yaş bandı
da doğru seçili geliyor.

Ses gerçekten çalıyor: kart dokunuşunda sistem günlüğünde `audio/raw`
çözücüsü açılıyor ve `AudioTrack` 10 054 kare teslim ediyor — tap.wav'ın
uzunluğu (0,23 sn × 44 100) tam olarak bu. Duyulan sesin kendisi hâlâ
denenmedi, yalnızca çalındığı.

Bir kusur çıktı ve düzeltildi: kalem düğmesi boş görünüyordu. Çıplak
U+270F cihazda soluk ince bir çizgi olarak çiziliyor; çöp kutusunun yanında
düğme boşmuş gibi duruyordu. VS16 eklenince (`&#x270F;&#xFE0F;`) renkli
emoji olarak çıkıyor.

Uygulama ikonu başlatıcıda yuvarlak maskeyle doğru duruyor, yüz
kırpılmıyor. Masal grubundaki emojiler (peri, büyücü, deniz kızı, süper
kahraman) API 36 emülatöründe eksiksiz çıkıyor.

### "1 stars in total"

`LocalizationService.Format` artık tek argüman 1 olduğunda anahtarın
`.One` ekli satırını arıyor. Üç dilin üçünde de yalnızca "bir" ayrı
davranıyor, o yüzden tam bir çoğul kuralı motoru yazılmadı; Lehçe gibi
birden çok çoğul biçimi olan bir dil eklenirse orası genişler.

**Sayılar:** 229 test geçiyor · 10 oyun tanımlı, 10'u oynanabilir ·
13 ses dosyası.

**Buradan başla: gerçek tablet.** Emülatör her şeyi gösterdi ama iki şeyi
ölçemiyor — parmağı ve hoparlörü. Tableti USB'den tak, hata ayıklamayı aç,
`dotnet build src/Ploofy.App/Ploofy.App.csproj -f net9.0-android -t:Run`.
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

- **src/Ploofy.Engine** — oyun mantığı, UI'ya sıfır bağımlılık (net9.0)
- **src/Ploofy.Data** — SQLite ilerleme deposu (net9.0)
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

**Öncelik 3 — iOS.** Hiç denenmedi. Mac gerektiriyor.

**Öncelik 4 — Yayın hazırlığı.** Play Console yaş beyanı, App Store Kids
kategorisi, gizlilik formu, mağaza görselleri ve tanıtım metinleri.
İkon ve açılış ekranı tamam.

## 5. Bilinen eksikler

- Gerçek satın alma yok; abonelik cihazda sahte olarak açılıyor
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

1. `dotnet workload install maui` — **yönetici** terminal gerektiriyor
2. Android SDK ve **JDK 17** (daha yeni JDK kabul edilmiyor)
3. Kök dizine `Ploofy.local.props` oluştur (depoya girmiyor):
   AndroidSdkDirectory ve JavaSdkDirectory özellikleri
4. `dotnet restore && dotnet test`

Hızlı deneme (Windows): `dotnet build src/Ploofy.App/Ploofy.App.csproj -f net9.0-windows10.0.19041.0`

## 7. Tekrar tuzağa düşmemek için

Bunların hepsi bir kez derlemeyi ya da uygulamayı kırdı; çözümleri koddan
okunmuyor:

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
