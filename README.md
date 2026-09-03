# Ploofy

Werbefreie Minispielsammlung für Kinder von 2 bis 9 Jahren. .NET MAUI
(Android + iOS).

Sieben Spaß- und drei Lernspiele; eine einzige Schwierigkeitsachse (drei
Altersstufen), eine einzige Sternesammlung, ein monatliches Abo. Kein Server,
kein Konto, keine Werbung: Kein Datum des Kindes verlässt das Gerät.

## Aufbau des Repos

```
Ploofy.sln
Directory.Build.props          Gemeinsame Build-Einstellungen aller Projekte
src/Ploofy.Engine/             Spiellogik — null Abhängigkeit zur UI, net10.0
src/Ploofy.Data/               SQLite-Fortschrittsspeicher (sqlite-net), net10.0
src/Ploofy.Ui/                 Gemeinsame MAUI-Oberflächenschicht (Theme, Ton/
                               Haptik, Elternsperre, Sternesteuerung)
src/Ploofy.App/                MAUI-Anwendung (Android + iOS; Windows nur zum
                               schnellen Ausprobieren während der Entwicklung)
content/strings.tsv            Texte aller drei Sprachen — die einzige Quelle
docs/store/                    Datenschutzerklärung, Impressum und Startseite —
                               die Quelle der veröffentlichten Seiten
tools/build_strings.py         strings.tsv -> Resources/Strings/*.resx
tools/build_sounds.py          synthetisiert die Rückmeldungstöne -> Resources/Raw/sounds/
tests/Ploofy.Engine.Tests/     xUnit — Tests für Engine + Speicher
```

`Ploofy.Engine` ist bewusst von MAUI unabhängig: Die Regeln lassen sich auf dem
Desktop in Sekunden testen, und später kann eine zweite Spielefamilie (z. B. eine
eigene App mit Vorschulmathematik) darauf aufbauen, ohne die Engine anzufassen.

## Die Grundbegriffe der Engine

| Begriff | Datei | Was er tut |
|---|---|---|
| `AgeBand` | `Engine/AgeBand.cs` | Filiz/Spross (2-4), Fidan/Setzling (4-6), Meşe/Eiche (6-9). Die **einzige** Schwierigkeitsachse der App. |
| `BandValue<T>` | `Engine/Difficulty/` | Der Wert jedes Reglers eines Spiels je Altersstufe. Die Schwierigkeitstabelle steht in einer Zeile. |
| `DifficultyProfile` | `Engine/Difficulty/` | Der gemeinsame Vertrag, an den sich jedes Spiel hält: Kann man verlieren, ist die Zeit sichtbar, wird Text verwendet. |
| `GameCatalog` | `Engine/Catalog/` | Das einzige Verzeichnis aller Spiele. Sperre, Altersfilter und Elternbereich speisen sich daraus. |
| `TurnController` | `Engine/Sessions/` | Die einzige Stelle, die Reihenfolge, Runden und Punkte führt. Auch im Einzelspiel läuft dieselbe Klasse. |
| `ISessionTransport` | `Engine/Sessions/` | Der Kanal der Sitzungsereignisse. Heute geräteintern; lokales Netz und Familienverbindung kommen dahinter. |
| `Entitlements` | `Engine/Access/` | Die einzige Entscheidungsstelle für die Stufenregeln. Kein Bildschirm entscheidet selbst, ob jemand Abonnent ist. |
| `ParentalGateChallenge` | `Engine/Access/` | Die Elternsperre. Eine Rechenaufgabe oberhalb der Stufe Eiche. |
| `StarRating` | `Engine/Progress/` | Die einzige Stelle, die aus einer Runde Sterne macht. Die Regel ändert sich mit der Altersstufe. |
| `BubblePopRound` | `Engine/Games/` | Entstehen, Aufsteigen und Platzen der Blasen. Die Positionen sind normalisiert, also unabhängig von der Bildschirmgröße, und im Test wird die Uhr von Hand weitergestellt. |
| `TracePath` | `Engine/Games/Tracing/` | Eine Linie, die mit dem Finger verfolgt wird: Toleranz, Fortschritt, der nie zurückgeht, und das Zählen der Ausrutscher. Finde den Weg und Buchstaben schreiben teilen sich diese Mechanik. |
| `GlyphShapes` | `Engine/Games/Tracing/` | Die Schreibwege der Großbuchstaben und Ziffern: Striche in Lehrreihenfolge, dazu nicht nachgefahrene Zeichen (der Punkt auf dem İ, die Cedille des Ç). |
| `PatternRound` | `Engine/Games/` | Eine sich wiederholende Reihe mit einer Lücke. Die Einheit (AB, AAB, ABC, AABB) und die Lage der Lücke hängen an der Altersstufe; vor der Lücke steht immer mindestens eine vollständige Einheit. |
| `PlayReport` | `Engine/Progress/` | Der Elternbericht: Tagesbalken, Summen und die Spieleliste eines Zeitraums. Jede einzelne Runde wird bei 15 Minuten gekappt, damit eine vergessene App den Bericht nicht auffrisst. |
| `LineUpRound` | `Engine/Games/` | Reihenfolge und Vergleich. Die jüngste Stufe sortiert nach Größe (ohne Zählen), ab der mittleren nach Menge; nur die sortierte Eigenschaft ändert sich je Aufgabe. Die Runde wechselt nicht von selbst weiter — `NextPuzzle` gehört der Oberfläche. |

