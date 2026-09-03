# Mağaza vitrini — Play Console'a girilecek her şey

Bu dosya kaynak. Play Console'daki alanlara buradan kopyalanıyor; tersi
değil. Metin değişirse önce burası değişir, çünkü Console'daki hâli
sürüm geçmişinde izlenemiyor.

Uygulama: **Ploofy** — `io.ploofy.app`
Sürüm: `1.0`, versionCode `1`
Diller: Türkçe (tr-TR), İngilizce (en-US), Almanca (de-DE)

Play Console'un varsayılan dili **en-US** olmalı: uygulamanın nötr dili de o
(`Directory.Build.props` → `NeutralLanguage`). Türkçe ve Almanca birer
çeviri olarak eklenir.

---

## 1. Uygulama adı (en fazla 30 karakter)

| Dil | Metin | Uzunluk |
| --- | --- | --- |
| en-US | `Ploofy: Kids Learning Games` | 27 |
| tr-TR | `Ploofy: Çocuk Oyunları` | 22 |
| de-DE | `Ploofy: Lernspiele für Kids` | 27 |

Ad "Ploofy" ile başlıyor ve tanımlayıcı kısım arkadan geliyor. Tersi
denenmemeli: Play'in adlandırma kurallarında marka öne geçmezse liste
sayfasında arama sonuçlarında kırpılıyor.

Adda **"En iyi", "Numara 1", "Ücretsiz"** gibi ifade yok — Play bunları
metadata politikasıyla reddediyor.

---

## 2. Kısa açıklama (en fazla 80 karakter)

| Dil | Metin | Uzunluk |
| --- | --- | --- |
| en-US | `14 calm games for ages 2-9. No ads, ever. Nothing leaves the tablet.` | 68 |
| tr-TR | `2-9 yaş için 14 sakin oyun. Reklam yok. Hiçbir veri cihazdan çıkmıyor.` | 70 |
| de-DE | `14 ruhige Spiele für 2-9 Jahre. Keine Werbung. Nichts verlässt das Gerät.` | 73 |

Bu satır arama sonucunda başlığın altında görünen tek metin. Üç şey
söylüyor ve üçü de ebeveynin ilk sorduğu şey: kaç oyun, hangi yaş,
reklam var mı.

---

## 3. Tam açıklama (en fazla 4000 karakter)

### en-US

```
Ploofy is a collection of quiet, ad-free mini games for children aged 2 to 9.

No ads. No accounts. No data leaves the tablet. Ever — not even in the free tier.

WHAT IS INSIDE

Fourteen games, grouped by what they ask of a child:

Play
• Memory Match — turn the cards, find the pairs
• Bubble Pop — pop the bubbles, or just watch them drift
• Shape Sort — drag each shape into the box that fits
• Find the Way — follow the winding path with a finger
• Jigsaw — put the picture back together
• Repeat the Beat — watch the sequence, then play it back
• Catch the Basket — move the basket, catch what falls

Learn
• Letter Hunt and Number Hunt — spot the letter or the number
• Count and Match — drag the group onto the right numeral
• Letter Writing — trace each stroke in the order it is taught, with the
  stroke numbers and direction arrows from a handwriting workbook
• Patterns — find the missing piece in a repeating sequence
• Line Up — arrange by size, or by how many
• Connect the Dots — follow the numbers and an animal appears

THREE AGE BANDS

Sprout (2-4), Sapling (4-6) and Oak (6-9). The band is not a label: it
changes how every single game plays — how many pieces, how fast, how
forgiving. A child keeps their stars when they move up.

STARS THAT MEAN SOMETHING

Stars unlock new friends to wear. Twenty of them, from a unicorn to a
whale. The collection screen shows what is coming next and how close it is.

BUILT FOR SHARING A TABLET

Add a profile for each child. Many games can be played in turns on the same
device, each child in their own band.

FOR PARENTS

• A parent gate — a small arithmetic question — guards settings, profiles
  and purchases
• A parent report shows how long was played on which day and which games
  were practised. It is read on the device and never sent anywhere
• Turkish, English and German, switchable at any time
• No losing. Nothing has a game-over screen; a wrong answer means try again

SUBSCRIPTION

Two games are fully open, free and unlimited. The rest are unlocked by a
subscription through Google Play. There is no other purchase, no currency,
no loot box and no ad in any tier.

PRIVACY

Ploofy has no server. There is nothing to sign up for. A nickname, the
stars and the play history stay in the app's own folder on the device and
are deleted when the app is uninstalled. The full policy is linked below.
```

