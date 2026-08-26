# Ploofy — İlerleme Notu

Son güncelleme: 26.08.2026
Depo: https://github.com/alikiratli/Ploofy (public)

## 1. Nerede kaldık

Faz 1 tamamlandı, Faz 2'nin yalnızca canvas gerektiren üç oyunu kaldı.
Uygulama Android tablette uçtan uca çalışıyor: çocuk profili oluşturuluyor,
oyun seçiliyor, oynanıyor, yıldız kazanılıyor ve kayıt cihazda tutuluyor.
Yedi mini oyun oynanabilir durumda, kalan üçü katalogda "yakında" olarak
görünüyor. Kütüphane artık beş etkileşim türünün beşini de kapsıyor.

**Son oturumda yapılan — iki oyun:**

*Say ve Eşleştir* — üç öğretici oyunun sonuncusu, yani öğretici bölüm tam.
Ekranda bir nesne kümesi duruyor, çocuk kümeyi doğru rakamın üstüne
sürüklüyor. Sayı Avı "rakamı tanıma"yı öğretiyordu; bu oyun rakamı bir
**miktara** bağlıyor. Banda göre üç knob: miktar aralığı (Filiz 1-3, Fidan
1-5, Meşe 1-10), çeldirici rakamların uzaklığı (Meşe'de komşu sayılar — 6 ile
7 arasından seçmek gerçekten saymayı gerektiriyor) ve nesnelerin dizilişi
(Meşe'de dağınık, altında beşerli sıra). Beşerli diziliş bilinçli: 7 nesne
"bir tam sıra ve iki tane" olarak görünüyor.

*Sırayı Tekrarla* — kütüphanenin ilk **Sequence** oyunu ve kalıbı ters
çeviren ilk oyun: ekran diziyi kendi oynatıyor, o sırada dokunuş kapalı,
sonra sıra çocuğa geçiyor. Dizi her seviyede uzuyor, yeniden üretilmiyor —
eski dizi yeninin başlangıcı olarak kalıyor. Tuşlar renk **ve** şekil
taşıyor (Şekil Ayırma'nın sözlüğü): renk körü çocuk için dizi yalnızca
renkle anlatılamaz, ve iki kanal diziyi daha akılda kalıcı yapıyor.
Banda göre: tuş sayısı (3/4/6), son dizi uzunluğu (4/6/8), gösterim hızı
(750/600/450 ms) ve aynı tuşun peş peşe gelip gelemeyeceği (yalnızca Meşe).

**Sayılar:** 167 test geçiyor · 10 oyun tanımlı, 7'si oynanabilir
(Eşleştirme Kartları, Balon Patlatma, Şekil Ayırma, Harf Avı, Sayı Avı,
Say ve Eşleştir, Sırayı Tekrarla).

**Buradan başla:** Sepeti Tut. Kalan üç oyunun hepsi canvas; bu en
kolayları, çünkü sürekli hareket zaten `BubbleSurface`'da çözülmüş durumda
(ticker, delta zaman, parçacıklar). Yeni olan tek şey düşen nesne ile sepet
arasındaki çarpışma kontrolü. Sonra Yolu Bul (serbest çizgi takibi), en son
Yapboz (parça kesme). Adımlar için 8. bölüme bak.

**Önce yapılması iyi olur:** İki yeni oyun da yalnızca derlendi (Windows +
Android), gerçek ekranda oynanmadı. Bakılacaklar: Say ve Eşleştir'de kart
boyutu ve rakam tepsilerinin parmak isabeti; Sırayı Tekrarla'da gösterim
hızının Filiz bandında gerçekten takip edilebilir olup olmadığı — 750 ms
masabaşında doğru göründü ama bunu ancak çocuk söyler.

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
- Üç yaş bandı (Filiz/Fidan/Meşe) her oyunun parametrelerini gerçekten ölçekliyor
- Eşleştirme Kartları: kart çevirme animasyonu, eşleşme zıplaması, sıralı oyun
- Balon Patlatma: SkiaSharp yüzeyi, parlayan balonlar, patlama parçacıkları, hedef renk, süre
- Şekil Ayırma: parmağı kare kare takip eden sürükleme, hayalet kutular, yanlış kutuda silkelenme
- Harf/Sayı Avı: dile göre alfabe (tr/en/de), Meşe bandında küçük harfler ve benzer çeldiriciler
- Say ve Eşleştir: nesne kümesi sürüklenip rakama bırakılıyor; miktar aralığı,
  çeldirici uzaklığı ve nesne dizilişi banda göre değişiyor
- Sırayı Tekrarla: ekran diziyi oynatıyor, çocuk tekrarlıyor; tuşlar renk ve
  şekil taşıyor, dizi her seviyede bir uzuyor
- Sıralı oyun (pass-and-play): devir katmanı, her çocuk kendi bandında
- Sonuç ekranı: kupa animasyonu, yıldızlar, konfeti
- Yıldız ve rozet kaydı; bant değişince eski yıldızlar korunuyor
- Abonelik akışı: paywall → ebeveyn kilidi → kilitlerin açılması (mağaza bağlantısı hariç)

## 4. Sıradaki işler

**Öncelik 1 — Ses varlıkları.** Tek eksik olan somut parça.
`src/Ploofy.App/Resources/Raw/sounds/` altına yedi dosya: tap.mp3,
correct.mp3, retry.mp3, round_complete.mp3, star.mp3, handoff.mp3,
locked.mp3. Kod hazır; dosya yoksa sessizce atlıyor, koyulduğu anda çalışır.

**Öncelik 2 — Fiziksel cihaz testi.** Şimdiye kadar yalnızca tablet
emülatöründe koştu. Parmak isabeti (özellikle Filiz bandındaki balon
boyutu) ve gerçek kare hızı ancak cihazda ölçülebilir. Tableti USB'den
tak, hata ayıklamayı aç, `dotnet build src/Ploofy.App/Ploofy.App.csproj
-f net9.0-android -t:Run`.

**Öncelik 3 — Faz 2, kalan üç oyun.** Hepsi canvas. Önerilen sıra, her adımda
yeni bir teknik parça açtığı için:

1. Sepeti Tut (sürekli hareket + çarpışma; `BubbleSurface`'ın ticker ve delta
   zaman kalıbı hazır, yeni olan tek şey çarpışma kontrolü)
2. Yolu Bul (serbest çizgi takibi — parmağın yoldan çıkıp çıkmadığı)
3. Yapboz (parça kesme; en zoru, sona bırakıldı)

**Öncelik 4 — Abonelik.** `LocalSubscriptionService` şu an satın almayı
başarılı sayıyor. Gerçek mağaza bağlantısı (Plugin.InAppBilling) aynı
`ISubscriptionService` arayüzünün arkasına takılacak; ekranlar değişmeyecek.

**Öncelik 5 — iOS.** Hiç denenmedi. Mac gerektiriyor.

**Öncelik 6 — Yayın hazırlığı.** Play Console yaş beyanı, App Store Kids
kategorisi, gizlilik formu, mağaza görselleri, ikon ve açılış ekranı
(şu an MAUI şablonunun varsayılanı duruyor).

## 5. Bilinen eksikler

- Ses dosyaları yok
- Gerçek satın alma yok; abonelik cihazda sahte olarak açılıyor
- Yerel ağ eşleşmesi ve aile bağlantısı yok (arayüzde "yakında" olarak duruyor)
- iOS derlenmedi
- Uygulama ikonu ve açılış ekranı hâlâ şablon varsayılanı
- Profil düzenleme yok (yalnızca oluştur ve sil)
- Öğretici oyunlarda sesli yönerge yok; şu an yönerge tamamen görsel (avda
  aranan işaret büyük gösteriliyor, Say ve Eşleştir'de küme ve rakamlar aynı
  ekranda duruyor). Ses varlıkları gelince seslendirme eklenebilir ama üçü de
  sessiz hâliyle tam çalışıyor
- Say ve Eşleştir ile Sırayı Tekrarla gerçek ekranda oynanmadı; yalnızca
  Windows ve Android'de derlendi. Bakılacaklar: kart boyutu ve rakam
  tepsilerinin parmak isabeti; gösterim hızının Filiz bandında takip
  edilebilir olup olmadığı
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