### Ein neues Minispiel hinzufügen

1. Eine Zeile in `Engine/Catalog/GameCatalog.cs` (Id, Interaktionsart, Stufe,
   kleinste Altersstufe, Zeichentechnik).
2. Die Regeln unter `Engine/Games/` als oberflächenunabhängige Klasse schreiben;
   die Schwierigkeitsregler mit `BandValue<T>` definieren.
3. Auf App-Seite eine Seite zur Id, eine Zeile in `GamePresentation` (Name,
   Symbol, Route) und in `content/strings.tsv` ein Name in drei Sprachen.

Ist das Spiel dauernd in Bewegung, schreibe die Darstellung als `SKCanvasView`
unter `Ploofy.Ui/Controls` und nutze die fertigen Bausteine aus `Painting/`
(`BubblePainter`, `ParticleField`, `PloofyPalette`).

Sperre, Altersfilter, Sterneerfassung und Elternbereich funktionieren von selbst.

## Die Spielesammlung

**Spaßspiele (7):** Memory · Blasen platzen · Formen sortieren ·
Wiederhole die Folge · Fang den Korb · Finde den Weg · Puzzle

**Lernspiele (7):** Buchstabenjagd · Zahlenjagd · Zählen und Zuordnen ·
Buchstaben schreiben · Was kommt als Nächstes · Der Reihe nach ·
Punkte verbinden

Alle vierzehn sind spielbar.

Sie decken fünf verschiedene Interaktionsarten ab (Tippen, Ziehen, einer Linie
folgen, Gedächtnis, Reihenfolge) — dieses Maß löst das Problem "alles fühlt sich
gleich an" von Anfang an.

**Zeichentechnik:** karten- und kachelbasierte Spiele mit MAUI-Steuerelementen;
alles, was dauernde Bewegung, Partikel oder freies Zeichnen braucht (Blasen
platzen, Finde den Weg, Puzzle, Fang den Korb, Buchstaben schreiben, Der
Reihe nach), mit SkiaSharp.

Wo das Layout selbst die Frage ist — der Balken im Elternbericht, die Reihe
in "Der Reihe nach" — liegt das Zeichnen MAUI-frei in `Ploofy.Ui/Painting`
(`TrendPainter`, `LineUpPainter`). Dieselben Klassen lassen sich aus einem
kleinen Konsolenprogramm nach PNG zeichnen und mit dem Auge prüfen; genau
so wurden dort mehrere Layoutfehler gefunden.

## Die Bildsprache

Die Besten der Kategorie (Sago Mini, Toca Boca, Khan Academy Kids) unterscheiden
sich in einem Punkt: Nichts auf dem Bildschirm steht still. Ploofys
Gestaltungsregeln liegen in `Ploofy.Ui` und gelten für alle Spiele gemeinsam:

| Regel | Wo | Warum |
|---|---|---|
| Keine Fläche ist einfarbig | Verläufe in `Theme/PloofyStyles.xaml` | Verlauf + Schatten machen aus der Kachel statt eines "gedruckten Bildes" ein Ding zum Anfassen |
| Dinge erscheinen nicht, sie federn herein | `BubbleSurface.BirthScale`, `MemoryCardView` | Lineares Wachsen wirkt wie ein Einblenden; ein leichtes Überschwingen fühlt sich an wie "plopp, da ist es" |
| Auch was stillsteht, atmet | Dehnungsphase in `BubbleSurface` | Jede Blase hat ihre eigene Phasenverschiebung; dehnen sich alle gleichzeitig, wirkt es mechanisch |
| Jede Berührung hat eine sichtbare Folge | `ParticleField` | Die Blase verschwindet nicht, sie zerstiebt — darin steckt das ganze Erfolgsgefühl |
| Falsch wird nicht bestraft, sondern gezeigt | Die falsche Farbe platzt nicht, sie wackelt | Würde sie platzen, wäre auch das Falsche eine Belohnung |
| Berührungsziel ≥ 64 Einheiten | `TouchTarget` | Der Finger eines kleinen Kindes ist groß, seine Treffsicherheit gering |
| Kein dunkles Theme | `PloofyColors.xaml` | Auf kleinen Bildschirmen senkt ein dunkler Grund Farbunterscheidung und Lesbarkeit |

