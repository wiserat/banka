## Userguide
- **Role uživatelů:**
  - **Klient:** Spravuje své účty, posílá peníze, vidí historii transakcí, maže účty (pokud mají 0 Kč), exportuje transakce.
  - **Bankéř:** Vidí všechny účty, mění limity účtů a status dítěte.
  - **Admin:** Má plný přístup – mění účty i uživatele, resetuje hesla, vidí všechny transakce.
- **Typy účtů:**
  - **Debit:** Běžný účet bez úroků.
  - **Credit:** Účet s možností přečerpání a úrokem na záporný zůstatek.
  - **Saving:** Spořicí účet s úrokem podle průměrného zůstatku.
  - **ChildrenSaving:** Dětský spořicí účet s limity (max 200 Kč na transakci a den).
- **Databáze:** SQLite (`database.db`) ukládá uživatele, účty a transakce.
- **Konfigurace:** Nastavení (úroky, limity) v `config.json`.
- **Bezpečnost:** Hesla jsou hashována pomocí BCrypt.

## Jak otestovat
1. **Spuštění:**
   - Databáze `database.db` se vytvoří automaticky.
   - Vytvoří se výchozí uživatelé:
     - **Admin:** ID `admin`, heslo `admin`.
     - **Bankéř:** ID `bank`, heslo `bank`.

2. **Přihlášení:**
   - **Admin:** Zadej `admin` a heslo `admin`.
   - **Bankéř:** Zadej `bank` a heslo `bank`.
   - **Klient:** Zadej číselné ID (např. `123`). Pokud neexistuje, systém vytvoří nového uživatele.

3. **Testování funkcí:**
   - **Klient:** Vytvoř účet (Debit, Credit, Saving, ChildrenSaving), pošli peníze, zkontroluj historii, exportuj transakce.
   - **Bankéř:** Prohlédni účty, změň limity nebo status dítěte.
   - **Admin:** Uprav účty/uživatele, resetuj hesla, zobraz všechny transakce.

4. **Konfigurace:**
   - Uprav `config.json` (např. úroky nebo limity):
     ```json
     {
       "InterestCalculationIntervalSeconds": 30,
       "SavingsInterestRate": 0.03,
       "CreditInterestRate": 0.12,
       "SpendingLimit": 1000.00
     }
     ```

5. **Převody:**
   - Zadej ID příjemce a částku. ChildrenSaving má limity (max 200 Kč).

6. **Úroky:**
   - Saving účet připisuje úrok podle průměrného zůstatku.
   - Credit účet účtuje úrok na záporný zůstatek.
