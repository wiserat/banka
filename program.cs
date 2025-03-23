using System;
using Microsoft.Data.Sqlite;
using BCrypt.Net;
using System.Collections.Generic;
using System.Timers;
using System.Text.Json;
using System.IO;
using System.Linq;

// typy rozhrani do kterych se muze uzivatel prihlasit
public enum UserRole
{
    Client,
    Banker,
    Admin
}

// drzi info o uzivateli a jeho roli
class User
{
    // v praxi napr. rodne cislo
    public int UserId { get; }
    public UserRole Role { get; }
    public string Name { get; }
    public bool Children { get; }

    public User(int userId, string role, string name, bool children)
    {
        UserId = userId;
        Role = role.ToLower() switch
        {
            "admin" => UserRole.Admin,
            "banker" => UserRole.Banker,
            _ => UserRole.Client
        };
        Name = name;
        Children = children;
    }

    // kontrola opravneni:
    // - admin muze vse
    // - banker ma omezena prava
    // - klient ma zakladni prava
    public bool CanModifyLimits => Role == UserRole.Admin || Role == UserRole.Banker;
    public bool CanModifyChildStatus => Role == UserRole.Admin;
    public bool CanModifyEverything => Role == UserRole.Admin;
}

// dashboard pro bankere
class BankerDashboard
{
    private readonly User _banker;

    public BankerDashboard(User banker)
    {
        _banker = banker;
    }

    // ukaze dashboard a moznosti co muze banker delat
    public void Show()
    {
        while (true)
        {
            Console.WriteLine("\nBanker Dashboard");
            Console.WriteLine("1. View all accounts");
            Console.WriteLine("2. Modify account limits");
            Console.WriteLine("3. Modify child status");
            Console.WriteLine("4. Exit");

            Console.Write("Choose an option: ");
            string option = Console.ReadLine();

            switch (option)
            {
                case "1":
                    ViewAllAccounts();
                    break;
                case "2":
                    ModifyAccountLimits();
                    break;
                case "3":
                    ModifyChildStatus();
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
    }

    // vypise vsechny ucty
    private void ViewAllAccounts()
    {
        var accounts = GetAllAccounts();
        foreach (var account in accounts)
        {
            Console.WriteLine($"Account ID: {account.Id}, User ID: {GetUserIdForAccount(account.Id)}, " +
                            $"Type: {account.GetType().Name}, Balance: {account.Balance:C}, " +
                            $"Spending Limit: {(account.SpendingLimit.HasValue ? account.SpendingLimit.Value.ToString("C") : "None")}");
        }
    }

    // upravi spending/transfer/withdraw limit uctu
    private void ModifyAccountLimits()
    {
        Console.Write("Enter account ID: ");
        if (!int.TryParse(Console.ReadLine(), out int accountId))
        {
            Console.WriteLine("Invalid account ID.");
            return;
        }

        var account = GetAccountById(accountId);
        if (account == null)
        {
            Console.WriteLine("Account not found.");
            return;
        }

        Console.Write("Enter new spending limit (or leave empty for no limit): ");
        string input = Console.ReadLine();
        // pokud je prazdne, tak limit neni, pote pokud je zadane cislo ok, tak se zapise novy limit
        decimal? newLimit = string.IsNullOrWhiteSpace(input) ? null :
            decimal.TryParse(input, out decimal limit) ? limit : null;

        DbHelper.ExecuteNonQuery(
            "UPDATE Accounts SET SpendingLimit = @limit WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@limit"] = newLimit as object ?? DBNull.Value,
                ["@id"] = accountId
            });

        Console.WriteLine("Spending limit updated successfully.");
    }

    // upravi status, jestli je ucet ditete
    private void ModifyChildStatus()
    {
        Console.Write("Enter user ID: ");
        if (!int.TryParse(Console.ReadLine(), out int userId))
        {
            Console.WriteLine("Invalid user ID.");
            return;
        }

        var user = GetUserById(userId);
        if (user == null)
        {
            Console.WriteLine("User not found.");
            return;
        }

        Console.Write("Set as child account? (y/n): ");
        bool isChild = Console.ReadLine()?.ToLower() == "y";

        DbHelper.ExecuteNonQuery(
            "UPDATE Users SET Children = @children WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@children"] = isChild,
                ["@id"] = userId
            });

        Console.WriteLine("Child status updated successfully.");
    }

    // vrati vsechny ucty
    private List<Account> GetAllAccounts()
    {
        return DbHelper.ExecuteReader<Account>(
            "SELECT Id, Balance, Type, SpendingLimit FROM Accounts",
            reader =>
            {
                string type = reader.GetString(2);
                Account account = type switch
                {
                    "Debit" => new DebitAccount(),
                    "Credit" => new CreditAccount(),
                    "Saving" => new SavingAccount(),
                    "ChildrenSaving" => new ChildrenSavingAccount(),
                    _ => new DebitAccount()
                };
                account.Id = reader.GetInt32(0);
                account.Balance = reader.GetDecimal(1);
                account.SpendingLimit = !reader.IsDBNull(3) ? reader.GetDecimal(3) : null;
                return account;
            });
    }

    // vrati id uzivatele/rodne cislo majitele uctu
    private int GetUserIdForAccount(int accountId)
    {
        return DbHelper.ExecuteScalar<int>(
            "SELECT UserId FROM Accounts WHERE Id = @id",
            new Dictionary<string, object> { ["@id"] = accountId });
    }

    // vrati jeden ucet podle id
    private Account GetAccountById(int accountId)
    {
        var accounts = DbHelper.ExecuteReader<Account>(
            "SELECT Id, Balance, Type, SpendingLimit FROM Accounts WHERE Id = @id",
            reader =>
            {
                string type = reader.GetString(2);
                Account account = type switch
                {
                    "Debit" => new DebitAccount(),
                    "Credit" => new CreditAccount(),
                    "Saving" => new SavingAccount(),
                    "ChildrenSaving" => new ChildrenSavingAccount(),
                    _ => new DebitAccount()
                };
                account.Id = reader.GetInt32(0);
                account.Balance = reader.GetDecimal(1);
                account.SpendingLimit = !reader.IsDBNull(3) ? reader.GetDecimal(3) : null;
                return account;
            },
            new Dictionary<string, object> { ["@id"] = accountId });

        return accounts.FirstOrDefault();
    }

    // vrati jednoho uzivatele podle jeho user id/rodneho cisla
    private User GetUserById(int userId)
    {
        var users = DbHelper.ExecuteReader<User>(
            "SELECT Id, Role, Name, Children FROM Users WHERE Id = @id",
            reader => new User(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3)
            ),
            new Dictionary<string, object> { ["@id"] = userId });

        return users.FirstOrDefault();
    }
}