Die Blase selbst besteht aus vier Schichten: weicher Schatten, Körperverlauf mit
Licht von links oben, feiner Randring und zwei Glanzflecken. Das Glasgefühl
kommt allein von den letzten beiden.

## Sterne und die Sammlung

Sterne sammelten sich lange an, ohne etwas zu bewirken. Jetzt schaltet die
**Gesamtzahl** neue Avatare frei — alle drei Sterne einen, zwanzig Stück
insgesamt. Der erste kommt nach einer perfekten Runde, damit die Regel ohne
Erklärung sichtbar wird.

Die Leiter liegt in `Engine/Progress/RewardLadder.cs`, die Reihenfolge der
Avatare in `App/Services/AvatarCatalog.cs`. Es gibt **keine neue Tabelle**:
was freigeschaltet ist, wird immer aus der Gesamtzahl abgeleitet. Gespeichert
wird nur, wo die Feier zuletzt stand (`rewards_seen:<Profil-ID>` in den
Einstellungen) — so geht keine Belohnung verloren und keine wird zweimal
gefeiert.

Der Sammelbildschirm liegt **nicht** hinter der Elternsperre: ein Kind, das
für seine verdiente Figur die Eltern rufen muss, verliert die Hälfte der
Belohnung.

## Spielzeit-Limit

Eltern können pro Kind eine **tägliche Spielzeit** setzen. Ist sie
aufgebraucht, zeigt die Startseite statt der Spieleliste einen Ruhe-Bildschirm;
am nächsten Tag öffnet sie sich von selbst. So muss nicht jeden Abend neu
verhandelt werden.

Drei Regeln, die die Sache tragen:

- **Standard ist aus.** Wäre das Limit voreingestellt, wären nach einem Update
  plötzlich alle Kinder ausgesperrt, ohne dass jemand den Grund kennt. Beim
  Einschalten schlägt die App einen Wert nach Altersstufe vor (15 / 20 / 30
  Minuten) — ein Vorschlag, keine Vorgabe.
- **Nie mitten im Spiel.** Geprüft wird nur *zwischen* den Runden. Ein Kind,
  das mitten im Puzzle ausgesperrt wird, hält die App für ungerecht — und
  dieses Gefühl bleibt länger als das Limit.
- **Kein sichtbarer Countdown.** In diesem Alter erzeugt eine ablaufende Uhr
  Druck. Stattdessen sagt der Ergebnisbildschirm einmal „noch ein Spiel“ und
  danach „für heute ist Schluss“.

Gezählt wird die Zeit **im Spiel**, nicht die Zeit im Menü: Quelle ist
`round_history`, dieselbe Tabelle wie beim Elternbericht — zwei getrennte
Zahlen würden sich früher oder später widersprechen. Die Rechnung liegt in
`Engine/Progress/ScreenTimeBudget.cs`, das Limit pro Profil in den
Einstellungen (`screen_time:<Profil-ID>`).

Das Limit ist **nicht** ans Abo gebunden. Eine Schutzfunktion für Kinder
hinter eine Bezahlschranke zu stellen, wäre in einer Kinder-App nicht zu
rechtfertigen.

## Die Stufen

| | Gratis | Abo |
|---|---|---|
| Spiele | 2 (Memory, Blasen platzen) | 14 + alles später Hinzukommende |
| Kinderprofile | 1 | 4 |
| Werbung | **Keine** | **Keine** |
| Offline | Ja | Ja + Inhaltspakete |

Das Abo wird auf einem eigenen Bildschirm verwaltet (`SubscriptionPage`,
Route `subscription`): Zustand, Ende des bezahlten Zeitraums, der Weg in die
Abo-Verwaltung des Stores und das **Beenden**. Beenden schaltet nur die
automatische Verlängerung ab — die Spiele bleiben bis zum Ende des bezahlten
Zeitraums offen (`SubscriptionStatus.Canceled`), und Sterne, Abzeichen und
Profile bleiben in jedem Fall erhalten. Sobald echtes Billing angebunden ist,
kündigt die App nicht selbst: Play und App Store erlauben das nur in ihren
eigenen Abo-Zentren, deshalb führt der Weg dorthin.

