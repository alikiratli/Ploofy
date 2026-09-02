# Ploofy — İlerleme Notu

Son güncelleme: 02.09.2026
Depo: https://github.com/alikiratli/Ploofy (public)
Web: https://alikiratli.github.io/ploofy-web/ (gizlilik politikası + Impressum)

## 1. Nerede kaldık

**Faz 2 bitti: on ikinin on ikisi de oynanabilir.** (Faz 2 onla kapandı;
Harf Yazma ve Örüntü sürümden sonra eklendi.) Uygulama Android tablette
uçtan uca çalışıyor — çocuk profili oluşturuluyor, oyun seçiliyor,
oynanıyor, yıldız kazanılıyor ve kayıt cihazda tutuluyor. Katalogda artık
"yakında" olarak duran hiçbir oyun yok ve kütüphane beş etkileşim türünün
beşini de kapsıyor.

**Faz 4 sürüyor: uygulama Play'in 2026 kapılarına uyduruldu.** Bu oturumda
yeni özellik yok denecek kadar az; yapılan iş uygulamayı mağazanın bugün
geçerli zorunluluklarına taşımak ve ilk kez gerçek bir **sürüm (Release)**
paketi üretip çalıştırmak oldu.

**Son oturumda yapılan: iki mağaza engeli kalktı, abonelik yönetimi yazıldı,
sonra içerik yol haritasının ilk maddesi.** Yayın anahtarı üretildi ve
gizlilik politikası yayımlandı — ikisi de kod işi değildi, ikisi de sürümü
bloke ediyordu. Ardından aboneliğin ekran tarafı tamamlandı, en son da
**Harf Yazma**, **Örüntü** ve **ebeveyn raporu** eklendi (yol haritası
maddeleri İ1, İ2, İ3). Ayrıntı aşağıda: "Yayın imzası", "Gizlilik politikası",
"Abonelik yönetimi", "Harf Yazma", "Örüntü", "Ebeveyn raporu".

**Bir önceki oturumda yapılan:** .NET 10'a geçiş, API 36 hedefi, yatay kilidin
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

