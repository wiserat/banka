# Bankovní systém v C#: Softwarové požadavky
* **SSPŠ**
* **Verze 1**
* **Jonáš Tenora**
* **17.2.2025**

## Obsah
1. Historie Dokumentu
2. Úvod
3. Funkční požadavky
4. Nefunkční požadavky
5. Specifikace výpočtu úroků

---

## Historie Dokumentu
### Verze 1
* **Autor:** Jonáš Tenora
* **Komentář:** První verze dokumentu

---

## Úvod
* **Účel dokumentu:**  
  Dokument popisuje všechny požadavky na vývoj bankovního systému v jazyce C#.
  
* **Cílová skupina:**  
  Klienti, bankéři a administrátoři banky
  
* **Kontakt:**  
  tenora.jo.2022@skola.ssps.cz
  
* **Odkazy na další dokumenty:**  
  Technická specifikace, implementační dokumentace
  
* **Strany:**  
  1. **Provozovatel:** Banka  
  2. **Uživatel:** Klient / Bankéř / Administrátor

---

## Funkční požadavky

1. **Správa účtů**  
   - Uživatel musí mít možnost vytvořit a spravovat různé typy účtů:
     - **Běžný účet:** Umožňuje provádět transakce (vklady, výběry, převody) a je často spojen se spořicím účtem.
     - **Spořicí účet:** Slouží pro ukládání peněz s roční úrokovou sazbou a připisováním úroků na konci měsíce.
     - **Studentský spořicí účet:** Varianta spořicího účtu s omezením jednorázového a denního výběru.
     - **Úvěrový účet:** Umožňuje čerpat prostředky do stanoveného úvěrového rámce, s úroky přičítanými k dluhu.
   - **Priorita:** Vysoká

2. **Transakce a logování**  
   - Uživatel musí mít možnost provádět transakce, jako jsou vklady, výběry a převody mezi účty, přičemž transakce budou provedeny pouze v případě dostatečného zůstatku.
   - Systém musí logovat všechny operace na účtech, včetně:
     - Vkladů
     - Výběrů
     - Připisování úroků
     - Splátek úvěru
   - Uživatel bude mít možnost volby způsobu ukládání logů (např. do souboru nebo databáze).
   - **Priorita:** Vysoká

3. **Přihlašovací a registrační mechanismus**  
   - Přihlášení bude probíhat pomocí rodného čísla (unikátní identifikátor) a hesla.
   - Pokud uživatel není nalezen v databázi, bude vyzván k registraci, při které se automaticky vytvoří počáteční debitní účet.
   - **Priorita:** Vysoká

4. **Správa rolí a oprávnění**  
   - Systém musí rozlišovat přístupy a oprávnění pro následující skupiny uživatelů:
     - **Klienti:** Přístup pouze ke svým účtům a transakcím.
     - **Bankéři:** Přístup ke všem účtům a celkový přehled o vkladech a úrocích.
     - **Administrátoři:** Správa uživatelských účtů a jejich oprávnění.
   - **Priorita:** Střední

5. **Práce s časem**  
   - Některé funkce systému (např. připisování úroků, úvěrové splátky) jsou závislé na čase.
   - Systém musí umožnit simulaci přechodu do budoucnosti za účelem testování těchto funkcí.
   - **Priorita:** Střední

6. **Výpočet úroků**  
   - **Spořicí účet:**  
     - Úrok se počítá jako:  
       ```
       Úrok = (vážený průměr zůstatku * roční úroková sazba) / 12
       ```
     - Každý den, kdy se zůstatek změní, se započítá s odpovídající váhou (podle počtu dní, kdy daný zůstatek platil).
     - Výsledek se zaokrouhluje dle pravidel banky.
   - **Úvěrový účet:**  
     - Úroky se připisují k dluhu na konci měsíce, pokud není dluh v bezúročném období.
     - Výpočet probíhá obdobně jako u spořicího účtu, avšak s negativním znaménkem.
   - **Priorita:** Střední

---

## Nefunkční požadavky

* **Vývojové prostředí:**  
  - Aplikace bude vyvíjena v jazyce **C#** s využitím .NET Frameworku.  
  - Preferovaná vývojová prostředí: Visual Studio Code.

* **Uživatelské rozhraní:**  
  - Aplikace bude poskytovat rozhraní pro:
    - **CLI (příkazový řádek)**

* **Databáze:**  
  - Databázové úložiště bude využívat **SQLite databázi**.

* **Bezpečnost:**  
  - Citlivá data (např. hesla) budou ukládána bezpečně pomocí **hashovacích algoritmů** (např. BCrypt).
  
* **Modularita a rozšiřitelnost:**  
  - Systém musí být navržen modulárně, aby bylo možné snadné přidání nových typů účtů a funkcí v budoucnu.

* **Logování:**  
  - Logování operací bude konfigurovatelné, s možností volby mezi ukládáním do souboru nebo databáze.

---

## Specifikace výpočtu úroků

* **Spořicí účet:**  
  - **Vzorec:**  
    ```
    Úrok = (vážený průměr zůstatku * roční úroková sazba) / 12
    ```
  - **Postup výpočtu:**  
    - Každý den, kdy se zůstatek mění, se započítá s odpovídající váhou podle počtu dní, kdy daný zůstatek platil.
    - Pro zjednodušení se všechny měsíce počítají jako 30 dní.
  
  - **Příklad:**  
    - **1. den:** zůstatek 10 000 Kč (platí 10 dní)  
    - **11. den:** vklad 5 000 Kč → nový zůstatek 15 000 Kč (platí 15 dní)  
    - **26. den:** výběr 3 000 Kč → nový zůstatek 12 000 Kč (platí 5 dní)  
    - **Výpočet:**  
      ```
      Vážený průměr = ((10 000 × 10) + (15 000 × 15) + (12 000 × 5)) / 30 
                     = (100 000 + 225 000 + 60 000) / 30 
                     = 385 000 / 30 
                     ≈ 12 833,33 Kč
      Úrok = 12 833,33 × (3% p.a.) × (1/12) ≈ 32,08 Kč
      ```
    - Zaokrouhlování se provádí dle pravidel banky.
  
* **Úvěrový účet:**  
  - Úroky se připisují k dluhu na konci měsíce, pokud dluh není v bezúročném období.
  - Výpočet probíhá obdobně jako u spořicího účtu, ale s negativním znaménkem.