// dashboard pro admina
class AdminDashboard
{
    private readonly User _admin;

    public AdminDashboard(User admin)
    {
        _admin = admin;
    }

    public void Show()
    {
        while (true)
        {
            Console.WriteLine("\nAdmin Dashboard");
            Console.WriteLine("1. View all accounts");
            Console.WriteLine("2. Modify account");
            Console.WriteLine("3. View all users");
            Console.WriteLine("4. Modify user");
            Console.WriteLine("5. View all transactions");
            Console.WriteLine("6. Exit");

            Console.Write("Choose an option: ");
            string option = Console.ReadLine();

            switch (option)
            {
                case "1":
                    ViewAllAccounts();
                    break;
                case "2":
                    ModifyAccount();
                    break;
                case "3":
                    ViewAllUsers();
                    break;
                case "4":
                    ModifyUser();
                    break;
                case "5":
                    ViewAllTransactions();
                    break;
                case "6":
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
    }

    private void ViewAllAccounts()
    {
        var accounts = GetAllAccounts();
        foreach (var account in accounts)
        {
            Console.WriteLine($"Account ID: {account.Id}, User ID: {GetUserIdForAccount(account.Id)}, " +
                            $"Type: {account.GetType().Name}, Balance: {account.Balance:C}, " +
                            $"Spending Limit: {(account.SpendingLimit.HasValue ? account.SpendingLimit.Value.ToString("C") : "None")}");
        }
    }

    // po zadani id uctu se da upravit jakakoli informace o uctu
    private void ModifyAccount()
    {
        Console.Write("Enter account ID: ");
        if (!int.TryParse(Console.ReadLine(), out int accountId))
        {
            Console.WriteLine("Invalid account ID.");
            return;
        }

        var account = GetAccountById(accountId);
        if (account == null)
        {
            Console.WriteLine("Account not found.");
            return;
        }

        Console.WriteLine("1. Modify balance");
        Console.WriteLine("2. Modify type");
        Console.WriteLine("3. Modify spending limit");
        Console.WriteLine("4. Cancel");

        Console.Write("Choose an option: ");
        string option = Console.ReadLine();

        switch (option)
        {
            case "1":
                ModifyBalance(accountId);
                break;
            case "2":
                ModifyType(accountId);
                break;
            case "3":
                ModifySpendingLimit(accountId);
                break;
            default:
                Console.WriteLine("Operation cancelled.");
                break;
        }
    }

    private void ModifyBalance(int accountId)
    {
        Console.Write("Enter new balance: ");
        if (!decimal.TryParse(Console.ReadLine(), out decimal newBalance))
        {
            Console.WriteLine("Invalid balance amount.");
            return;
        }

        DbHelper.ExecuteNonQuery(
            "UPDATE Accounts SET Balance = @balance WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@balance"] = newBalance,
                ["@id"] = accountId
            });

        Console.WriteLine("Balance updated successfully.");
    }

