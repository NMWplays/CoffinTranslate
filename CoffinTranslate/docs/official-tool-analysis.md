# Analyse: Offizielles Translation Tool (Instructions.pdf, Tool v3.0.x)

Referenzdokument für die Entwicklung von CoffinTranslate. Quelle: `Instructions.pdf`
(identisch mit der Kopie im Spielordner) inkl. aller 17 Screenshots, plus Inspektion
der lokalen Spielinstallation.

## 1. Das Spiel

- **Engine:** RPG Maker MV auf NW.js (belegt durch `Game.exe` + `nw.dll`, `www/`-Struktur,
  `EULA MV.txt`, `CommonEvents.json` / `MapXXX.json`, MV-Standard-Fallback-Fonts).
- **Lokale Installation:** `E:\SteamLibrary\steamapps\common\The Coffin of Andy and Leyley`
- **Relevante Pfade:**
  - `www/languages/` — Installationsort für Übersetzungen (Ordner oder `.cld`-Dateien)
  - `www/languages/tool/` — offizielles Tool: `Translator.exe` (38 MB), `Translator.dat`
    (15 MB, enthält die extrahierbaren Sprachdaten des Spiels), `Instructions.pdf`,
    `User Agreement.pdf`
  - `package.json` (root + www) enthält **keine** Versionsnummer — die Spielversion und der
    Daten-Hash stammen aus `Translator.dat` (werden in der Titelleiste des Tools angezeigt,
    z. B. `[Version 3.0.4 : f0e8318869356136c76e]`). Der Hash signalisiert, ob sich
    Sprachdaten durch Patches geändert haben.

## 2. Funktionsweise des offiziellen Tools