### tr-TR

```
Ploofy, 2-9 yaş çocuklar için sakin ve reklamsız mini oyunlardan oluşan bir koleksiyon.

Reklam yok. Hesap yok. Hiçbir veri cihazdan çıkmıyor — ücretsiz kullanımda da.

İÇİNDE NE VAR

On dört oyun, çocuktan ne istediğine göre ikiye ayrılmış:

Eğlence
• Eşleştirme Kartları — kartları çevir, eşleri bul
• Balon Patlatma — balonları patlat, ya da sadece süzülüşlerini izle
• Şekil Ayırma — her şekli kendi kutusuna sürükle
• Yolu Bul — kıvrımlı yolu parmakla takip et
• Yapboz — resmi yeniden birleştir
• Sırayı Tekrarla — diziyi izle, sonra sen çal
• Sepeti Tut — sepeti kaydır, düşenleri yakala

Öğrenme
• Harf Avı ve Sayı Avı — aranan harfi ya da rakamı bul
• Say ve Eşleştir — kümeyi doğru rakamın üstüne bırak
• Harf Yazma — her darbeyi öğretilen sırayla çiz; darbe numaraları ve
  yön okları yazı defterindeki gibi duruyor
• Örüntü — tekrar eden dizide eksik parçayı bul
• Sırala — boyuta ya da miktara göre diz
• Noktaları Birleştir — rakamları takip et, bir hayvan ortaya çıksın

ÜÇ YAŞ BANDI

Filiz (2-4), Fidan (4-6) ve Meşe (6-9). Bant bir etiket değil: her oyunun
nasıl oynandığını değiştiriyor — kaç parça, ne hızda, ne kadar affedici.
Çocuk bir üst banda geçtiğinde yıldızlarını kaybetmiyor.

BİR KARŞILIĞI OLAN YILDIZLAR

Yıldızlar takılacak yeni arkadaşlar açıyor. Tek boynuzlu attan balinaya,
yirmi tane. Koleksiyon ekranı sıradakini ve ona ne kadar kaldığını
gösteriyor.

TABLET PAYLAŞMAK İÇİN

Her çocuk için ayrı profil. Oyunların çoğu aynı cihazda sırayla
oynanabiliyor, her çocuk kendi bandında.

EBEVEYNLER İÇİN

• Ebeveyn kilidi — küçük bir aritmetik sorusu — ayarları, profilleri ve
  satın almayı koruyor
• Ebeveyn raporu hangi gün ne kadar oynandığını ve hangi oyunların
  çalışıldığını gösteriyor. Cihazda okunuyor, hiçbir yere gönderilmiyor
• Türkçe, İngilizce ve Almanca; istediğiniz an değiştirilebiliyor
• Kaybetmek yok. Hiçbir oyunda "oyun bitti" ekranı yok; yanlış cevap
  "tekrar dene" demek

ABONELİK

İki oyun tamamen açık, ücretsiz ve sınırsız. Geri kalanı Google Play
üzerinden bir abonelikle açılıyor. Başka satın alma, oyun içi para birimi,
sandık ya da hiçbir katmanda reklam yok.

GİZLİLİK

Ploofy'nin sunucusu yok. Kaydolunacak bir şey yok. Takma ad, yıldızlar ve
oyun geçmişi cihazın kendi uygulama klasöründe kalıyor ve uygulama
kaldırıldığında siliniyor. Politikanın tamamı aşağıda bağlı.
```