    private void ModifyType(int accountId)
    {
        Console.WriteLine("Available types:");
        Console.WriteLine("1. Debit");
        Console.WriteLine("2. Credit");
        Console.WriteLine("3. Saving");
        Console.WriteLine("4. ChildrenSaving");

        Console.Write("Choose new type: ");
        string input = Console.ReadLine();
        string newType = input switch
        {
            "1" => "Debit",
            "2" => "Credit",
            "3" => "Saving",
            "4" => "ChildrenSaving",
            _ => null
        };

        if (newType == null)
        {
            Console.WriteLine("Invalid type selected.");
            return;
        }

        DbHelper.ExecuteNonQuery(
            "UPDATE Accounts SET Type = @type WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@type"] = newType,
                ["@id"] = accountId
            });

        Console.WriteLine("Account type updated successfully.");
    }

    private void ModifySpendingLimit(int accountId)
    {
        Console.Write("Enter new spending limit (or leave empty for no limit): ");
        string input = Console.ReadLine();
        decimal? newLimit = string.IsNullOrWhiteSpace(input) ? null :
            decimal.TryParse(input, out decimal limit) ? limit : null;

        DbHelper.ExecuteNonQuery(
            "UPDATE Accounts SET SpendingLimit = @limit WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@limit"] = newLimit as object ?? DBNull.Value,
                ["@id"] = accountId
            });

        Console.WriteLine("Spending limit updated successfully.");
    }

    private void ViewAllUsers()
    {
        var users = GetAllUsers();
        foreach (var user in users)
        {
            Console.WriteLine($"User ID: {user.UserId}, Role: {user.Role}, " +
                            $"Name: {user.Name}, Is Child: {user.Children}");
        }
    }

    private void ModifyUser()
    {
        Console.Write("Enter user ID: ");
        if (!int.TryParse(Console.ReadLine(), out int userId))
        {
            Console.WriteLine("Invalid user ID.");
            return;
        }

        var user = GetUserById(userId);
        if (user == null)
        {
            Console.WriteLine("User not found.");
            return;
        }

        Console.WriteLine("1. Modify role");
        Console.WriteLine("2. Modify name");
        Console.WriteLine("3. Modify child status");
        Console.WriteLine("4. Reset password");
        Console.WriteLine("5. Cancel");

        Console.Write("Choose an option: ");
        string option = Console.ReadLine();

        switch (option)
        {
            case "1":
                ModifyUserRole(userId);
                break;
            case "2":
                ModifyUserName(userId);
                break;
            case "3":
                ModifyUserChildStatus(userId);
                break;
            case "4":
                ResetUserPassword(userId);
                break;
            default:
                Console.WriteLine("Operation cancelled.");
                break;
        }
    }

    private void ModifyUserRole(int userId)
    {
        Console.WriteLine("Available roles:");
        Console.WriteLine("1. Client");
        Console.WriteLine("2. Banker");
        Console.WriteLine("3. Admin");

        Console.Write("Choose new role: ");
        string input = Console.ReadLine();
        string newRole = input switch
        {
            "1" => "client",
            "2" => "banker",
            "3" => "admin",
            _ => null
        };

        if (newRole == null)
        {
            Console.WriteLine("Invalid role selected.");
            return;
        }

        DbHelper.ExecuteNonQuery(
            "UPDATE Users SET Role = @role WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@role"] = newRole,
                ["@id"] = userId
            });

        Console.WriteLine("User role updated successfully.");
    }

    private void ModifyUserName(int userId)
    {
        Console.Write("Enter new name: ");
        string newName = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(newName))
        {
            Console.WriteLine("Invalid name.");
            return;
        }

        DbHelper.ExecuteNonQuery(
            "UPDATE Users SET Name = @name WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@name"] = newName,
                ["@id"] = userId
            });

        Console.WriteLine("User name updated successfully.");
    }

    private void ModifyUserChildStatus(int userId)
    {
        Console.Write("Set as child account? (y/n): ");
        bool isChild = Console.ReadLine()?.ToLower() == "y";

        DbHelper.ExecuteNonQuery(
            "UPDATE Users SET Children = @children WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@children"] = isChild,
                ["@id"] = userId
            });

        Console.WriteLine("Child status updated successfully.");
    }

    private void ResetUserPassword(int userId)
    {
        Console.Write("Enter new password: ");
        string newPassword = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            Console.WriteLine("Invalid password.");
            return;
        }

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        DbHelper.ExecuteNonQuery(
            "UPDATE Users SET Password = @password WHERE Id = @id",
            new Dictionary<string, object>
            {
                ["@password"] = hashedPassword,
                ["@id"] = userId
            });

        Console.WriteLine("Password reset successfully.");
    }

    private void ViewAllTransactions()
    {
        var transactions = GetAllTransactions();
        foreach (var transaction in transactions)
        {
            Console.WriteLine($"Transaction ID: {transaction.Id}, " +
                            $"From Account: {transaction.FromAccountId}, " +
                            $"To Account: {transaction.ToAccountId}, " +
                            $"Amount: {transaction.Amount:C}, " +
                            $"Timestamp: {transaction.Timestamp}");
        }
    }

    private List<Account> GetAllAccounts()
    {
        return DbHelper.ExecuteReader<Account>(
            "SELECT Id, Balance, Type, SpendingLimit FROM Accounts",
            reader =>
            {
                string type = reader.GetString(2);
                Account account = type switch
                {
                    "Debit" => new DebitAccount(),
                    "Credit" => new CreditAccount(),
                    "Saving" => new SavingAccount(),
                    "ChildrenSaving" => new ChildrenSavingAccount(),
                    _ => new DebitAccount()
                };
                account.Id = reader.GetInt32(0);
                account.Balance = reader.GetDecimal(1);
                account.SpendingLimit = !reader.IsDBNull(3) ? reader.GetDecimal(3) : null;
                return account;
            });
    }

    private List<User> GetAllUsers()
    {
        return DbHelper.ExecuteReader<User>(
            "SELECT Id, Role, Name, Children FROM Users",
            reader => new User(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3)
            ));
    }

    private List<Transaction> GetAllTransactions()
    {
        return DbHelper.ExecuteReader<Transaction>(
            "SELECT Id, FromAccountId, ToAccountId, Amount, Timestamp FROM Transactions",
            reader => new Transaction
            {
                Id = reader.GetInt32(0),
                FromAccountId = reader.GetInt32(1),
                ToAccountId = reader.GetInt32(2),
                Amount = reader.GetDecimal(3),
                Timestamp = reader.GetDateTime(4)
            });
    }

    private int GetUserIdForAccount(int accountId)
    {
        return DbHelper.ExecuteScalar<int>(
            "SELECT UserId FROM Accounts WHERE Id = @id",
            new Dictionary<string, object> { ["@id"] = accountId });
    }

    private Account GetAccountById(int accountId)
    {
        var accounts = DbHelper.ExecuteReader<Account>(
            "SELECT Id, Balance, Type, SpendingLimit FROM Accounts WHERE Id = @id",
            reader =>
            {
                string type = reader.GetString(2);
                Account account = type switch
                {
                    "Debit" => new DebitAccount(),
                    "Credit" => new CreditAccount(),
                    "Saving" => new SavingAccount(),
                    "ChildrenSaving" => new ChildrenSavingAccount(),
                    _ => new DebitAccount()
                };
                account.Id = reader.GetInt32(0);
                account.Balance = reader.GetDecimal(1);
                account.SpendingLimit = !reader.IsDBNull(3) ? reader.GetDecimal(3) : null;
                return account;
            },
            new Dictionary<string, object> { ["@id"] = accountId });

        return accounts.FirstOrDefault();
    }

    private User GetUserById(int userId)
    {
        var users = DbHelper.ExecuteReader<User>(
            "SELECT Id, Role, Name, Children FROM Users WHERE Id = @id",
            reader => new User(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3)
            ),
            new Dictionary<string, object> { ["@id"] = userId });

        return users.FirstOrDefault();
    }
}

// pomocna trida pro praci s databazi
public static class DbHelper
{
    private const string ConnectionString = "Data Source=database.db;";
    private static readonly object _lock = new object();

