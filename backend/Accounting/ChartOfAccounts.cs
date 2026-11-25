using System.Text.Json;
using System.Text.Json.Serialization;

namespace backend.Accounting;

[JsonConverter(typeof(JsonStringEnumConverter<Category>))]
public enum Category
{
    Income,
    Expense,
}

public record Account(int Number, string Name, Category Category, bool Active);

public class ChartOfAccountsException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// The club's Chart of Accounts, read from a checked-in file (see ADR 0001).
/// </summary>
public class ChartOfAccounts
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<int, Account> _byNumber;

    private ChartOfAccounts(IReadOnlyList<Account> accounts)
    {
        Accounts = accounts;
        _byNumber = accounts.ToDictionary(a => a.Number);
    }

    public IReadOnlyList<Account> Accounts { get; }

    /// <summary>Resolves an Account by number, including inactive ones.</summary>
    public Account? Find(int number) => _byNumber.GetValueOrDefault(number);

    public static ChartOfAccounts Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            throw new ChartOfAccountsException($"Kontoplan '{path}' kann nicht gelesen werden: {e.Message}", e);
        }
        return Parse(json);
    }

    public static ChartOfAccounts Parse(string json)
    {
        FileShape? file;
        try
        {
            file = JsonSerializer.Deserialize<FileShape>(json, JsonOptions);
        }
        catch (JsonException e)
        {
            throw new ChartOfAccountsException($"Kontoplan ist kein gültiges JSON: {e.Message}", e);
        }

        if (file?.Income is null || file.Expense is null)
        {
            throw new ChartOfAccountsException("Kontoplan muss die Abschnitte 'income' und 'expense' enthalten.");
        }

        var accounts = file
            .Income.Select(e => ToAccount(e, Category.Income))
            .Concat(file.Expense.Select(e => ToAccount(e, Category.Expense)))
            .ToList();

        var duplicates = accounts.GroupBy(a => a.Number).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new ChartOfAccountsException(
                $"Kontoplan enthält doppelte Kontonummern: {string.Join(", ", duplicates)}."
            );
        }

        return new ChartOfAccounts(accounts);
    }

    private static Account ToAccount(EntryShape? entry, Category category)
    {
        if (entry is null || entry.Number is not > 0 || string.IsNullOrWhiteSpace(entry.Name))
        {
            throw new ChartOfAccountsException(
                $"Ungültiger Eintrag in '{category}': Nummer (> 0) und Name sind erforderlich."
            );
        }
        return new Account(entry.Number.Value, entry.Name.Trim(), category, !entry.Inactive);
    }

    private record FileShape(List<EntryShape?>? Income, List<EntryShape?>? Expense);

    private record EntryShape(int? Number, string? Name, bool Inactive = false);
}