### de-DE

```
Ploofy ist eine Sammlung ruhiger, werbefreier Minispiele für Kinder von 2 bis 9 Jahren.

Keine Werbung. Kein Konto. Keine Daten verlassen das Gerät — auch nicht in der kostenlosen Version.

WAS DRIN IST

Vierzehn Spiele, gruppiert danach, was sie vom Kind verlangen:

Spielen
• Memory — Karten umdrehen, Paare finden
• Blasen platzen — Blasen zerplatzen lassen oder einfach zusehen
• Formen sortieren — jede Form in die passende Kiste ziehen
• Finde den Weg — dem gewundenen Pfad mit dem Finger folgen
• Puzzle — das Bild wieder zusammensetzen
• Wiederhole die Folge — die Folge ansehen, dann nachspielen
• Korb fangen — den Korb bewegen, das Fallende auffangen

Lernen
• Buchstabenjagd und Zahlenjagd — den gesuchten Buchstaben oder die Zahl finden
• Zählen und Zuordnen — die Menge auf die richtige Ziffer ziehen
• Buchstaben schreiben — jeden Strich in der gelehrten Reihenfolge nachziehen,
  mit Strichnummern und Richtungspfeilen wie im Schreibheft
• Muster — das fehlende Teil in einer sich wiederholenden Folge finden
• Aufreihen — nach Größe oder nach Menge ordnen
• Punkte verbinden — den Zahlen folgen, und ein Tier erscheint

DREI ALTERSSTUFEN

Spross (2-4), Setzling (4-6) und Eiche (6-9). Die Stufe ist kein Etikett:
sie verändert, wie jedes einzelne Spiel gespielt wird — wie viele Teile, wie
schnell, wie nachsichtig. Beim Aufsteigen bleiben die Sterne erhalten.

STERNE, DIE ETWAS BEDEUTEN

Sterne schalten neue Freunde zum Anlegen frei. Zwanzig Stück, vom Einhorn
bis zum Wal. Die Sammlung zeigt, wer als Nächstes kommt und wie nah er ist.

ZUM TEILEN EINES TABLETS

Für jedes Kind ein eigenes Profil. Die meisten Spiele lassen sich abwechselnd
auf demselben Gerät spielen, jedes Kind in seiner eigenen Stufe.

FÜR ELTERN

• Eine Elternsperre — eine kleine Rechenaufgabe — schützt Einstellungen,
  Profile und Käufe
• Ein Elternbericht zeigt, an welchem Tag wie lange gespielt und welche
  Spiele geübt wurden. Er wird auf dem Gerät gelesen und nirgendwohin gesendet
• Türkisch, Englisch und Deutsch, jederzeit umschaltbar
• Kein Verlieren. Kein Spiel hat einen Game-over-Bildschirm; eine falsche
  Antwort heißt „noch mal versuchen“

ABO

Zwei Spiele sind vollständig offen, kostenlos und unbegrenzt. Der Rest wird
über ein Abo bei Google Play freigeschaltet. Es gibt keinen weiteren Kauf,
keine Spielwährung, keine Lootbox und in keiner Stufe Werbung.

DATENSCHUTZ

Ploofy hat keinen Server. Es gibt nichts, wofür man sich anmelden müsste.
Spitzname, Sterne und Spielverlauf bleiben im eigenen Ordner der App auf dem
Gerät und werden beim Deinstallieren gelöscht. Die vollständige Erklärung
ist unten verlinkt.
```

---

## 4. Kategori ve iletişim

| Alan | Değer |
| --- | --- |
| Uygulama türü | Uygulama değil, **Oyun** |
| Kategori | **Eğitim** (Educational) |
| E-posta | alikiratlide@gmail.com |
| Web sitesi | https://alikiratli.github.io/ploofy-web/ |
| Gizlilik politikası | https://alikiratli.github.io/ploofy-web/privacy-policy.html |

