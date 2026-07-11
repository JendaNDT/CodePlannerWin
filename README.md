# VoiceCoder Brief

Windows aplikace (.exe), která z volně popsaného nápadu udělá strukturovanou,
verzovanou specifikaci připravenou pro kódovacího agenta.

Demonstrátor projektu **VoiceCoder AI** – první krok podle kap. 18 návrhu
(„malý demonstrátor bez automatického kódování"). Bez AI, plně offline.

## Co umí (v0.1)

- Zápis nápadu textem, nebo diktováním Windows (Win+H)
- 10 řízených otázek seřazených podle dopadu (platforma, offline, osobní údaje, non-goals, akceptace…)
- „Nevím" → viditelně označený **[PŘEDPOKLAD]** místo tichého odhadu
- Živá specifikace se 7 bloky (Cíl a uživatelé, Rozsah, UX, Data, Technika, Akceptace, Rizika)
- Verzování a log každého rozhodnutí
- Export **Markdown** (pro člověka) a **JSON** (stabilní struktura pro agenta)
- Ukládání projektu jako `.vcbrief`

## Novinky ve v0.3

- **Kontrola konzistence** (kap. 7): 9 pravidel hlídá rozpory ve specifikaci – offline vs. cloud,
  osobní údaje vs. „bez osobních údajů", non-goals popsané jako funkce, vágní akceptační kritéria,
  odmítnutý export zmíněný jinde, desktop vs. mobil, moc předpokladů s vysokým dopadem…
- Nálezy se ukazují v barevném pruhu nad náhledem (klik = detail) a jdou i do exportů
  (MD sekce `Kontrola konzistence`, JSON pole `kontrolaKonzistence`)
- Kontrola je orientační – porovnává klíčová slova, nechápe význam

## Novinky ve v0.2

- Formátovaný náhled specifikace (nadpisy, odrážky, oranžově zvýrazněné **[PŘEDPOKLAD]**)
- Barevné stavy otázek (✔ zodpovězeno, ≈ předpoklad, ○ čeká) a štítek dopadu V/S
- Progress bar postupu
- Klávesové zkratky: Ctrl+N/O/S, Ctrl+M (export MD), Ctrl+J (export JSON), Ctrl+Enter (uložit odpověď)
- Hvězdička v titulku při neuložených změnách, roztahovatelný log, ostřejší DPI (PerMonitorV2)

## Stažení a spuštění

Hotové `VoiceCoderBrief.exe` najdeš v [Releases](../../releases). Nic se neinstaluje –
stačí spustit (Windows 10/11, 64-bit). Při prvním spuštění projdi SmartScreen:
**Další informace → Přesto spustit** (aplikace zatím není podepsaná).

## Stavba ze zdrojáků

Vyžaduje .NET 8 SDK (na Windows, Linuxu i macOS):

```bash
cd VoiceCoderBrief
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Testy jádra (běží i mimo Windows):

```bash
cd CoreTests
dotnet run 