Drei Tabs + Logging-Panel (rechts, mit „Open Log Folder"-Button):

| Tab | Funktion |
|---|---|
| **Create** | Erzeugt neuen Übersetzungs-Projektordner (Felder: Ordnername, Format TXT/CSV, Images PNG/ohne). Ordner entsteht neben dem Tool. |
| **Update** | Migriert ein altes `dialogue.txt`/`.csv` auf neue Spielversion. Optionen: Ziel-Datei, Bild-Handling (Replace All / Replace Missing / Change Nothing), Zielformat (kann TXT↔CSV konvertieren), Backup-Toggle (Timestamp-Ordner). „Check" = Trockenlauf mit Bericht, „Update" = anwenden. Bericht: Zeilen updated/moved/lost/added. |
| **Build** | Optional: kompiliert Projektordner in eine einzelne `.cld`-Datei („Coffin Language Data"), inkl. Bilder + Fonts. **Nicht umkehrbar**, Inhalt für Nutzer unzugänglich/nicht modifizierbar, „natives Format des Spiels", umgeht Encoding-Probleme. |

Das Tool benötigt `Translator.dat` neben sich; ohne die Datei fragt es einmalig nach dem Pfad.

## 3. Projektordner-Struktur (= Format einer Übersetzung)

```
<Sprachname>/
├── dialogue.txt  ODER  dialogue.csv   (Pflicht, UTF-8)
├── font/                              (optional; Custom-Font-Dateien)
└── pictures/, system/, titles1/       (optional; PNG-Ersetzungen für Bilder mit Text,
                                        Ordner-/Dateinamen müssen exakt erhalten bleiben)
```

Alternativ: eine einzelne `<Name>.cld`-Datei.

### 3.1 TXT-Format

Sektionen mit `[HEADER]`-Zeilen. Übersetzt wird der Text rechts vom Doppelpunkt
(eine Leerstelle nach `:`).

> **Layout byte-genau verifiziert (14.07.2026)** an einer echten, mit dem offiziellen Tool
> erzeugten `dialogue.txt` (Deutsch, v3.0.13). Unser Reader+Writer reproduzieren die Datei
> **Zeile für Zeile identisch** (52 289 Zeilen, 0 Diff; nur BOM entfällt). Präzise Regeln:
> - **Kopf:** Datei beginnt mit `[VERSION]` + `ver_hash`-Zeile (z. B. `3.0.13 : a671af9…`),
>   danach `[LANGUAGE]`, `[FONT]`, `[CREDITS]`, `[LABELS]`, `[MENUS]`, `[SPEAKERS]`, `[ITEMS]`,
>   `[DESCRIPTIONS]`, dann je Dialogdatei `[CommonEvents.json]`/`[MapXXX.json]`.
> - **Ausrichtung:** In Scalar-Sektionen wird der Key mit Leerzeichen auf die längste Keylänge
>   der Sektion aufgefüllt, dann ` : ` (Doppelpunkte einer Sektion stehen bündig). SPEAKERS/ITEMS
>   nutzen den Key **mit** `#`-Präfix (9 Zeichen).
> - **Leerwert:** `key : ` und leere Textzeilen `: ` werden **mit** abschließendem Leerzeichen
>   geschrieben (kein Trim).
> - **Leerzeilen:** Scalar-Sektionen sind durch **eine** Leerzeile getrennt; Textsektionen
>   (DESCRIPTIONS + Dialogdateien) tragen eine zusätzliche Leerzeile → nach ihnen stehen **zwei**
>   Leerzeilen bis zum nächsten Header; Einträge innerhalb einer Textsektion sind durch **eine**
>   Leerzeile getrennt. Kein BOM; genau **ein** abschließendes `\n`.
> - **Leerer Sprecher** wird als Annotation `(Narrator)` gedruckt (es gibt keinen Speaker „Narrator").
>
> **`[CHOICES]` (Show-Choices-Menüs):** In den Dialogdateien stehen zwischen den normalen
> Einträgen `[CHOICES]`-Blöcke. Ein Block = **ein** Section-Eintrag mit `name == "[CHOICES]"`; je
> `text`-ID eine **einzeilige** Scalar-Zeile `#<id> : <Option>` (Ausrichtung auf 9 Zeichen wie
> SPEAKERS). Mehrere Optionen desselben Menüs teilen sich einen Header; ein Ein-Options-Menü bekommt
> einen eigenen. In der echten Datei: 295 `[CHOICES]`-Header (= 295× `CHOICE(1)` in der CSV).

```
[VERSION]
3.0.13 : a671af9ea6c1a302da7f   ← Spielversion + Datenhash (Kompatibilitäts-Marker)

[LANGUAGE]
English                       ← Sprachname, wie er im Spiel erscheint

[FONT]
File : GameFont               ← Font-Name (built-in) oder Dateiname (z. B. MyFont.ttf)
Size : 28                     ← Standard-Fontgröße

[CREDITS]
1 : (Name oder leer)          ← 3 Zeilen, kurz halten (Menü ist schmal)
2 :
3 :

[LABELS]                      ← UI-Labels:  Key : Text
Game : The Coffin of Andy and Leyley
Item : Item

[MENUS]                       ← Menüeinträge:  EnglischerText : Übersetzung
New Game : New Game

[SPEAKERS]                    ← Sprechernamen:  #ID : Name
#t1mR4QYN : TV

[ITEMS]                       ← Item-Namen:  #ID : Name
#Q64WLbpK : Axe

[DESCRIPTIONS]                ← Item-Beschreibungen: ID-Zeile, dann `: Text`-Zeile
#Q64WLbpK (Axe)
: CHOPPY CHOP CHOP CHOP!!!!!!!
#tfJBRzjg (Key)
:                             ← leere Beschreibungen sind ok

[CommonEvents.json]           ← Dialog-Sektionen: CommonEvents + je eine pro MapXXX.json
#MT195j9V (TV)
: "Erste Zeile"
: "Folgezeile desselben Eintrags"

[CHOICES]                     ← Show-Choices-Menü innerhalb der laufenden Dialogdatei
#D0xbRbb2 : Linker Arm.       ← je Option eine einzeilige `#id : Text`-Zeile
#RD9G4nvv : Rechter Arm.
```

- **IDs** (`#x86GCl9v`, 8 Zeichen alphanumerisch) müssen exakt erhalten bleiben — darüber
  findet das Spiel die Texte.
- **Neue Zeilen einfügen:** unter einem Eintrag zusätzliche `: Text`-Zeilen ohne Lücken.
- **Duplikate:** identischer Text kommt mehrfach mit verschiedenen IDs vor (Map-Layout);
  jedes Duplikat muss separat übersetzt werden.
- Nachteil TXT: Originaltext wird beim Übersetzen überschrieben (keine Referenz).

### 3.2 CSV-Format

Eine Datei, vertikal gestapelte Blöcke (durch Leerzeilen getrennt), UTF-8.
Leere `Translation`-Zellen ⇒ Fallback auf Englisch. Spaltenlayout laut Screenshots:

| Block | Spalten |
|---|---|
| Info | `Language, Font File, Font Size` (Headerzeile + Wertezeile) |
| Credits | `Credit 1, Credit 2, Credit 3` (Headerzeile + Wertezeile) |
| Labels | `Labels, English, Translation` |
| Menüs | `Menus, Translation` (2 Spalten — Key ist der englische Text) |
| Sprecher | `Speakers, English, Translation` (ID ohne `#`-Präfix) |
| Items | `Items, English, Translation` |
| Beschreibungen | `Descriptions, Item, English, Translation` (4 Spalten) |
| Dialog | Kopfzeile `Section, MapXXX.json`, dann Zeilen `ID, Source, English, Translation`; mehrzeilige Einträge = mehrere Rows mit gleicher ID; neue Zeilen = zusätzliche Rows mit gleicher ID; English-Spalte wird bei Update zurückgesetzt (dient nur als Referenz). **Choices** erscheinen als Zeilen mit der `Source`-Spalte `CHOICE(n)` (n = 1-basierte Optionsnummer); ein neues Menü beginnt bei `CHOICE(1)`. Leerer Sprecher = `Narrator`. (Verifiziert 14.07.2026; CSV-Import → TXT-Export erzeugt strukturidentische, choice-korrekte Dateien.) |

**⚠️ Zu verifizieren an echter Datei, bevor der Editor gebaut wird:** exaktes Quoting/
Escaping (Kommas/Anführungszeichen im Text), Trennzeichen, BOM ja/nein, Bedeutung der
Rows mit leerer English-Zelle in Dialogblöcken (im Screenshot: `wGnMzS21,Narrator,` vor
`wGnMzS21,Narrator,Got 1 Candle!`).

> **Update (13.07.2026): Datenmodell verifiziert** — nicht über eine generierte CSV, sondern
> direkt aus `Translator.dat` (siehe Abschnitt 3.5). Damit sind Quellstruktur, IDs und
> Sektions-Reihenfolge eindeutig bekannt. Das exakte CSV-*Quoting* (Verhalten des offiziellen
> Exports bei Kommas/Quotes/Zeilenumbrüchen) bleibt an einer real generierten Datei zu
> bestätigen; unser Editor schreibt CSV selbst nach RFC 4180 und muss dort nicht bit-genau dem
> Original folgen. Das **TXT-Format** ist unser primäres, verlustarmes Austauschformat.

### 3.3 Formatierungs-Codes (müssen in Übersetzungen erhalten bleiben)

`\fi` kursiv · `\fb` fett · `\fr` Reset (löscht auch Farbe!) · `\{` größer · `\}` kleiner ·
`\c[#]` Farbe

Farbnummern: 1 = Andrew/Andy · 2 = Ashley/Leyley · 3 = Dad · 4 = TV · 5 = ???/Lord Unknown ·
6 = Mom/Lady/Cultist/Julia · 7 = Leader

Typisches Muster: `"\fi Sigh..... \fr\c[1]"` — nach `\fr` muss die Sprecherfarbe
wiederhergestellt werden.

### 3.5 `Translator.dat` — Format des Quelldaten-Bundles (verifiziert)

Reverse-engineered am 13.07.2026 aus der lokalen Installation (reine Format-Interoperabilität,
keine Spielinhalte werden verbreitet). Aufbau:

```
Translator.dat = zlib(  Python-Pickle (Protokoll 4)  )
```

- **Container:** zlib-Stream (Header `78 9C`). Nach dem Entpacken ein Python-Pickle
  (`80 04 95…`, Protokoll 4). Der Pickle ist **reine Daten** (dict/list/str/int/None +
  `builtins.bytearray` für Bilder) — **kein** ausführbarer Code. Ein Reader darf nur die
  Daten-Opcodes interpretieren; `bytearray` wird als Byte-Array whitelisted, jedes andere
  `GLOBAL`/`REDUCE` muss abgelehnt werden (sonst Code-Ausführungs-Risiko).
- **Wurzel:** ein dict mit 13 Schlüsseln:

| Key | Typ | Bedeutung |
|---|---|---|
| `ver_hash` | str | `"3.0.13 : a671af9ea6c1a302da7f"` — Spielversion + Datenhash (Kompatibilitäts-Check) |
| `lng_name` | str | Sprachname (Quelle: `English`) |
| `lng_info` | list[str] (3) | die drei Credit-Zeilen |
| `fnt_face` | str | Font-Name (`GameFont`) |
| `fnt_size` | int | Standard-Fontgröße (`28`) |
| `fnt_data` | bytes\|None | eingebettete Custom-Font-Datei (Quelle: None) |
| `sys_lbls` | dict | System-Labels `Key→Text` (`Game`, `Item`, `File`, `Save`, `Load`) → `[LABELS]` |
| `sys_menu` | dict (25) | Menütexte `Text→Text` → `[MENUS]` |
| `actr_lut` | dict (83) | Sprecher `ID→Name` → `[SPEAKERS]` |
| `item_lut` | dict (188) | Item-Namen `ID→Name` → `[ITEMS]` |
| `text_lut` | dict (15790) | **alle übersetzbaren Texte** `ID→list[str]` (eine Zeile je Listenelement) |
| `img_data` | dict | `"img/<ordner>/<hash>"→PNG-bytes` — übersetzbare Bilder (Referenz) |
| `sections` | dict | Dialog-Layout: `"CommonEvents.json"/"MapXXX.json"→[Eintrag,…]` |

- **`text_lut`** ist der universelle Textspeicher, adressiert per 8-Zeichen-ID. Ein Eintrag ist
  eine **Liste von Strings** (Mehrfachelemente = mehrzeiliger Text). Er enthält sowohl
  Dialogzeilen als auch **Item-Beschreibungen** (`text_lut[itemId]` = Beschreibung, während
  `item_lut[itemId]` = Item-Name). → `[DESCRIPTIONS]` = `item_lut`-IDs, deren `text_lut`-Text.
- **`sections`**: pro Datei eine **geordnete** Liste; jeder Eintrag:
  ```json
  { "name": "<speakerId | ''>", "text": ["<textId>", …] }
  ```
  `name` referenziert `actr_lut` (leer = Erzähler/kein Sprecher, in der TXT als `(Narrator)`;
  Sonderwert `"[CHOICES]"` = Show-Choices-Menü). Jede `textId` referenziert
  `text_lut`. Daraus ergibt sich die TXT-Sektion `[CommonEvents.json]` /`[MapXXX.json]`:
  je `textId` eine Zeile `#<textId> (<Sprechername>)`, gefolgt von `: <Zeile>` je `text_lut`-Element.
  Ein `"[CHOICES]"`-Eintrag wird stattdessen als `[CHOICES]`-Block geschrieben (je `textId` eine
  einzeilige `#<id> : <Option>`-Zeile; der Eintrag = ein Menü).
- **Reihenfolge & Duplikate** stammen 1:1 aus `sections` (erklärt die im Screenshot
  beobachteten wiederholten IDs/Namen). Die leere-English-Zelle-Row = Eintrag, dessen erste
  `text_lut`-Zeile leer ist.
- **Konsequenz:** Aus `Translator.dat` lässt sich der **vollständige Quelltext-Katalog** mit
  IDs, Sprechern, Reihenfolge und Referenzbildern rekonstruieren. CoffinTranslate kann damit
  eine Übersetzung **von Grund auf** erstellen (nicht nur vorhandene Dateien editieren) und
  einen korrekten `dialogue.txt`-Export erzeugen — der Punkt „Create/Update hängt an
  Translator.dat" aus Abschnitt 6 ist damit für den Editor gelöst (nur das Erzeugen von `.cld`
  bleibt proprietär).

### 3.4 Fonts

- `font/`-Ordner im Projekt; Name in `[FONT] File :` muss dem Font-Namen entsprechen.
- Built-in: `GameFont` (Default), `Dotum`, `SimHei`, `Heiti TC`, `sans-serif`, `AppleGothic`.
- Unterstützte Formate: TTF, OTF, SVG, EOT, WOFF, WOFF2.
- Lizenzhinweise des Fonts sollen beigelegt werden.

## 4. Installation einer Übersetzung (der Kern unseres Patchers!)

1. Übersetzungsordner (oder `.cld`) nach `<Spiel>/www/languages/` kopieren.
2. Spiel neu starten → Hauptmenü zeigt neuen Eintrag **„Language"**.
3. Sprache in der Liste auswählen (zeigt Sprachname + Credits) — wird beim **Auswählen**
   angewendet (kein OK nötig), mit Escape zurück.

Mehrere Übersetzungen können parallel installiert sein (je ein Ordner/eine CLD-Datei).

## 5. Schwächen des offiziellen Tools (= unsere Chance)

1. **Kein Editor** — Übersetzen heißt: rohe TXT/CSV in Texteditor/Excel bearbeiten.
   Laut PDF selbst: „primarily a developer tool, very few safeguards and error checking";
   Formatfehler können Text verschlucken oder das Spiel crashen.
2. **Encoding-Hölle:** Excel 2016 exportiert kein sauberes UTF-8, LibreOffice braucht
   manuelle Import-Einstellungen (alles Text-Format), Google Sheets als Workaround.
3. **Kein Installations-Support:** Nutzer müssen selbst per Explorer in den Spielordner
   kopieren; keine Validierung, kein Uninstall, keine Verwaltung.
4. **Keine Übersichtlichkeit:** Dialoge nicht in sinnvoller Reihenfolge, Duplikate müssen
   mehrfach übersetzt werden (keine Translation Memory), kein Fortschritts-Tracking.
5. **Kein Tag-/Konsistenz-Check** für `\c[#]`/`\fi`/… (Layout-Probleme werden nicht erkannt).
6. **Windows-only**, muss im Spielordner liegen (braucht `Translator.dat` daneben).
7. **CLD nicht umkehrbar**, Projektordner muss separat aufbewahrt werden.
8. Update auf neue Major-Version kann Inhalte verlieren (wird nur im Log berichtet).

## 6. Konsequenzen für CoffinTranslate

- **Patcher braucht das offizielle Tool nicht.** Installieren = validieren + kopieren nach
  `www/languages/` (+ Verwaltung, Backup, Uninstall). Funktioniert identisch für Ordner,
  ZIP-Archive (Fan-Übersetzungen!) und `.cld`-Dateien.
- **CLD:** können wir installieren/verwalten (Datei kopieren), aber nicht erzeugen oder
  öffnen (proprietär, bewusst undokumentiert). Erzeugen bleibt beim offiziellen Tool.
- **Projekt-Generierung (Create/Update)** hängt an `Translator.dat` → bleibt beim
  offiziellen Tool. Unser Editor **importiert** stattdessen vorhandene `dialogue.txt/.csv`
  und schreibt sie garantiert korrekt (UTF-8, Struktur, IDs) zurück — damit entfällt
  Excel/Calc komplett.
- **Kompatibilitäts-Indikator:** Hash über `Translator.dat` berechnen und mit dem Stand
  einer Übersetzung vergleichen (Heuristik für „Spiel wurde gepatcht, Übersetzung evtl.
  veraltet").
- **Linux:** Spiel läuft via Proton; Dateistruktur in der Steam-Library ist identisch →
  gleicher Kopiervorgang, nur andere Steam-Pfad-Erkennung.