    public static SqliteConnection GetConnection()
    {
        try
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            return connection;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to open database connection: {ex.Message}", ex);
        }
    }

    // provadi SQL dotaz a vraci jednu hodnotu, prevadi SQLite typy na C# typy
    public static T ExecuteScalar<T>(string query, Dictionary<string, object> parameters = null)
    {
        lock (_lock)
        {
            using var connection = GetConnection();
            using var cmd = CreateCommand(connection, query, parameters);
            try
            {
                var result = cmd.ExecuteScalar();
                return ConvertScalarResult<T>(result);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute scalar query: {ex.Message}", ex);
            }
        }
    }

    public static void ExecuteNonQuery(string query, Dictionary<string, object> parameters = null)
    {
        lock (_lock)
        {
            using var connection = GetConnection();
            using var cmd = CreateCommand(connection, query, parameters);
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute non-query: {ex.Message}", ex);
            }
        }
    }

    // provadi SQL dotaz a vraci seznam objektu pomoci mapovaci funkce
    // mapper: funkce ktera prevadi radek z databaze na objekt typu T
    public static List<T> ExecuteReader<T>(string query, Func<SqliteDataReader, T> mapper,
        Dictionary<string, object> parameters = null)
    {
        lock (_lock)
        {
            using var connection = GetConnection();
            using var cmd = CreateCommand(connection, query, parameters);
            try
            {
                using var reader = cmd.ExecuteReader();
                var results = new List<T>();
                while (reader.Read())
                {
                    results.Add(mapper(reader));
                }
                return results;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute reader: {ex.Message}", ex);
            }
        }
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, string query, Dictionary<string, object> parameters)
    {
        var cmd = new SqliteCommand(query, connection);
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
        }
        return cmd;
    }

    // prevod vysledku z SQLite na C# typy
    private static T ConvertScalarResult<T>(object result)
    {
        if (result == null || result == DBNull.Value)
            return default;

        if (typeof(T) == typeof(decimal) && result is double doubleValue)
            return (T)(object)Convert.ToDecimal(doubleValue);

        if (typeof(T) == typeof(int) && result is long longValue)
            return (T)(object)Convert.ToInt32(longValue);

        return (T)result;
    }
}

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Data Source=database.db;";

        using (SqliteConnection connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // vytvoreni tabulek pokud neexistuji
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY,
                    Role TEXT NOT NULL CHECK (Role IN ('admin', 'banker', 'client')),
                    Name TEXT NOT NULL,
                    Password TEXT NOT NULL,
                    Children BOOLEAN NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS Accounts (
                    Id INTEGER PRIMARY KEY,
                    UserId INTEGER NOT NULL,
                    Balance REAL NOT NULL DEFAULT 1000,
                    Type TEXT NOT NULL CHECK (Type IN ('Debit', 'Credit', 'Saving', 'ChildrenSaving')),
                    Children BOOLEAN NOT NULL DEFAULT 0,
                    SpendingLimit REAL,
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Transactions (
                    Id INTEGER PRIMARY KEY,
                    FromAccountId INTEGER NOT NULL,
                    ToAccountId INTEGER NOT NULL,
                    Amount REAL NOT NULL CHECK (Amount > 0),
                    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (FromAccountId) REFERENCES Accounts(Id),
                    FOREIGN KEY (ToAccountId) REFERENCES Accounts(Id)
                );

                -- Create indexes for better performance
                CREATE INDEX IF NOT EXISTS idx_accounts_userid ON Accounts(UserId);
                CREATE INDEX IF NOT EXISTS idx_transactions_fromaccount ON Transactions(FromAccountId);
                CREATE INDEX IF NOT EXISTS idx_transactions_toaccount ON Transactions(ToAccountId);";

            using (SqliteCommand cmd = new SqliteCommand(createTableQuery, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // vytvori admina, pokud neexistuje
            string checkAdminQuery = "SELECT COUNT(*) FROM Users WHERE Role = 'admin';";
            using (SqliteCommand cmd = new SqliteCommand(checkAdminQuery, connection))
            {
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                {
                    string insertAdminQuery = "INSERT INTO Users (Role, Name, Password, Children) VALUES ('admin', 'Administrator', @password, 0);";
                    using (SqliteCommand insertCmd = new SqliteCommand(insertAdminQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@password", BCrypt.Net.BCrypt.HashPassword("admin"));
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }

            // vytvori bankare, pokud neexistuje
            string checkBankerQuery = "SELECT COUNT(*) FROM Users WHERE Role = 'banker';";
            using (SqliteCommand cmd = new SqliteCommand(checkBankerQuery, connection))
            {
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                {
                    string insertBankerQuery = "INSERT INTO Users (Role, Name, Password, Children) VALUES ('banker', 'Bank Manager', @password, 0);";
                    using (SqliteCommand insertCmd = new SqliteCommand(insertBankerQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@password", BCrypt.Net.BCrypt.HashPassword("bank"));
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }

            // hlavni prihlasovaci loop
            while (true)
            {
                Console.Write("Enter User ID (or 'admin'/'bank' for special access): ");
                string userIdInput = Console.ReadLine();

                if (userIdInput.ToLower() == "admin")
                {
                    Console.Write("Password: ");
                    string password = Console.ReadLine();

                    if (password == "admin")
                    {
                        Console.WriteLine("Admin login successful!");
                        var adminUser = new User(0, "admin", "Administrator", false);
                        new AdminDashboard(adminUser).Show();
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Invalid admin password!");
                    }
                }
                else if (userIdInput.ToLower() == "bank")
                {
                    Console.Write("Password: ");
                    string password = Console.ReadLine();

                    if (password == "bank")
                    {
                        Console.WriteLine("Banker login successful!");
                        var bankerUser = new User(0, "banker", "Bank Manager", false);
                        new BankerDashboard(bankerUser).Show();
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Invalid banker password!");
                    }
                }
                else if (int.TryParse(userIdInput, out int userId))
                {
                    if (UserExists(connection, userId))
                    {
                        Console.Write("Password: ");
                        string password = Console.ReadLine();

                        string query = "SELECT Password FROM Users WHERE Id = @id;";
                        using (SqliteCommand cmd = new SqliteCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@id", userId);
                            object result = cmd.ExecuteScalar();

                            if (result != null && BCrypt.Net.BCrypt.Verify(password, result.ToString()))
                            {
                                Console.WriteLine("Login successful!");
                                User user = GetUser(connection, userId.ToString());
                                Dashboard(user);
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Invalid password!");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("User does not exist. Creating new account...");
                        RegisterUser(connection, userId);
                        User user = GetUser(connection, userId.ToString());
                        Dashboard(user);
                        break;
                    }
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter a numeric ID or 'admin'/'bank'.");
                }
            }
        }
    }

    // dashboard pro klienta
    static void Dashboard(User user)
    {
        Console.WriteLine($"Welcome, {user.Name}. BankId: {user.UserId}");
        List<Account> accounts = GetUserAccounts(user.UserId);

        if (accounts.Count == 0)
        {
            StartNewAccount(user.UserId);
            accounts = GetUserAccounts(user.UserId);
        }

        while (true)
        {
            Console.WriteLine("Choose an account:");
            for (int i = 0; i < accounts.Count; i++)
            {
                Console.WriteLine($"{i + 1}. Account ID: {accounts[i].Id}, Balance: {accounts[i].Balance}, Type: {accounts[i].GetType().Name}");
            }
            Console.WriteLine($"{accounts.Count + 1}. Start a new account");
            Console.WriteLine($"{accounts.Count + 2}. Exit");

            Console.Write("Choose an option: ");
            if (!int.TryParse(Console.ReadLine(), out int option) || option < 1 || option > accounts.Count + 2)
            {
                Console.WriteLine("Invalid option. Please try again.");
                continue;
            }

            if (option == accounts.Count + 1)
            {
                StartNewAccount(user.UserId);
            }
            else if (option == accounts.Count + 2)
            {
                return;
            }
            else
            {
                Account selectedAccount = accounts[option - 1];
                ManageAccount(selectedAccount, accounts);
            }
            accounts = GetUserAccounts(user.UserId);
        }
    }

    // sprava konkretniho uctu
    static void ManageAccount(Account account, List<Account> accounts)
    {
        while (true)
        {
            Console.WriteLine($"Managing Account ID: {account.Id}, Balance: {account.Balance}, Type: {account.GetType().Name} ");
            Console.WriteLine("1. Transfer money");
            Console.WriteLine("2. View transaction history");
            Console.WriteLine("3. Delete account");
            Console.WriteLine("4. Export transactions to file");
            Console.WriteLine("5. Exit");

            Console.Write("Choose an option: ");
            string option = Console.ReadLine();

            switch (option)
            {
                case "1":
                    account.SendMoney();
                    break;
                case "2":
                    ViewTransactionHistory(account.Id);
                    break;
                case "3":
                    DeleteAccount(account, accounts);
                    return;
                case "4":
                    ExportTransactionsToFile(account);
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
    }

    // export transakci do textoveho souboru
    static void ExportTransactionsToFile(Account account)
    {
        var transactions = GetTransactionHistory(account.Id);
        if (transactions.Count == 0)
        {
            Console.WriteLine("No transactions to export.");
            return;
        }

        string fileName = $"transactions_account_{account.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        try
        {
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine($"Transaction History for Account {account.Id}");
                writer.WriteLine($"Account Type: {account.GetType().Name}");
                writer.WriteLine($"Current Balance: {account.Balance:C}");
                writer.WriteLine("----------------------------------------");
                writer.WriteLine();

                foreach (var transaction in transactions)
                {
                    string direction = transaction.FromAccountId == account.Id ? "Sent to" : "Received from";
                    int otherAccountId = transaction.FromAccountId == account.Id ? transaction.ToAccountId : transaction.FromAccountId;
                    string amount = transaction.FromAccountId == account.Id ? $"-{transaction.Amount:C}" : $"+{transaction.Amount:C}";

                    writer.WriteLine($"Date: {transaction.Timestamp}");
                    writer.WriteLine($"{direction} Account: {otherAccountId}");
                    writer.WriteLine($"Amount: {amount}");
                    writer.WriteLine("----------------------------------------");
                }
            }

            Console.WriteLine($"Transactions exported successfully to {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting transactions: {ex.Message}");
        }
    }

    // smazani uctu, pouze s nulovym zustatkem
    static void DeleteAccount(Account account, List<Account> accounts)
    {
        if (account.Balance != 0)
        {
            Console.WriteLine("Please transfer all your funds before deleting the account.");
            return;
        }

        string connectionString = "Data Source=database.db;";
        using (SqliteConnection connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // smazani souvisejicich transakci
            string deleteTransactionsQuery = "DELETE FROM Transactions WHERE FromAccountId = @id OR ToAccountId = @id;";
            using (SqliteCommand cmd = new SqliteCommand(deleteTransactionsQuery, connection))
            {
                cmd.Parameters.AddWithValue("@id", account.Id);
                cmd.ExecuteNonQuery();
            }

            // smazani uctu
            string deleteAccountQuery = "DELETE FROM Accounts WHERE Id = @id;";
            using (SqliteCommand cmd = new SqliteCommand(deleteAccountQuery, connection))
            {
                cmd.Parameters.AddWithValue("@id", account.Id);
                cmd.ExecuteNonQuery();
            }

            accounts.Remove(account);
            Console.WriteLine("Account deleted successfully.");
        }
    }

    // vytvoreni noveho uctu pro uzivatele
    static void StartNewAccount(int userId)
    {
        // kontroluje, jestli je uzivatel dite
        string connectionString = "Data Source=database.db;";
        bool isChild = false;
        using (SqliteConnection connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            string query = "SELECT Children FROM Users WHERE Id = @userId;";
            using (SqliteCommand cmd = new SqliteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                isChild = Convert.ToBoolean(cmd.ExecuteScalar());
            }
        }

        Console.WriteLine("Choose account type:");
        if (isChild)
        {
            Console.WriteLine("1. Debit");
            Console.WriteLine("2. Children Saving");
        }
        else
        {
            Console.WriteLine("1. Debit");
            Console.WriteLine("2. Credit");
            Console.WriteLine("3. Saving");
            Console.WriteLine("4. Children Saving");
        }
        Console.Write(": ");

        string accountType = Console.ReadLine();
        string type;
        decimal? spendingLimit = null;

        if (isChild)
        {
            switch (accountType)
            {
                case "1":
                    type = "Debit";
                    break;
                case "2":
                    type = "ChildrenSaving";
                    spendingLimit = 200;
                    break;
                default:
                    Console.WriteLine("Invalid option. Defaulting to Debit.");
                    type = "Debit";
                    break;
            }
        }
        else
        {
            switch (accountType)
            {
                case "1":
                    type = "Debit";
                    break;
                case "2":
                    type = "Credit";
                    break;
                case "3":
                    type = "Saving";
                    break;
                case "4":
                    type = "ChildrenSaving";
                    spendingLimit = 200;
                    break;
                default:
                    Console.WriteLine("Invalid option. Defaulting to Debit.");
                    type = "Debit";
                    break;
            }
        }

        using (SqliteConnection connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            string insertAccountQuery = "INSERT INTO Accounts (UserId, Balance, Type, Children, SpendingLimit) VALUES (@userId, @balance, @type, @children, @spendingLimit);";
            using (SqliteCommand cmd = new SqliteCommand(insertAccountQuery, connection))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@balance", 1000);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@children", type == "ChildrenSaving");
                cmd.Parameters.AddWithValue("@spendingLimit", spendingLimit as object ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            Console.WriteLine($"New {type} account started with a balance of 1000.");
        }
    }

    static void ViewTransactionHistory(int accountId)
    {
        List<Transaction> transactions = GetTransactionHistory(accountId);
        foreach (var transaction in transactions)
        {
            Console.WriteLine($"Transaction ID: {transaction.Id}, From: {transaction.FromAccountId}, To: {transaction.ToAccountId}, Amount: {transaction.Amount}, Timestamp: {transaction.Timestamp}");
        }
    }

    static List<Account> GetUserAccounts(int userId)
    {
        string connectionString = "Data Source=database.db;";
        using (SqliteConnection connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            string query = "SELECT Id, Balance, Type, SpendingLimit FROM Accounts WHERE UserId = @userId;";
            using (SqliteCommand cmd = new SqliteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    List<Account> accounts = new List<Account>();
                    while (reader.Read())
                    {
                        string type = reader.GetString(2);
                        Account account;
                        switch (type)
                        {
                            case "Debit":
                                account = new DebitAccount();
                                break;
                            case "Credit":
                                account = new CreditAccount();
                                break;
                            case "Saving":
                                account = new SavingAccount();
                                break;
                            case "ChildrenSaving":
                                account = new ChildrenSavingAccount();
                                break;
                            default:
                                account = new DebitAccount();
                                break;
                        }
                        account.Id = reader.GetInt32(0);
                        account.Balance = reader.GetDecimal(1);
                        account.SpendingLimit = !reader.IsDBNull(3) ? reader.GetDecimal(3) : null;
                        accounts.Add(account);
                    }
                    return accounts;
                }
            }
        }
    }

    static bool UserExists(SqliteConnection connection, int userId)
    {
        string query = "SELECT COUNT(*) FROM Users WHERE Id = @id;";
        using (SqliteCommand cmd = new SqliteCommand(query, connection))
        {
            cmd.Parameters.AddWithValue("@id", userId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    static bool VerifyPassword(SqliteConnection connection, string userId, string password)
    {
        string query = "SELECT Password FROM Users WHERE Id = @id;";
        using (SqliteCommand cmd = new SqliteCommand(query, connection))
        {
            cmd.Parameters.AddWithValue("@id", userId);
            object result = cmd.ExecuteScalar();

            if (result != null)
            {
                string storedHash = result.ToString();
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }
        }
        return false;
    }

    // registrace noveho uzivatele
    static int RegisterUser(SqliteConnection connection, int userId)
    {
        Console.Write("Jméno Příjmení: ");
        string name = Console.ReadLine();

        Console.Write("Nové heslo: ");
        string password = Console.ReadLine();
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        string insertUserQuery = "INSERT INTO Users (Id, Role, Name, Password, Children) VALUES (@id, @role, @name, @password, @children);";
        using (SqliteCommand cmd = new SqliteCommand(insertUserQuery, connection))
        {
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.Parameters.AddWithValue("@role", "client");
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@password", hashedPassword);
            cmd.Parameters.AddWithValue("@children", false);
            cmd.ExecuteNonQuery();
        }

        Console.WriteLine("Účet vytvořen úspěšně! ID uživatele: " + userId);

        // vytvori ucet s balance 1000 pro noveho uzivatele
        string insertAccountQuery = "INSERT INTO Accounts (UserId, Balance, Type, Children) VALUES (@userId, @balance, @type, @children);";
        using (SqliteCommand accountCmd = new SqliteCommand(insertAccountQuery, connection))
        {
            accountCmd.Parameters.AddWithValue("@userId", userId);
            accountCmd.Parameters.AddWithValue("@balance", 1000);
            accountCmd.Parameters.AddWithValue("@type", "Debit");
            accountCmd.Parameters.AddWithValue("@children", false);
            accountCmd.ExecuteNonQuery();
        }

        return userId;
    }

    static User GetUser(SqliteConnection connection, string userId)
    {
        string query = "SELECT Id, Role, Name, Children FROM Users WHERE Id = @id;";
        using (SqliteCommand cmd = new SqliteCommand(query, connection))
        {
            cmd.Parameters.AddWithValue("@id", userId);
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    return new User(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetBoolean(3)
                    );
                }
                else
                {
                    throw new Exception("User not found.");
                }
            }
        }
    }

    static List<Transaction> GetTransactionHistory(int accountId)
    {
        string connectionString = "Data Source=database.db;";
        using (SqliteConnection connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            string query = "SELECT Id, FromAccountId, ToAccountId, Amount, Timestamp FROM Transactions WHERE FromAccountId = @accountId OR ToAccountId = @accountId;";
            using (SqliteCommand cmd = new SqliteCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@accountId", accountId);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    List<Transaction> transactions = new List<Transaction>();
                    while (reader.Read())
                    {
                        transactions.Add(new Transaction
                        {
                            Id = reader.GetInt32(0),
                            FromAccountId = reader.GetInt32(1),
                            ToAccountId = reader.GetInt32(2),
                            Amount = reader.GetDecimal(3),
                            Timestamp = reader.GetDateTime(4)
                        });
                    }
                    return transactions;
                }
            }
        }
    }
}

// konfigurace banky - uroky, limity, intervaly
public class BankConfig
{
    public int InterestCalculationIntervalSeconds { get; set; }
    public decimal SavingsInterestRate { get; set; }
    public decimal CreditInterestRate { get; set; }
    public int CreditGracePeriodSeconds { get; set; }
    public decimal SpendingLimit { get; set; }
}

// sprava konfigurace banky:
// - nacita nastaveni z JSON souboru
// - urcuje urokove sazby a limity pro ruzne typy uctu
// - umoznuje zmeny nastaveni za behu
class BankConfigManager
{
    // defaultni konfigurace pokud neexistuje config.json
    private static readonly BankConfig DefaultConfig = new BankConfig
    {
        InterestCalculationIntervalSeconds = 30,  // interval pro vypocet uroku
        SavingsInterestRate = 0.03m,             // 3% rocni urok pro sporici ucty
        CreditInterestRate = 0.12m,              // 12% rocni urok pro kreditni ucty
        CreditGracePeriodSeconds = 60,           // doba bez uroku na kreditnim uctu
        SpendingLimit = 1000.00m                 // zakladni limit pro prevody
    };

    // nacte konfiguraci ze souboru nebo vytvori novou
    // validuje hodnoty a opravi neplatne na defaultni
    public static BankConfig LoadConfiguration()
    {
        try
        {
            // vytvori defaultni konfiguraci, pokud neexistuje config.json
            if (!File.Exists("config.json"))
            {
                string jsonString = JsonSerializer.Serialize(DefaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText("config.json", jsonString);
                return DefaultConfig;
            }

            string configJson = File.ReadAllText("config.json");
            var config = JsonSerializer.Deserialize<BankConfig>(configJson);

            // validuje a opravi neplatne hodnoty
            if (config.InterestCalculationIntervalSeconds <= 0)
                config.InterestCalculationIntervalSeconds = DefaultConfig.InterestCalculationIntervalSeconds;
            if (config.CreditGracePeriodSeconds <= 0)
                config.CreditGracePeriodSeconds = DefaultConfig.CreditGracePeriodSeconds;
            if (config.SpendingLimit <= 0)
                config.SpendingLimit = DefaultConfig.SpendingLimit;

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}. Using default values.");
            return DefaultConfig;
        }
    }
}

// zaznam transakce mezi ucty
class Transaction
{
    public int Id { get; set; }
    public int FromAccountId { get; set; }
    public int ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
}

// abstraktni trida pro vsechny typy uctu
// zakladni operace pro prevody penez a kontrolu zustatku
abstract class Account
{
    private static readonly BankConfig _config = BankConfigManager.LoadConfiguration();

    public int Id { get; set; }
    public decimal Balance { get; protected internal set; }
    public decimal? SpendingLimit { get; set; }

    protected virtual decimal GetMaximumDebit() => 0;

    // kontroluje zda je prevod mozny:
    // - dostatecny zustatek vcetne limitu
    // - kladna castka
    // - existujici cilovy ucet
    protected virtual bool ValidateTransfer(decimal amount, decimal currentBalance, int toAccountId)
    {
        if (amount <= 0)
        {
            Console.WriteLine("Amount must be greater than zero. Please try again.");
            return false;
        }

        var effectiveLimit = SpendingLimit ?? GetMaximumDebit();
        if (currentBalance - amount < -effectiveLimit)
        {
            Console.WriteLine($"Insufficient balance. You cannot go below -{effectiveLimit:C}. Please try again.");
            return false;
        }

        if (!AccountExists(toAccountId))
        {
            Console.WriteLine("Recipient account does not exist. Please try again.");
            return false;
        }

        return true;
    }

    // provadi prevod penez mezi ucty:
    // - kontroluje aktualni zustatek
    // - provadi transakci v ramci jedne databazove transakce
    // - aktualizuje zustatek po uspesnem prevodu
    protected virtual void ProcessTransfer(int toAccountId, decimal amount)
    {
        using var connection = DbHelper.GetConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            ExecuteTransfer(connection, transaction, amount, toAccountId);
            transaction.Commit();
            // aktualizuje balance po uspesnem prevodu
            Balance = GetCurrentBalance();
            Console.WriteLine("Money transferred successfully.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // provadi samotny prevod v databazi:
    // - odecte penize z uctu odesilatele
    // - pricte penize na ucet prijemce
    // - vytvori zaznam o transakci
    private void ExecuteTransfer(SqliteConnection connection, SqliteTransaction transaction, decimal amount, int toAccountId)
    {
        var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        cmd.CommandText = "SELECT Balance FROM Accounts WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", Id);
        decimal currentBalance = Convert.ToDecimal(cmd.ExecuteScalar());

        // overi, zda existuje prijemci ucet a ziska jeho balance
        cmd.CommandText = "SELECT Balance FROM Accounts WHERE Id = @toAccountId";
        cmd.Parameters.AddWithValue("@toAccountId", toAccountId);
        var recipientBalanceObj = cmd.ExecuteScalar();
        if (recipientBalanceObj == null)
        {
            throw new Exception("Recipient account not found.");
        }
        decimal recipientBalance = Convert.ToDecimal(recipientBalanceObj);

        // odecte penize z odesilatele
        cmd.CommandText = "UPDATE Accounts SET Balance = Balance - @amount WHERE Id = @id";
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.ExecuteNonQuery();

        // pricte penize na ucet prijemce
        cmd.CommandText = "UPDATE Accounts SET Balance = Balance + @amount WHERE Id = @toAccountId";
        cmd.ExecuteNonQuery();

        // zaznamenani transakce
        cmd.CommandText = "INSERT INTO Transactions (FromAccountId, ToAccountId, Amount) VALUES (@fromAccountId, @toAccountId, @amount)";
        cmd.Parameters.AddWithValue("@fromAccountId", Id);
        cmd.ExecuteNonQuery();
    }

    protected decimal GetCurrentBalance()
    {
        return DbHelper.ExecuteScalar<decimal>(
            "SELECT Balance FROM Accounts WHERE Id = @id",
            new Dictionary<string, object> { ["@id"] = Id });
    }

    protected bool AccountExists(int accountId)
    {
        return DbHelper.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Accounts WHERE Id = @id",
            new Dictionary<string, object> { ["@id"] = accountId }) > 0;
    }

    protected static string ReadInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }

    // odeslani penez na jiny ucet
    public virtual void SendMoney()
    {
        while (true)
        {
            if (!int.TryParse(ReadInput("Enter the recipient's account ID: "), out int toAccountId))
            {
                Console.WriteLine("Invalid account ID. Please try again.");
                continue;
            }

            if (!decimal.TryParse(ReadInput("Enter the amount to transfer: "), out decimal amount))
            {
                Console.WriteLine("Invalid amount. Please try again.");
                continue;
            }

            var currentBalance = GetCurrentBalance();
            if (!ValidateTransfer(amount, currentBalance, toAccountId))
                continue;

            try
            {
                ProcessTransfer(toAccountId, amount);
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing transfer: {ex.Message}. Please try again.");
            }
        }
    }
}

// bezny ucet bez uroku
class DebitAccount : Account { }

// kreditni ucet:
// - umoznuje cerpat do minusu do stanoveneho limitu
// - pocita urok z prumerneho zustatku
// - eviduje historii zustatku pro vypocet uroku
class CreditAccount : Account
{
    // historie zustatku pro vypocet uroku
    private readonly List<(DateTime Time, decimal Balance)> _balanceHistory;

    public CreditAccount()
    {
        var config = BankConfigManager.LoadConfiguration();
        _balanceHistory = new List<(DateTime Time, decimal Balance)>();
        UpdateBalanceHistory();
    }

    private void UpdateBalanceHistory()
    {
        var now = DateTime.Now;
        if (_balanceHistory.Count > 0)
        {
            var lastEntry = _balanceHistory[_balanceHistory.Count - 1];
            if (lastEntry.Balance != Balance)
            {
                _balanceHistory.Add((now, Balance));
            }
        }
        else
        {
            _balanceHistory.Add((now, Balance));
        }
    }

    protected override decimal GetMaximumDebit() => BankConfigManager.LoadConfiguration().SpendingLimit;

    protected override void ProcessTransfer(int toAccountId, decimal amount)
    {
        base.ProcessTransfer(toAccountId, amount);
        UpdateBalanceHistory();
    }

    // vypocet uroku z prumerneho zustatku:
    // - bere v uvahu zmeny zustatku v case
    // - pouziva vazeny prumer pro presnejsi vypocet
    private decimal CalculateInterest()
    {
        var now = DateTime.Now;
        var totalSeconds = (now - _balanceHistory.First().Time).TotalSeconds;
        var weightedSum = _balanceHistory.Skip(1)
            .Zip(_balanceHistory, (curr, prev) =>
                Convert.ToDecimal((curr.Time - prev.Time).TotalSeconds) * prev.Balance)
            .Sum();

        var averageBalance = weightedSum / (decimal)totalSeconds;
        return averageBalance < 0 ? averageBalance * BankConfigManager.LoadConfiguration().CreditInterestRate / 12 : 0;
    }
}

// sporici ucet s pravidelnym pripisovanim uroku
// pocita urok z prumerneho zustatku za dane obdobi
class SavingAccount : Account
{
    private readonly System.Timers.Timer _timer;
    private DateTime _lastInterestCalculationTime;
    private readonly List<(DateTime Time, decimal Balance)> _balanceHistory;

    public SavingAccount()
    {
        var config = BankConfigManager.LoadConfiguration();
        _timer = new System.Timers.Timer(config.InterestCalculationIntervalSeconds * 1000.0);
        _timer.Elapsed += AddInterest;
        _timer.AutoReset = true;
        _timer.Enabled = true;
        _lastInterestCalculationTime = DateTime.Now;
        _balanceHistory = new List<(DateTime Time, decimal Balance)>();
    }

    protected override void ProcessTransfer(int toAccountId, decimal amount)
    {
        base.ProcessTransfer(toAccountId, amount);
        _balanceHistory.Add((DateTime.Now, Balance));
    }

    // pripise urok na zaklade prumerneho zustatku
    // vytvori transakcni zaznam o pripisanem uroku
    private void AddInterest(object sender, ElapsedEventArgs e)
    {
        var config = BankConfigManager.LoadConfiguration();
        var now = DateTime.Now;
        _balanceHistory.Add((now, Balance));

        var interest = CalculateInterest(now, config.SavingsInterestRate);
        UpdateBalanceAndRecordInterest(interest);

        _balanceHistory.Clear();
        _balanceHistory.Add((now, Balance));
        _lastInterestCalculationTime = now;
    }

    // vypocita vazeny prumer zustatku za casove obdobi
    // pouziva historii zustatku pro presny vypocet
    private decimal CalculateInterest(DateTime now, decimal interestRate)
    {
        var totalSeconds = (now - _lastInterestCalculationTime).TotalSeconds;
        var weightedSum = _balanceHistory.Skip(1)
            .Select((b, i) => _balanceHistory[i].Balance * (decimal)(b.Time - _balanceHistory[i].Time).TotalSeconds)
            .Sum();

        var averageBalance = weightedSum / (decimal)totalSeconds;
        return averageBalance * interestRate / 12;
    }

    private void UpdateBalanceAndRecordInterest(decimal interest)
    {
        Balance += interest;
        DbHelper.ExecuteNonQuery(
            "UPDATE Accounts SET Balance = @balance WHERE Id = @id",
            new Dictionary<string, object> { ["@balance"] = Balance, ["@id"] = Id });

        DbHelper.ExecuteNonQuery(
            "INSERT INTO Transactions (FromAccountId, ToAccountId, Amount) VALUES (@id, @id, @amount)",
            new Dictionary<string, object> { ["@id"] = Id, ["@amount"] = interest });
    }
}

// detsky sporici ucet:
// - ma denni a jednorazovy limit pro prevody
// - limit se resetuje kazdou sekundu (simulace dne)
// - vyzaduje potvrzeni od rodice pro vyssi castky
class ChildrenSavingAccount : SavingAccount
{
    private const decimal MaxTransferAmount = 200;
    private decimal _dailySpent = 0;
    private DateTime _lastSpendingReset;

    public ChildrenSavingAccount()
    {
        _lastSpendingReset = DateTime.Now;
        var config = BankConfigManager.LoadConfiguration();
        // startuje timer na reset denniho vydaju (InterestCalculationIntervalSeconds/30 = 1 den)
        var dayInSeconds = config.InterestCalculationIntervalSeconds / 30.0;
        var timer = new System.Timers.Timer(dayInSeconds * 1000.0); // prevod na milisekundy
        timer.Elapsed += ResetDailySpending;
        timer.AutoReset = true;
        timer.Enabled = true;
    }

    // reset denního limitu (InterestCalculationIntervalSeconds/30 = 1 den)
    private void ResetDailySpending(object sender, ElapsedEventArgs e)
    {
        var config = BankConfigManager.LoadConfiguration();
        var now = DateTime.Now;
        var dayInSeconds = config.InterestCalculationIntervalSeconds / 30.0;
        var secondsElapsed = (now - _lastSpendingReset).TotalSeconds;

        // pokud uplynul jeden den (InterestCalculationIntervalSeconds/30)
        if (secondsElapsed >= dayInSeconds)
        {
            _dailySpent = 0;
            _lastSpendingReset = now;
        }
    }

    // kontrola limitu pro prevod:
    // - overuje jednorazovy limit
    // - kontroluje denni limit
    // - aktualizuje soucet dennich vydaju
    protected override bool ValidateTransfer(decimal amount, decimal currentBalance, int toAccountId)
    {
        if (amount > MaxTransferAmount)
        {
            Console.WriteLine($"Amount exceeds the maximum transfer limit of {MaxTransferAmount:C}.");
            return false;
        }

        if (_dailySpent + amount > MaxTransferAmount)
        {
            var remainingDaily = MaxTransferAmount - _dailySpent;
            Console.WriteLine($"Daily spending limit reached. You can only spend {remainingDaily:C} more today.");
            return false;
        }

        if (!base.ValidateTransfer(amount, currentBalance, toAccountId))
            return false;

        return true;
    }

    protected override void ProcessTransfer(int toAccountId, decimal amount)
    {
        base.ProcessTransfer(toAccountId, amount);
        _dailySpent += amount;
    }

    public override void SendMoney()
    {
        while (true)
        {
            if (!int.TryParse(ReadInput("Enter the recipient's account ID: "), out int toAccountId))
            {
                Console.WriteLine("Invalid account ID. Please try again.");
                continue;
            }

            var remainingDaily = MaxTransferAmount - _dailySpent;
            Console.Write($"Enter the amount to transfer (max {remainingDaily:C}): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal amount))
            {
                Console.WriteLine("Invalid amount. Please try again.");
                continue;
            }

            var currentBalance = GetCurrentBalance();
            if (!ValidateTransfer(amount, currentBalance, toAccountId))
                continue;

            ProcessTransfer(toAccountId, amount);
            break;
        }
    }
}

