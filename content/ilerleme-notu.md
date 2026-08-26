# Ploofy — İlerleme Notu

Son güncelleme: 26.08.2026
Depo: https://github.com/alikiratli/Ploofy (public)

## 1. Nerede kaldık

**Faz 2 bitti: on oyunun onu da oynanabilir.** Uygulama Android tablette
uçtan uca çalışıyor — çocuk profili oluşturuluyor, oyun seçiliyor,
oynanıyor, yıldız kazanılıyor ve kayıt cihazda tutuluyor. Katalogda artık
"yakında" olarak duran hiçbir oyun yok ve kütüphane beş etkileşim türünün
beşini de kapsıyor.

**Bundan sonrası oyun eklemek değil, olanı yayına hazırlamak.** Kalan üç
şey — ses varlıkları, gerçek satın alma ve mağaza hazırlığı — 4. bölümde.

**Son oturumda yapılan — Yapboz:**

Tahtada boş yuvalar duruyor, altta sıradaki parça bekliyor; çocuk parçayı
yuvasına sürüklüyor. Parçalar Şekil Ayırma'daki gibi **sırayla** geliyor:
on altı parçayı aynı anda ekrana dökmek bu yaş grubunda dağıtıyor.

Bandın asıl farkı **hayalet**: küçük bantlarda boş yuvaların altında resmin
soluk bir kopyası duruyor ve oyun "resmi eşleştir" oluyor. Meşe'de hayalet
yok, oyun "resmi kur"a dönüşüyor — parçanın yeri ancak yerleşmiş komşulara
bakarak çıkarılıyor. Bu yüzden o bantta parçaların geliş sırası rastgele
**olamıyor**: sıra bir köşeden başlıyor ve her parça yerleşmişlerden en az
birine komşu geliyor. Olmasaydı ortadan gelen yalnız bir parçanın yerini
çıkarmanın yolu olmazdı.

Resim bir varlık dosyası değil, tohumdan üretiliyor: uygulama hiç görsel
varlık taşımıyor. Şekiller sarsılmış bir ızgaraya dağıtılıyor, rastgele
serpilmiyor — serpme boş bölgeler bırakıyor ve düz zeminden ibaret kalan
bir parçanın yeri hayaletsiz bantta bulunamıyor.

Kesim: her iç kenara bir tırnak, komşu kenar onun tersi. Tırnak, kenarın
ortasına oturan bir dairenin **büyük** yayı; daire kenarı iki noktada
kestiği için yay tam o boyunlardan başlayıp bitiyor ve ek birleştirme
çizgisi gerekmiyor. Geometri ekranda gözle doğrulanamadığı için ayrıca
sayısal olarak sınandı: tırnaklar doğru yöne taşıyor, komşu kenarlar
birebir örtüşüyor ve küçük yay değil büyük yay seçiliyor.

Yapboz, son üç oyunun aksine **hedef süre taşıyor** (Meşe, 150 sn): tahtanın
tamamı en baştan görünüyor, bekleyecek bir gösterim ya da düşecek bir nesne
yok, yani hızlı bitirmek gerçekten "daha çabuk çözdüm" demek.

**Sayılar:** 228 test geçiyor · 10 oyun tanımlı, 10'u oynanabilir.

**Android'de çalıştırıldı (26.08.2026).** Beş yeni oyunun beşi de tablet
emülatöründe açıldı, çizildi ve dokunuşa cevap verdi; Yolu Bul'da bir tur,
Sepeti Tut'ta bir tur ve Yapboz'da bir parça uçtan uca oynandı, yıldız
kaydı ve sonuç ekranı çalıştı. Üç kusur bulundu ve üçü de düzeltildi:

1. **Yolu Bul'da geri kayma yoldan çıkmak sayılıyordu.** Parmağın yola
   oturduğu yer yalnızca `[şu an - 2, şu an + 10]` penceresinde aranıyordu;
   iki parçadan (yolun ~%1,7'si) fazla geri kayan parmak "çıktın" alıyor ve
   Meşe'de bu bir hata puanına dönüyordu — o kadar geri kayma beş yaşındaki
   bir çocuğun titremesi kadar bir mesafe. Üstelik ilerleme o noktada
   kilitlenip kalabiliyordu. Pencere artık yalnızca ileriyi sınırlıyor;
   geriye doğru yolun tamamı açık. İleri atlamayı engelleyen kural aynen
   duruyor.
2. **Yapbozda "sıradaki parça" önizlemesi çizim hatası gibi görünüyordu.**
   Şekil Ayırma'da parçalar küçük olduğu için arkadaki soluk parça
   okunuyor; yapbozda parça tepsinin tamamını kaplıyor ve arkadaki
   yalnızca tırnağıyla farklı renkte dışarı taşıyordu. Kaldırıldı.
3. **Tepsideki parça hücreye göre ölçekleniyordu**, oysa parça
   tırnaklarıyla birlikte hücreden %30 büyük — tepsiden taşıyordu.
4. **Say ve Eşleştir'de kart sabit yükseklikteydi.** Tek sıralık bir küme
   (5 ve altı nesne) kartın ortasında asılı kalıp altında yarım ekranlık
   boşluk bırakıyordu. Kart artık sıra sayısına göre yükseliyor ve altı
   sabit bir yerde duruyor.

**Ekran yönü yatayda kilitlendi.** Emülatörde dikey çalıştığında oyunların
ortasında yarım ekranlık boşluklar kaldığı görüldü; bütün yerleşimler yatay
tablet düşünülerek ölçülmüştü. Android'de `SensorLandscape` (kilitli ama
iki yön de serbest, çocuk tableti ters çevirince görüntü dönüyor), iOS'ta
`Info.plist` yalnızca yatay. İki yönü birden desteklemek her oyun için
ikinci bir yerleşim yazmak demekti.

