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
src/Ploofy.Engine/             Spiellogik — null Abhängigkeit zur UI, net9.0
src/Ploofy.Data/               SQLite-Fortschrittsspeicher (sqlite-net), net9.0
src/Ploofy.Ui/                 Gemeinsame MAUI-Oberflächenschicht (Theme, Ton/
                               Haptik, Elternsperre, Sternesteuerung)
src/Ploofy.App/                MAUI-Anwendung (Android + iOS; Windows nur zum
                               schnellen Ausprobieren während der Entwicklung)
content/strings.tsv            Texte aller drei Sprachen — die einzige Quelle
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

**Lernspiele (3):** Buchstabenjagd · Zahlenjagd · Zählen und Zuordnen

Alle zehn sind spielbar.

Sie decken fünf verschiedene Interaktionsarten ab (Tippen, Ziehen, einer Linie
folgen, Gedächtnis, Reihenfolge) — dieses Maß löst das Problem "alles fühlt sich
gleich an" von Anfang an.

**Zeichentechnik:** karten- und kachelbasierte Spiele mit MAUI-Steuerelementen;
alles, was dauernde Bewegung, Partikel oder freies Zeichnen braucht (Blasen
platzen, Finde den Weg, Puzzle, Fang den Korb), mit SkiaSharp.

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

## Die Stufen

| | Gratis | Abo |
|---|---|---|
| Spiele | 2 (Memory, Blasen platzen) | 10 + alles später Hinzukommende |
| Kinderprofile | 1 | 4 |
| Werbung | **Keine** | **Keine** |
| Offline | Ja | Ja + Inhaltspakete |

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
- **Elternsperre** — Kauf, Einstellungen, Profilverwaltung und jeder Link, der
  aus der App hinausführt, liegen hinter `ParentalGateChallenge`.
- **Abo** — hängt am Konto des Stores; die App hat kein eigenes Konto und keinen
  eigenen Server.
- **Altersangabe** — Zielaltersgruppe in der Play Console und die Kids-Kategorie
  im App Store werden vor der Veröffentlichung korrekt angegeben.

## Einrichtung

```bash
# MAUI-Workload — erfordert ein als ADMINISTRATOR geöffnetes Terminal
dotnet workload install maui

dotnet restore
dotnet test

# Schneller Versuch unter Windows (Entwicklungsziel, kommt nicht in den Store)
dotnet build src/Ploofy.App/Ploofy.App.csproj -f net9.0-windows10.0.19041.0
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
dotnet build src/Ploofy.App/Ploofy.App.csproj -t:InstallAndroidDependencies -f net9.0-android -p:AcceptAndroidSDKLicenses=True
```

Auf Gerät oder Emulator installieren und starten:

```bash
dotnet build src/Ploofy.App/Ploofy.App.csproj -f net9.0-android -t:Run
```

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
  Startbildschirm ✅ · Test auf echtem Gerät, echter Kauf
  (Plugin.InAppBilling), iOS, Theme-Pakete, Store-Grafiken, Altersangabe,
  Veröffentlichung ⏳