## Mehrspielermodus

Drei Modi hinter derselben Schnittstelle `ISessionTransport`:

- **Abwechselnd am selben Gerät (Pass-and-play)** — läuft seit Phase 1. Braucht
  weder Internet noch Konto noch Partnersuche. Vor jeder Runde steht ein
  Übergabebildschirm; ohne diesen Zwischenschritt spielt ein Kind versehentlich
  die Runde seines Geschwisters.
- **Lokales Netz** — Phase 2. Per QR-Code oder Gerätesuche in der Nähe, nur mit
  jemandem, der physisch im selben Raum ist. Keine Fremden aus dem Internet.
- **Von den Eltern bestätigte Familienverbindung** — Phase 3.

Jedes Kind einer Sitzung spielt **in seiner eigenen Altersstufe**: Das kleinere
Geschwister als Spross und das größere als Eiche können sich dieselbe Runde
teilen.

## Plattform und rechtliche Vorgaben

Diese Punkte gehen von Anfang an in die Architektur ein, sie werden nicht
nachträglich angeklebt:

- **Keine Werbung** — auf keiner Stufe. `Entitlements.ShowsAds` ist fest
  `false`; das zu ändern hieße, das Produktversprechen zu ändern, und fällt im
  Test auf.
- **Keine Datenerhebung** — weder Werbe-ID (AAID/IDFA) noch Seriennummer,
  MAC/IMEI oder Standort werden erhoben oder übertragen. Die Profile bleiben nur
  auf dem Gerät; verwendet wird ein Spitzname, nach dem echten Namen wird nicht
  gefragt.
- **Der Spielbericht bleibt auf dem Gerät.** Seit der Bericht existiert, wird
  jede beendete Runde in `round_history` festgehalten (Spiel, Sterne, Dauer,
  Zeitpunkt). Die Tabelle wird nur lokal gelesen, hinter der Elternsperre, und
  verschwindet mit dem Profil (`DeleteProfileAsync`). Sie geht nirgendwohin —
  die Datenschutzerklärung gilt unverändert.
- **Elternsperre** — Kauf, Beenden des Abos, Einstellungen, Profilverwaltung
  und jeder Link, der aus der App hinausführt (Datenschutzerklärung,
  Impressum, Abo-Verwaltung des Stores; die Adressen stehen in
  `Services/PloofyLinks.cs`), liegen hinter `ParentalGateChallenge`.
- **Abo** — hängt am Konto des Stores; die App hat kein eigenes Konto und keinen
  eigenen Server.
- **Altersangabe** — Zielaltersgruppe in der Play Console und die Kids-Kategorie
  im App Store werden vor der Veröffentlichung korrekt angegeben.
- **Datenschutzerklärung** — veröffentlicht. Wie bei den übrigen Apps liegt
  sie in einem **eigenen Repository** und wird von dort über GitHub Pages
  ausgeliefert; Play verlangt dafür eine öffentlich erreichbare URL:

  - <https://alikiratli.github.io/ploofy-web/privacy-policy.html> (tr/en/de auf
    einer Seite) — dieser Link, nicht die Wurzel, gehört in die Play Console
  - <https://alikiratli.github.io/ploofy-web/impressum.html> (de/en)
  - <https://alikiratli.github.io/ploofy-web/> — die Wurzel für das Feld
    „Website“ im Store-Eintrag

  Die Quelle der drei Seiten liegt hier unter `docs/store/`; das Repository
  <https://github.com/alikiratli/ploofy-web> ist nur die Kopie, die Play liest.
  Nach einer Änderung beide Stellen gleichziehen — das Vorgehen steht in dessen
  README.

## Einrichtung

```bash
# MAUI-Workload — erfordert ein als ADMINISTRATOR geöffnetes Terminal
dotnet workload install maui

dotnet restore
dotnet test

# Schneller Versuch unter Windows (Entwicklungsziel, kommt nicht in den Store)
dotnet build src/Ploofy.App/Ploofy.App.csproj -f net10.0-windows10.0.19041.0
```

### Android

Nötig sind das Android SDK und **JDK 17** — ein neueres JDK auf dem System wird
nicht akzeptiert. Da die Pfade maschinenabhängig sind, liegen sie nicht im Repo;
lege im Wurzelverzeichnis eine `Ploofy.local.props` an:

