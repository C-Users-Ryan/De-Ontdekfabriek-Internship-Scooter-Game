# Toegangscode en herstel (voor begeleiders)

**Waarvoor dit kaartje is:** één taak — de begeleiderscode beheren. Wat de code is, hoe je hem wijzigt of uitzet, en — het belangrijkste — wat je doet als je hem **vergeten** bent. Je hebt geen Unity en geen computer nodig; alles gebeurt op de tablet.

> [!info] Waarom een code
> De code is de tweede muur die kinderen uit het instellingenmenu houdt (de eerste is het hoekgebaar). Een kind stuurt door te kantelen en tikt om te starten — de hoek 2 seconden vasthouden én een code intikken doet het nooit per ongeluk.

---

## Het menu openen

1. **Houd de linkerbovenhoek van het scherm ~2 seconden ingedrukt.** Doe dit tussen de beurten door of op het titelscherm, niet terwijl een kind rijdt. Na 2 seconden verschijnt het codescherm.
2. **Voer de code in op het cijferpaneel.** Klopt de code, dan opent het menu (en staat het spel op pauze). Klopt hij niet, dan trilt het scherm en probeer je opnieuw.

> Test je op een computer zonder aanraakscherm? Druk dan op **F8** om hetzelfde menu te openen.

---

## De code beheren

- **Standaardcode: `3683`.** Op een telefoontoetsenbord spelt dat **D-O-T-F** (De Ontdek-Fabriek) — makkelijk te onthouden, maar geen voor de hand liggende 1-2-3-4. **Wijzig deze code bij een echte locatie.**
- **Code wijzigen:** open het menu → **Beheer → Toegangscode wijzigen** → tik de nieuwe code twee keer in ter bevestiging. Een halve of ongeldige code wordt genegeerd, dus je kunt jezelf hiermee niet buitensluiten.
- **Code uitzetten (vertrouwde locatie):** open het menu → **Beheer → Toegangscode vereist** uit. Dan opent het hoekgebaar het menu meteen, zonder code. Standaard staat de code **aan**.

---

## Code vergeten? (herstel)

Je kunt nooit definitief buitengesloten raken. Er zijn drie manieren om de code terug te zetten naar de standaard `3683`. De eerste is het makkelijkst en werkt gewoon op het codescherm.

![Het codescherm en de twee herstelwegen](Diagram%20-%20facilitator%20PIN%20recovery.svg)

*Het codescherm en de twee wegen terug naar de standaardcode: de zichtbare "Code vergeten?"-knop (twee tikken) of het slotje 4 seconden ingedrukt houden. Terugzetten opent het menu niet — je tikt daarna nog steeds `3683` in.*

1. **Knop "Code vergeten?" (nieuw, aanbevolen).** Onder op het codescherm staat de tekst **"Code vergeten? Tik om terug te zetten."** Tik erop — er verschijnt een bevestiging (*"Zeker? Tik nog eens…"*) — en **tik nog een keer**. De code springt terug naar de standaard. De twee-tik-bevestiging voorkomt dat één misklik de gekozen code van een locatie wist.
2. **Padlock 4 seconden vasthouden (oudere manier).** Houd op het codescherm de PIN-knop bovenaan **4 seconden** ingedrukt; de code gaat dan ook terug naar de standaard. Werkt nog steeds, als tweede weg.
3. **Op een ontwikkelaarsmachine:** *Tools → Kenya Scooter → Facilitator → Reset Access Code* zet de code terug naar de standaard.

> [!warning] Terugzetten is geen ontgrendelen
> Herstellen zet de code alleen terug naar `3683` — het opent het menu **niet**. Daarna moet je nog steeds `3683` intikken. Een kind dat de standaardcode niet kent, komt er dus alsnog niet in. Zeg de standaardcode niet tegen de kinderen. De standaardcode zelf staat nooit op het scherm; jij kent hem uit deze handover.

---

## Even opletten (nog niet op een tablet bevestigd)

- De code, de aan/uit-schakelaar en de nieuwe **"Code vergeten?"**-knop leven allemaal in de broncode (`Settings/FacilitatorLock.cs` en `Settings/SettingsMenu.cs`) in de mirror. Ze zijn **nog niet op een echte tablet getest**: de zichtbare herstelknop is deze sessie toegevoegd en de live-versie moet in Unity nog opnieuw gecompileerd worden voordat je hem op het toestel ziet.
- De code wordt als gewone tekst in PlayerPrefs op de tablet bewaard (een bewuste keuze: de "aanvaller" is een nieuwsgierig kind, en herstel op locatie zónder ontwikkelaar moet altijd kunnen). Behandel de tablet dus als een fysiek beveiligd, vergrendeld kioskapparaat.

---

## Verbindingen

- [[FACILITATOR — Changing the Game]] — de volledige uitleg van álle instellingen in het menu (moeilijkheid, verkeer, geluid, profielen…). Dit kaartje gaat alleen over de code.
- [[Facilitator Quick Reference — Run-of-Show and Troubleshooting]] — het dagelijkse draaiboek naast de tablet.
- [[Handover — What You Can Change vs What Needs a Developer]] — de veilige grens: alles in het menu is van jou, de rest heeft een ontwikkelaar nodig.
- [[HANDOVER — Start Here]] — de voordeur van de hele handover.
- Broncode: `Settings/FacilitatorLock.cs` (standaardcode, aan/uit, terugzetten) en `Settings/SettingsMenu.cs` (codescherm + de drie herstelwegen).