**Gerçek yayın anahtarı üretildi (02.09.2026).** RSA 2048, 10000 gün
geçerli, PKCS12; sertifika `CN=Ali Kiratli, O=Ploofy, L=Ennepetal,
ST=Nordrhein-Westfalen, C=DE`. Dosya `ploofy-release.keystore` deponun
kökünde ve `.gitignore`'da, dört değer `Ploofy.local.props`'ta (o da
gitignore'da). Parola 32 karakterlik rastgele bir dize.

Doğrulandı: anahtarla üretilen Release APK'sında `apksigner verify
--print-certs` yukarıdaki DN'i basıyor ve "yayın anahtarı tanımlı değil"
uyarısı artık çıkmıyor.

**İki dosyanın da makine dışında bir yedeği olmalı.** Anahtarı kaybetmek
uygulamayı kaybetmek demek: aynı anahtarla imzalanmayan bir güncellemeyi
Play kabul etmiyor. Play App Signing açılırsa yükleme anahtarı yenilenebilir
kalıyor — çocuk uygulamasında uzun ömür beklendiği için açılması mantıklı.

### Abonelik yönetimi

Abonelik bugüne kadar tek yönlüydü: paywall satın alıyordu, ayarlar tek satır
durum gösteriyordu ve **bitirmenin yolu yoktu**. Mağazalar bunu kabul etmiyor;
ayrıca ebeveynin "ne zamana kadar açık" sorusunun da cevabı yoktu.

Motora `SubscriptionStatus.Canceled` eklendi: iptal yenilemeyi kapatır, erişim
ödenmiş dönemin sonuna kadar sürer. Bu, Play'in ve App Store'un kendi
davranışı — erişimi iptal anında kesmek ebeveynin parasını yakmak olurdu.
`Entitlements` üç yeni soruya cevap veriyor (`AutoRenews`,
`AccessEndsAfterPeriod`, `CanCancel`) ve tarih işi ayrı bir kayda alındı:
`SubscriptionInfo` (durum + dönem sonu). Ayrılığın sebebi, hiçbir erişim
kararının tarihe bakmaması — cihazın saatini ileri almak oyunları kilitlemiyor,
geri almak da abonelik uzatmıyor.

Yeni ekran `SubscriptionPage` (rota `subscription`), ayarlardan açılıyor:

- durum rozeti (Etkin / Ücretsiz katman / Bitiyor / Ödeme sorunu) ve tek
  cümlelik durum
- ödenmiş dönemin sonu ve kaç gün kaldığı; tarih yoksa uydurulmuyor, ekran
  "mağaza henüz bildirmedi" diyor (çevrimdışı ilk açılışta olabiliyor)
- abone için "neler dahil", aboneliksiz için "aboneliksiz ne kalıyor"
- mağazanın abonelik merkezine çıkış ve satın alımları geri yükleme
- **Aboneliği bitir** — en altta, ayrı kartta. Önce ebeveyn kilidi, sonra
  onay: onay ekranını çocuğa hiç göstermemek "hayır"a basmasına güvenmekten
  iyi. Metin ne olacağını açıkça yazıyor — yenileme durur, oyunlar dönem
  sonuna kadar açık kalır, yıldız/rozet/profil silinmez
- bitirildikten sonra kart yerini "yeniden başlat"a bırakıyor

`ISubscriptionService.CancelAsync` bugün durumu yerel olarak `Canceled`
yapıyor ki akış denenebilsin. Mağaza bağlandığında **iptali uygulama
yapmayacak**: Play ve App Store aboneliğin yalnızca kendi abonelik
merkezlerinden bitirilmesine izin veriyor, o gün bu çağrı `ManagementUri`
adresini açıp dönüşte durumu mağazaya soracak. Ekranların gördüğü davranış
iki durumda da aynı.

Ayarlar da zenginleşti: abonelik kartı artık rozet + tarih özeti gösterip
yönetim ekranına götürüyor, "Sesleri dene" düğmesi eklendi (sesler hiçbir
hoparlörde duyulmadı; kararı ayarlar ekranında verebilmek için) ve bir
"Hakkında" kartı geldi — sürüm numarası, gizlilik politikası ve Impressum.

Bu arada eski bir tutarsızlık kapandı: `ParentalGateReason.ExternalLink`
bugüne kadar **hiç çağrılmıyordu**, çünkü uygulamadan dışarı çıkan bağlantı
yoktu. Artık üç tane var (politika, Impressum, mağaza abonelik merkezi) ve
üçü de kilidin arkasında. README'nin "dışarı çıkan her bağlantı kilidin
arkasında" cümlesi ilk kez gerçekten doğru. Adresler tek yerde:
`Services/PloofyLinks.cs`.

### Harf Yazma (İ1)

Kütüphanenin on birinci oyunu ve dördüncü öğreticisi: ekranda bir harf ya da
rakamın boş şeridi duruyor, çocuk darbelerini **öğretilen sırayla** parmakla
çiziyor, harf gözünün önünde doluyor. Yolu Bul yazı öncesi beceriyi
(çizgi takip etmek) çalıştırıyordu; bu bir adım sonrası — belirli bir biçimi,
belirli bir sırayla.

**Önce ortak parça ayıklandı.** Yolu Bul'un takip mekaniği
`Engine/Games/Tracing/TracePath.cs`'e taşındı: tolerans, geri gitmeyen
ilerleme, parmak kalkınca korunan konum, çıkış başına bir hata. Yolu Bul artık
buna devrediyor. Ayıklamanın güvenli olmasının tek sebebi o oyunun 22 testi;
ikisi de aynı davranışı gösteriyor, hiçbiri değişmedi.

**Harf verisi elle yazıldı** (`GlyphShapes.cs`): 26 büyük harf, 10 rakam,
artı aksanlılar. Yapı taşları üç tane — düz çizgi dizisi, elips yayı ve
noktalarının **üstünden** geçen Catmull-Rom eğrisi; böylece S ya da 6 gibi bir
biçimi ayarlamak noktayı gözle doğru yere koymaktan ibaret. Her darbe boy
boyunca eşit aralıklı 40 noktaya indirgeniyor: ilerleme nokta indisinden
sayıldığı için sık örneklenen bir bölge yolun gereğinden büyük bir parçası
sayılıyor ve L'nin dikeyini bitiren çocuk harfi yarılanmış görüyor.

Üç karar açıklama istiyor:

- **Aksanlar çizilmiyor, çiziliyor.** Ç'nin kuyruğu, İ'nin noktası ve Ö'nün
  iki noktası baştan dolu duruyor. Bir noktanın yönü yok, dolayısıyla takip
  edilecek bir şeyi de yok. Gövde taban harften **paylaşılıyor**: C
  düzeltilince Ç de düzeliyor.
- **Almanca ß yok.** Sözcük başında hiç bulunmuyor, bu yaşta öğretilmiyor ve
  tek darbeyle anlatılabilecek bir biçimi yok. Kasıtlı olduğunu söyleyen bir
  test var.
- **Sekiz iki halka.** Tek darbede yazılan sekiz kendini kesiyor ve kesişme
  noktasında parmağın hangi kolda olduğu belirsizleşiyor.

**Bantlar.** En küçük bant Fidan — 2-4 yaş harf yazmıyor, o yaşın karşılığı
Yolu Bul. Fidan yalnızca kolay işaretleri görüyor (düz çizgiden oluşanlar ve
tek halkalılar: A E H I L O T U V X Y, 1 4 7 0); Meşe alfabenin tamamını,
aksanlılar dahil. Tolerans Yolu Bul'unkinden biraz geniş (0,09 / 0,065),
çünkü harf darbeleri kısa ve köşeli — aynı toleransta zorluk beceriden değil
biçimden geliyor.

**Doğrulama sayıyla.** Harf şekilleri ekrana bakılarak doğrulanamıyor, ekran
yok. Onun yerine biçim bozulunca kırılacak özellikler sınandı: kutunun dışına
taşma, kopuk ya da çok kısa darbe, seyrek örneklenmiş bölge, üç alfabenin
eksik harfi, kapalı halkanın başına dönmemesi, E'nin darbelerinin öğretim
sırası. 28 yeni test, toplam **264**.

### Örüntü (İ2)

Kütüphanenin on ikinci oyunu ve beşinci öğreticisi: ekranda tekrar eden bir
dizi duruyor, bir parçası eksik, çocuk alttaki seçeneklerden doğru olanı
seçiyor. Okul öncesi matematiğin belkemiği — örüntü görmek, saymadan önce
gelen ve toplamaya zemin hazırlayan beceri, ve on bir oyunun hiçbirinde yoktu.

Birimler harf dizisi olarak yazılıyor (AB, AAB, ABB, ABC, AABB); harfler
soyut, her turda başka parçalara karşılık geliyor. Dizi birimin döngüsel
tekrarı; sonu yarım kalması kusur değil, örüntü zaten sonsuz ve ekran onun
bir penceresi.

Bantlar:

- **Filiz** yalnızca AB görüyor ve parçalar **yalnızca renkçe** değişiyor —
  iki boyutta birden değişen bir dizi, örüntüyü henüz kavramamış bir çocuk
  için iki ayrı bilmece. Yanlış seçim hata sayılmıyor.
- **Fidan** AAB ve ABB'yi de görüyor, şekil de değişiyor.
- **Meşe** ABC ve AABB dahil hepsini görüyor, boşluk dizinin **ortasında**
  olabiliyor ve hedef süre var. Ortadaki boşluk belirgin biçimde zor: sondaki
  "sırada ne var" sorusu, ortadaki "burada ne eksik" — ikincisi sağdaki
  parçaları da hesaba katmayı gerektiriyor.

İki karar açıklama istiyor:

- **Boşluktan önce her zaman en az bir tam birim duruyor.** Aksi hâlde örüntü
  diziden okunamıyor ve soru bilmeceye değil kura çekmeye dönüyor. Bir test
  bunu bütün tohumlar için doğruluyor.
- **Çeldiriciler dizinin kendi parçaları.** Örüntüyü çözemeyen çocuğun eli
  oraya gidiyor, yani yanlış seçim "rastgele bir şeye bastım" değil "örüntüyü
  yanlış okudum" oluyor. Yetmezse aynı şeklin başka rengi üretiliyor —
  ekrandaki hiçbir şeye benzemeyen bir parçadan çok daha öğretici.

Yanlış seçimde dizi değişmiyor; çocuk yeniden bakıp deneyebiliyor. Say ve
Eşleştir ile Harf Avı'ndaki karar burada da geçerli.

Bu arada katalogdaki bir kural keskinleşti. "Öğretici oyunlar Fidan'dan
başlar" diyen test aslında **harfe ve sayıya** bakıyordu ("harf ve sayıların
anlam kazandığı bant"); Örüntü'de ikisi de yok ve Filiz'den başlıyor. Kural
metniyle uyumlu hâle getirildi, istisna da ayrı bir testle yazıldı.

Arayüz tarafında `ShapeTileView` eklendi (`Ploofy.Ui/Controls`): tek bir şekli
çizen küçük kare, hem dizide hem seçeneklerde kullanılıyor. Boşluk hayalet
olarak çiziliyor ve nabız gibi atıyor — dokuz kutucuk arasında hangisinin
eksik olduğunu hareketsiz bir kesik çizgi yeterince söylemiyor.

**25 yeni test, toplam 290.**

### Ebeveyn raporu (İ3)

Ebeveynin ücretli aboneliğin karşılığını gördüğü ekran: çocuk ne oynadı, ne
kadar oynadı, ne kazandı. Ayarlardan açılıyor, yani ebeveyn kilidinin
arkasında.

**Yeni tablo: `round_history`.** Bugüne kadar yalnızca "en iyisi ne"
tutuluyordu (`game_progress`) ve en iyiyi tutan bir satırdan geçmiş
çıkarılamıyor. Artık biten her tur ayrı bir satır: oyun, bant, yıldız, puan,
hata, süre ve **yerel** saatle zaman damgası. Yerel olması bilinçli — rapor
"hangi gün" diye gruplayacak ve gece 22:00'de oynanan bir tur ebeveyn için
bugün, UTC'de yarın. Satırlar hiç güncellenmiyor, yalnızca ekleniyor; profil
silinince hepsi gidiyor.

**Hesap motorda: `PlayReport`.** Grafiğin çubuğu doğru yükseklikte mi, ekrana
bakarak anlaşılmıyor — o yüzden gün kovaları, toplamlar ve oyun listesi
veritabanı olmadan sınanabilen bir sınıfta. İki savunma var:

- **Tur süresi 15 dakikada kırpılıyor.** Süre, tur başlarken çalışan bir
  kronometreden geliyor ve uygulama arka plana atıldığında kronometre
  durmuyor. Cihazı bırakıp akşam dönen çocuk, kırpma olmadan "bugün 6 saat
  oynadı" satırı üretiyor ve o tek satır bütün raporu yalancı yapıyor. Kırpma
  raporda, kayıtta değil: ham veri dürüst kalıyor.
- **Oynanmayan gün listede, sıfır değerle.** Boş günün yerini boş bırakmak,
  hafta sonu oynanmadığını gösteren tek şey; atlanan gün çubukları yan yana
  getirip eğilimi olduğundan düzgün gösteriyor.

**Grafik.** Günlük dakika, sütun grafiği. Çizgi değil: günler ayrık ve aradaki
iki günü birleştiren bir çizgi olmayan bir sürekliliği anlatıyor. Ölçek her
zaman sıfırdan başlıyor. Tek ölçü olduğu için gösterge kutusu yok — kartın
başlığı zaten neyin çizildiğini söylüyor. Renk Ocean'ın koyu ucu (`#0F6FC4`);
beyaz kart üstünde kontrast ölçüldü, geçiyor.

**Çizim ekrana bakılarak doğrulandı.** `TrendPainter` MAUI'den bağımsız, saf
SkiaSharp; küçük bir konsol programından çağrılıp PNG üretiliyor — kopya değil,
uygulamanın çizdiği dosyanın kendisi. Beş senaryo (14 gün tipik, tek yüksek
gün + bir dakikalık günler, tamamen boş, 30 gün, 7 gün) bakılarak üç kusur
bulundu ve düzeltildi:

1. Sütunlar yuvayı doldurup duvara dönüşüyordu — kalınlık artık yuvanın en
   fazla %55'i.
2. Gün adları tek harfti ve Türkçe'de ayırt etmiyordu: Pazar, Pazartesi ve
   Perşembe'nin üçü de "P". Kültürün `ShortestDayNames`'i tam olarak bunu
   veriyor; `AbbreviatedDayNames`'e geçildi (Paz/Pzt/Sal/Çar/Per/Cum/Cmt).
3. 30 günlük dönemde gün adları üst üste biniyor ve kenardakiler taşıyordu.
   Etiketler artık yazının gerçek genişliğine göre seyreltiliyor (bugün her
   zaman etiketli) ve kenarda içeri kelepçeleniyor.

Bir dakika oynanan gün, hiç oynanmayan günle aynı görünmüyor: en az 3 piksel
yüksekliğinde bir sütun çiziliyor. Sıfır yükseklikli bir sütun "hiç oynamadı"
der ve bu yanlış.

Ekranda ayrıca dönem seçici (7 / 14 / 30 gün), dört sayılık özet (süre, tur,
oynanan gün, yıldız), çok oynanandan aza oyun listesi ve birden çok çocuk
varsa profil şeridi var. En altta raporun cihazdan çıkmadığını söyleyen bir
satır duruyor.

**13 yeni test, toplam 303.**

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

**Yayımlandı (02.09.2026).** SchiriFit ve CertPilot'taki düzenin aynısı
kuruldu: sayfaların kaynağı bu depoda `docs/store/`, yayın nüshası ayrı ve
herkese açık `alikiratli/ploofy-web` deposunda, GitHub Pages'ten servis
ediliyor. Üçü de 200 dönüyor:

- https://alikiratli.github.io/ploofy-web/privacy-policy.html — Play
  Console'a **bu** adres girilecek, kök değil; denetim sayfanın kendisini
  görsün
- https://alikiratli.github.io/ploofy-web/impressum.html
- https://alikiratli.github.io/ploofy-web/ — mağaza girişindeki "Web sitesi"

On iki alan dolduruldu (2 Eylül 2026 · Ali Kiratli, Ischebecker Straße 8,
58256 Ennepetal · alikiratlide@gmail.com) ve "Taslak" rozeti hem HTML'den
hem betikten kaldırıldı. Yayıncı bilgileri uydurulmadı; CertPilot'un
Impressum'undan alındı.

Ploofy'nin de bir **Impressum**'u oldu (de/en, bağlayıcı olan Almanca) —
diğer iki uygulamada var, burada yoktu. § 5 DDG, § 19 UStG Kleinunternehmer,
§ 18/2 MStV, § 36 VSBG; artı aboneliğin mağazayla kurulduğunu söyleyen bir
bölüm.

Eski `../ploofy-privacy` yerel deposu artık gereksiz — içeriği `docs/store/`
ile `ploofy-web`'e taşındı.

**Sayılar:** 303 test geçiyor · 12 oyun tanımlı, 12'si oynanabilir ·
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

Sonrası 4. bölümdeki öncelik sırası; içerik yol haritası 5. bölümde.

## 2. Depo düzeni

- **src/Ploofy.Engine** — oyun mantığı, UI'ya sıfır bağımlılık (net10.0)
- **src/Ploofy.Data** — SQLite ilerleme deposu (net10.0)
- **src/Ploofy.Ui** — ortak MAUI arayüz katmanı: tema, çizim, ses/haptik, ebeveyn kilidi
- **src/Ploofy.Ui/Painting/TrendPainter.cs** — rapordaki grafiğin çizimi; MAUI'den
  bağımsız, bu yüzden konsoldan PNG'ye alınıp gözle bakılabiliyor
- **src/Ploofy.App** — MAUI uygulaması (Android + iOS; Windows sadece geliştirme için)
- **tests/Ploofy.Engine.Tests** — xUnit, motor + depo
- **content/strings.tsv** — üç dilin metinleri, tek kaynak
- **docs/store** — gizlilik politikası, Impressum ve tanıtım sayfası; yayın
  nüshası `alikiratli/ploofy-web` deposunda
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
- Harf Yazma: harfin darbeleri öğretilen sırayla parmakla çiziliyor; Fidan
  kolay işaretleri, Meşe alfabenin tamamını görüyor
- Örüntü: tekrar eden dizide eksik parçayı bulma; birim, boşluğun yeri ve
  seçenek sayısı banda göre değişiyor
- Ebeveyn raporu: dönem seçici, günlük süre grafiği, özet sayılar ve oyun
  listesi; kaynağı her turun kaydedildiği `round_history` tablosu
- Avatarlar: 32 emoji, üç tematik grup, her yerde renkli rozet olarak
- Sıralı oyun (pass-and-play): devir katmanı, her çocuk kendi bandında
- Sonuç ekranı: kupa animasyonu, yıldızlar, konfeti
- Yıldız ve rozet kaydı; bant değişince eski yıldızlar korunuyor
- Ses ve titreşim: yedi geri bildirim sesi, altı tuş notası; ayarlardan
  ikisi de kapatılabiliyor
- Uygulama ikonu ve açılış ekranı: gülen mavi kabarcık, sarı zemin
- Abonelik akışı: paywall → ebeveyn kilidi → kilitlerin açılması (mağaza bağlantısı hariç)
- Abonelik yönetimi (ayrı ekran): durum rozeti, ödenmiş dönemin sonu ve kaç
  gün kaldığı, "neler dahil", mağazanın abonelik merkezine çıkış ve
  **aboneliği bitirme**. Bitirme yalnızca yenilemeyi kapatıyor; oyunlar dönem
  sonuna kadar açık kalıyor, yıldız ve profiller hiç silinmiyor
- Ayarlar: abonelik özeti (rozet + tarih), sesleri deneme düğmesi, sürüm
  numarası ve gizlilik politikası / Impressum bağlantıları — ikisi de ebeveyn
  kilidinin arkasından tarayıcıda açılıyor

## 4. Sıradaki işler — sürüme kadar

Bu dört madde 1.0'ı bloke ediyor ve hepsi bu bilgisayarın dışında bir şey
istiyor. İçerik yol haritası ayrı: 5. bölüm.

**Öncelik 1 — Fiziksel cihaz testi.** Artık en acil olan bu ve iki başlığı
var: parmak (isabet, gerçek kare hızı) ve hoparlör (seslerin dengesi).
Bakılacaklar 1. bölümün sonunda.

**Öncelik 2 — Abonelik.** `LocalSubscriptionService` şu an satın almayı
başarılı sayıyor. Gerçek mağaza bağlantısı (Plugin.InAppBilling) aynı
`ISubscriptionService` arayüzünün arkasına takılacak; ekranlar
değişmeyecek. Play Console'da ürün tanımı ve test hesabı gerekiyor —
onlar olmadan yazılacak kod denenemez, o yüzden mağaza hesabı ilk adım.

**Öncelik 3 — iOS.** Hiç denenmedi. Mac gerektiriyor.

**Öncelik 4 — Mağaza vitrini.** Play Console yaş beyanı, App Store Kids
kategorisi, Data safety formu, içerik derecelendirme anketi, ekran
görüntüleri ve üç dilde tanıtım metinleri. İkon ve açılış ekranı tamam.
Gizlilik politikası URL'i hazır (1. bölüm); Play Console'a
`privacy-policy.html` doğrudan girilecek. Sürüm `1.0`, versionCode `1` —
Play'e her yüklemede versionCode'un artması gerekiyor.

İki uyarı politikanın metniyle ilgili: 4. bölümü (abonelik) gerçek billing
bağlanmadan tam doğru değil, çünkü satın almanın Play üzerinden yürüdüğünü
anlatıyor — Öncelik 2 ile birlikte gözden geçirilmeli. Ve metnin bir
hukukçuya okutulması yerinde olur; iddialar koda dayanıyor ama yasal biçim
ayrı bir iş.

## 5. İçerik yol haritası — sürümden sonra

Oyun kütüphanesi 1.0 için yeterliydi (10 oyun, beş etkileşim türü) ve şu an
12'de. Buradakiler kütüphaneyi derinleştiriyor, sürümü bloke etmiyor. Sıra
kasıtlı: önce boşluğu büyük olup teknik olarak ucuz olanlar.

**İ1 — Harf ve rakam yazma. ✅ Bitti (02.09.2026).** Ayrıntı 1. bölümde,
"Harf Yazma" başlığı.

**İ2 — Örüntü tamamlama. ✅ Bitti (02.09.2026).** Ayrıntı 1. bölümde,
"Örüntü" başlığı.

**İ3 — Ebeveyn raporu. ✅ Bitti (02.09.2026).** Ayrıntı 1. bölümde,
"Ebeveyn raporu" başlığı.

**İ4 — Sıralama ve karşılaştırma.** Büyükten küçüğe dizme, "hangisi daha çok".
Say ve Eşleştir sayıyor ama karşılaştırmıyor. Şekil Ayırma'nın sürükleme
altyapısı doğrudan kullanılabilir.

**İ5 — Yıldızların bir karşılığı.** Şu an yıldız birikiyor ve hiçbir şey
açmıyor. 32 emoji avatar zaten var; yıldızla açılan avatarlar/çıkartmalar
küçük bir iş, motivasyon döngüsünü kapatıyor.

**İ6 — Ekran süresi sınırı.** "15 dakika sonra nazikçe bitir." Ebeveyn
ekranına yakışıyor ve abonelik tarafında somut bir değer.

**İ7 — Kategori ayırma, basit toplama, boyama.** Sırasıyla: hayvan/araç
ayırma (Şekil Ayırma'nın motoru genelleşir), Meşe bandı için toplama (Say ve
Eşleştir'in devamı), ve Filiz için boyama — kaybetmenin olmadığı serbest oyun,
bu yaşta Sago Mini'nin bütün işi o.

**İ8 — Bant içi uyarlama.** Üç bant kaba; çocuk üst üste başarıyorsa zorluğu
bir kademe artırmak. `BandValue<T>` mimarisi buna hazır. Riski var: gizli
zorluk değişimi ebeveyni şaşırtır, o yüzden görünür olmalı.

**Asıl darboğaz — sesli yönerge.** Öğretici üç oyunda yönerge tamamen görsel.
Filiz bandı (2-4 yaş) henüz okumuyor, yani öğrenme oyunlarının o banda
gerçekten ulaşması bundan geçiyor. Sentez yetmiyor, üç dilde insan kaydı
gerekiyor. Yeni oyun eklemekten önce bu gelirse mevcut üç oyun bir anda iki
kat işe yarar hâle gelir.

**Bilerek yapılmayacaklar.** Günlük seri/streak: bu yaş grubunda suçluluk
mekaniği, Sago Mini ve Toca Boca bilerek kaçınıyor. Yerel ağ eşleşmesi:
arayüzde "yakında" duruyor ama pass-and-play ihtiyacı karşılıyor,
karmaşıklığı değmiyor.

## 6. Bilinen eksikler

- Gerçek satın alma yok; abonelik cihazda sahte olarak açılıyor ve iptal de
  yerel — ekranlar hazır, arkasına Plugin.InAppBilling takılacak
- Yayın anahtarının makine dışında yedeği yok; `ploofy-release.keystore`
  ve `Ploofy.local.props` yalnızca bu bilgisayarda duruyor
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

## 7. Yeni makinede kurulum

1. **.NET 10 SDK** (10.0.400 ile kuruldu) — API 36 hedefi .NET 9'da yok
2. `dotnet workload install maui` — **yönetici** terminal gerektiriyor
3. Android SDK ve **JDK 17** (daha yeni JDK kabul edilmiyor)
4. Kök dizine `Ploofy.local.props` oluştur (depoya girmiyor):
   AndroidSdkDirectory ve JavaSdkDirectory özellikleri
5. `dotnet restore && dotnet test`

Hızlı deneme (Windows): `dotnet build src/Ploofy.App/Ploofy.App.csproj -f net10.0-windows10.0.19041.0`

## 8. Tekrar tuzağa düşmemek için

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

## 9. Yeni mini oyun ekleme

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
   `ParticleField`, `PloofyPalette` hazır. Parmakla çizgi takibi gerekiyorsa
   `TracePath` var — tolerans, geri gitmeyen ilerleme ve çıkış sayımı orada,
   yeniden yazma. Sürükleme için MAUI'nin
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