```xml
<Project>
  <PropertyGroup>
    <AndroidSdkDirectory>C:\Users\...\AppData\Local\Android\Sdk</AndroidSdkDirectory>
    <JavaSdkDirectory>C:\Users\...\.jdks\ms-17.0.15</JavaSdkDirectory>
  </PropertyGroup>
</Project>
```

Falls das Android SDK nicht installiert ist:

```bash
dotnet build src/Ploofy.App/Ploofy.App.csproj -t:InstallAndroidDependencies -f net10.0-android -p:AcceptAndroidSDKLicenses=True
```

Auf Gerät oder Emulator installieren und starten:

```bash
dotnet build src/Ploofy.App/Ploofy.App.csproj -f net10.0-android -t:Run
```

### Veröffentlichungssignatur

Ohne konfigurierten Schlüssel wird auch ein Release-Paket mit dem
**Debug-Zertifikat** signiert. Auf dem Gerät reicht das zum Ausprobieren, Play
weist es ab. Der Build warnt in diesem Fall.

Den Schlüssel einmalig erzeugen — er gehört **nicht** ins Repo (`.gitignore`
schließt `*.keystore` und `*.jks` bereits aus) und ein Verlust bedeutet, dass
keine Aktualisierung der App mehr veröffentlicht werden kann:

```bash
keytool -genkeypair -v -keystore ploofy-release.keystore -alias ploofy \
  -keyalg RSA -keysize 2048 -validity 10000
```

Die vier Werte kommen von außen, entweder über `Ploofy.local.props` oder über
gleichnamige Umgebungsvariablen (für CI):

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

Das Bundle für Play erzeugen und die Signatur prüfen:

```bash
dotnet publish src/Ploofy.App/Ploofy.App.csproj -c Release -f net10.0-android -p:AndroidPackageFormat=aab
apksigner verify --print-certs src/Ploofy.App/bin/Release/net10.0-android/io.ploofy.app-Signed.apk
```

Steht dort `CN=Android Debug`, hat der Build den Schlüssel nicht gesehen.

Nach Textänderungen die resx-Dateien neu erzeugen (sie werden nicht von Hand
bearbeitet):

```bash
python tools/build_strings.py content/strings.tsv src/Ploofy.App/Resources/Strings
```

Auch die Töne werden erzeugt — sie liegen fertig im Repo, nur bei einer Änderung
der Klangfarbe neu ausführen:

```bash
python tools/build_sounds.py
```

## Fortschrittsnotiz

Wo zuletzt aufgehört wurde und was als Nächstes ansteht, steht in
`ilerleme notu.docx`; **sie wird am Ende jeder Sitzung aktualisiert.** Ihre
Quelle ist `content/ilerleme-notu.md` — die docx ist erzeugte Ausgabe und wird
nicht von Hand bearbeitet:

```bash
python tools/build_progress_note.py
```

## Fahrplan

- **Phase 1 — Gerüst.** Engine + Datenschicht + Tests ✅ · MAUI-Shell, drei
  Sprachen, Profilablauf, Startbildschirm, Memory von Anfang bis Ende (samt
  Spiel im Wechsel und Sterneerfassung), Elternsperre, Einstellungen,
  Abo-Bildschirm ✅ · Blasen platzen und die gemeinsame Bildsprache ✅ · auf
  einem Android-Tablet von Anfang bis Ende bestätigt ✅ · Tondateien ✅
- **Phase 2 — Vielfalt.** Die restlichen 9 Minispiele, alle mit derselben
  Altersstufen-API. Am Ende steht "10 Spiele, 3 Altersstufen, 1 Sternesammlung".
- **Phase 3 — Eltern und Regelkonformität.** Einstellungen, Abo-Ablauf, Prüfung
  der Datenerhebung, Kopplung im lokalen Netz.
- **Phase 4 — Feinschliff und Veröffentlichung.** App-Symbol und
  Startbildschirm ✅ · Datenschutzerklärung und Impressum veröffentlicht ✅ ·
  Veröffentlichungsschlüssel erzeugt, Release-Paket damit signiert ✅ ·
  Store-Texte, Data-safety- und Altersfreigabe-Antworten in
  `docs/store/listing.md` ✅ · Test auf echtem Gerät, echter Kauf
  (Plugin.InAppBilling), iOS, Theme-Pakete, Store-Grafiken,
  Veröffentlichung ⏳