Gizlilik politikası alanına **kökün değil, doğrudan politika sayfasının**
adresi giriliyor. Play kökü verilen başvuruları "politika bulunamadı"
diyerek reddediyor.

---

## 5. Data safety formu

Play Console → App content → Data safety. Cevaplar
`docs/store/privacy-policy.html` ile birebir uyuşmalı; uyuşmazlık tek
başına ret sebebi.

**Does your app collect or share any of the required user data types?**
→ **No.**

Gerekçe (Console'a girilmiyor, buraya not): "collect" Play'in tanımında
uygulamanın veriyi **cihazdan dışarı** taşıması demek. Ploofy'nin sunucusu
yok, analitiği yok, çökme raporlaması yok, reklam SDK'sı yok. Cihazda
tutulan her şey (profil, yıldız, oyun geçmişi, ayarlar) uygulamanın kendi
klasöründe kalıyor ve `allowBackup="false"` ile buluta da gitmiyor.

**Is all of the user data collected by your app encrypted in transit?**
→ Soru sorulmuyor (veri toplanmıyor).

**Do you provide a way for users to request that their data be deleted?**
→ Soru sorulmuyor. Yine de politikada anlatılıyor: profili silmek ya da
uygulamayı kaldırmak her şeyi siliyor.

Bir uyarı: **gerçek billing bağlandıktan sonra bu form yeniden
gözden geçirilmeli.** Play Billing kütüphanesi satın alma jetonunu Google'a
gönderiyor; Play bunu "Purchase history" başlığı altında beyan
ettirebiliyor. Şu anki cevap, `LocalSubscriptionService` ile doğru.

---

## 6. İçerik derecelendirme anketi (IARC)

Play Console → App content → Content rating.

| Soru | Cevap |
| --- | --- |
| Kategori | Uygulama türü: **Oyun** |
| Şiddet | Yok |
| Cinsellik / çıplaklık | Yok |
| Küfür | Yok |
| Uyuşturucu, alkol, tütün | Yok |
| Kumar (gerçek ya da simüle) | Yok |
| Korku öğeleri | Yok |
| Kullanıcılar arası etkileşim | **Yok** — sohbet, arkadaş listesi, kullanıcı içeriği paylaşımı yok |
| Konum paylaşımı | Yok |
| Kişisel bilgi paylaşımı | Yok |
| Dijital satın alma | **Var** — abonelik |
| Reklam | **Yok**, hiçbir katmanda |

Beklenen sonuç: PEGI 3 / ESRB Everyone / USK 0.

**"Kullanıcılar arası etkileşim" sorusuna "yok" demek doğrudur** ve
sıralı oyun (pass-and-play) bunu değiştirmez: iki çocuk aynı cihazı elden
ele veriyor, ağ üzerinden bir bağlantı kurulmuyor. Arayüzde "yakında"
olarak duran yerel ağ eşleşmesi **gerçekten eklenirse bu cevap değişir.**

---

## 7. Hedef kitle ve içerik (Target audience and content)

Play Console → App content → Target audience and content.

- **Hedef yaş grupları:** 5 yaş altı, 6-8, 9-12 — yani uygulama
  **yalnızca çocuklara** yönelik. Bu beyan uygulamayı Play'in
  **Families** politikasına sokuyor.
- **Reklam var mı:** Hayır.
- **Families reklam SDK'sı beyanı:** Uygulamada reklam SDK'sı yok.
- **Ayrıca:** Uygulama yalnızca çocuklara yönelik olduğu için Play,
  Designed for Families programına katılım sorabilir. Katılmak
  isteğe bağlı; katılmak Play'in çocuk uygulamaları vitrinlerine
  çıkmayı sağlıyor ve ek bir teknik gereklilik getirmiyor (reklam yok,
  analitik yok, veri toplanmıyor — üç şart da zaten sağlanıyor).

---

## 8. Ekran görüntüleri

Play en az **2**, en fazla 8 tablet ekran görüntüsü istiyor; yatay olmalı
(uygulama yatayda kilitli). Üç dilin her biri için ayrı set yüklemek
zorunlu değil ama listenin o dilde inandırıcı olması için yükleniyor.

Çekilecek sekiz kare, bu sırayla:

1. **Ana ekran** — oyun kutucukları, üstte çocuğun avatarı ve yıldız
   sayacı. Vitrinin ilk karesi bu olmalı: kütüphanenin genişliğini tek
   bakışta gösteren tek ekran
2. **Noktaları Birleştir** — yarısı çizilmiş bir hayvan, rakamlar görünür
3. **Harf Yazma** — darbe numaraları ve yön okları görünür durumda
4. **Koleksiyon** — açılmış avatarlar, sıradaki ödül ve dolan çubuk
5. **Say ve Eşleştir** ya da **Sırala** — öğretici tarafın ikinci örneği
6. **Ebeveyn raporu** — günlük süre grafiği. Ebeveyni ikna eden kare bu
7. **Sonuç ekranı** — kupa, yıldızlar, yeni açılan avatar şeridi
8. **Ayarlar** — üç dil, ses/titreşim, abonelik özeti

Çekim notları:

- Profil adı olarak gerçek bir çocuk adı **kullanılmamalı**. "Ada" ve
  "Efe" (tr), "Mia" ve "Leo" (de/en) uydurma ve nötr
- Ekranlarda yıldız sayısı sıfır olmamalı: boş bir koleksiyon ekranı
  ödülü değil eksikliği gösteriyor. Çekimden önce birkaç tur oynanmalı
- Emülatörde değil **gerçek tablette** çekilmeli — emülatörün yazı
  tipi kerningi bazı emojileri farklı gösteriyor

Ayrıca gereken grafikler:

| Öğe | Boyut | Not |
| --- | --- | --- |
| Uygulama ikonu | 512×512 PNG | `Resources/AppIcon` içindeki kabarcıktan üretilir |
| Öne çıkan grafik | 1024×500 PNG | Zorunlu. Sarı zemin + kabarcık + uygulama adı |

---

## 9. Sürüm notları (Release notes, en fazla 500 karakter)

İlk sürüm olduğu için üçü de aynı şeyi söylüyor.

**en-US**
```
The first release. Fourteen games for ages 2 to 9, in three age bands.
No ads, no accounts, nothing leaves the tablet.
```

**tr-TR**
```
İlk sürüm. 2-9 yaş için üç yaş bandında on dört oyun.
Reklam yok, hesap yok, hiçbir veri cihazdan çıkmıyor.
```

**de-DE**
```
Die erste Version. Vierzehn Spiele für 2 bis 9 Jahre in drei Altersstufen.
Keine Werbung, kein Konto, nichts verlässt das Gerät.
```

---

## 10. Yüklemeden önceki kontrol listesi

- [ ] Gerçek tablette uçtan uca oynandı (bkz. ilerleme notu, 1. bölüm)
- [ ] `dotnet publish -f net10.0-android -c Release` imzalı `.aab` üretti
- [ ] Yayın anahtarının makine dışında bir yedeği var
- [ ] Gizlilik politikası yayımlandı ve adresi açılıyor
- [ ] Play Console'da abonelik ürünü tanımlandı ve test hesabı eklendi
- [ ] Gerçek billing bağlandı; sonra Data safety formu (5. bölüm) yeniden
      okundu ve politikanın abonelik bölümü gözden geçirildi
- [ ] Ekran görüntüleri gerçek cihazda çekildi
- [ ] Bu dosyadaki metinler Console'a kopyalandı
- [ ] versionCode `1` — sonraki her yüklemede artırılacak