Kilitledikten sonra ana ekranda kartların ekran boyu şeritlere dönüştüğü
görüldü: sütun sayısı sabit ikiydi. Artık genişliğe göre — 900 birimin
altında iki, üstünde üç sütun.

Kalan sorular hâlâ **gerçek tablet** gerektiriyor; emülatörde ölçülemeyen
tek şey parmak: Meşe'de Yolu Bul'un 0,055 toleransı ve Sepeti Tut'un
düşme hızı gerçek elle denenmedi.

## 2. Depo düzeni

- **src/Ploofy.Engine** — oyun mantığı, UI'ya sıfır bağımlılık (net9.0)
- **src/Ploofy.Data** — SQLite ilerleme deposu (net9.0)
- **src/Ploofy.Ui** — ortak MAUI arayüz katmanı: tema, çizim, ses/haptik, ebeveyn kilidi
- **src/Ploofy.App** — MAUI uygulaması (Android + iOS; Windows sadece geliştirme için)
- **tests/Ploofy.Engine.Tests** — xUnit, motor + depo
- **content/strings.tsv** — üç dilin metinleri, tek kaynak
- **tools/build_strings.py** — strings.tsv'den resx üretir

## 3. Şu an çalışan

- Profil akışı: oluşturma, seçme, silme; ücretsiz katmanda tek profil sınırı
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
- Sırayı Tekrarla: ekran diziyi oynatıyor, çocuk tekrarlıyor; tuşlar renk ve
  şekil taşıyor, dizi her seviyede bir uzuyor
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
- Abonelik akışı: paywall → ebeveyn kilidi → kilitlerin açılması (mağaza bağlantısı hariç)

## 4. Sıradaki işler

Oyun kütüphanesi bitti; buradan sonrası yayın işi.

**Öncelik 1 — Fiziksel cihaz testi.** Artık en acil olan bu. Şimdiye kadar
yalnızca tablet emülatöründe koştu ve son beş oturumun oyunları hiç
oynanmadı. Parmak isabeti ve gerçek kare hızı ancak cihazda ölçülebilir.
Tableti USB'den tak, hata ayıklamayı aç,
`dotnet build src/Ploofy.App/Ploofy.App.csproj -f net9.0-android -t:Run`.
Bakılacaklar 1. bölümün sonunda.

**Öncelik 2 — Ses varlıkları.** Tek eksik olan somut parça.
`src/Ploofy.App/Resources/Raw/sounds/` altına yedi dosya: tap.mp3,
correct.mp3, retry.mp3, round_complete.mp3, star.mp3, handoff.mp3,
locked.mp3. Kod hazır; dosya yoksa sessizce atlıyor, koyulduğu anda çalışır.

**Öncelik 3 — Abonelik.** `LocalSubscriptionService` şu an satın almayı
başarılı sayıyor. Gerçek mağaza bağlantısı (Plugin.InAppBilling) aynı
`ISubscriptionService` arayüzünün arkasına takılacak; ekranlar değişmeyecek.

**Öncelik 4 — iOS.** Hiç denenmedi. Mac gerektiriyor.

**Öncelik 5 — Yayın hazırlığı.** Play Console yaş beyanı, App Store Kids
kategorisi, gizlilik formu, mağaza görselleri, ikon ve açılış ekranı
(şu an MAUI şablonunun varsayılanı duruyor).

## 5. Bilinen eksikler

- Ses dosyaları yok
- Gerçek satın alma yok; abonelik cihazda sahte olarak açılıyor
- Yerel ağ eşleşmesi ve aile bağlantısı yok (arayüzde "yakında" olarak duruyor)
- iOS derlenmedi
- Uygulama ikonu ve açılış ekranı hâlâ şablon varsayılanı
- Öğretici oyunlarda sesli yönerge yok; şu an yönerge tamamen görsel (avda
  aranan işaret büyük gösteriliyor, Say ve Eşleştir'de küme ve rakamlar aynı
  ekranda duruyor). Ses varlıkları gelince seslendirme eklenebilir ama üçü de
  sessiz hâliyle tam çalışıyor
- Gerçek tablette hiç denenmedi; şimdiye kadar yalnızca emülatör. Parmak
  isabeti emülatörde ölçülemiyor (özellikle Yolu Bul'un Meşe toleransı)
- Uygulama yalnızca **yatay** çalışıyor. Dikey desteklenmiyor ve
  desteklenecekse her oyun için ikinci bir yerleşim gerekiyor
- İngilizcede "1 stars in total" yazıyor; `TotalStars` metni tekil/çoğul
  ayrımı yapmıyor. Türkçe ve Almanca'da sorun yok
- Profil düzenleme hâlâ yok: avatar ancak profil oluştururken seçiliyor,
  sonradan değiştirilemiyor. Otuz iki seçenek varken bu daha çok göze
  batacak
- Masal grubundaki bazı emojiler (peri, büyücü, deniz kızı, süper kahraman)
  Unicode 11 ile geldi; çok eski Android sürümlerinde boş kutu görünebilir.
  Uygulamanın alt sınırı API 21 ama depoda zaten 🦕 vardı ve tablette
  sorunsuz çıkıyordu. Gerçek cihaz testinde bakılacak
- Sırayı Tekrarla'da gösterim sessiz. Klasik oyunda her tuşun kendi notası
  var ve dizi kulakla da hatırlanıyor; şu an yalnızca görsel. Ses varlıkları
  gelince tuş başına ayrı ses eklemek `FeedbackCue` sözlüğünü genişletmeyi
  gerektiriyor (şu an tek `Tap` var)

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
