using Codecrete.SwissQRBill.Generator;

namespace backend.Invoicing;

/// <summary>The club as QR-bill creditor: its normal IBAN and the Klubadresse from the settings (country is always CH).</summary>
public record QrBillCreditor(string Iban, string Name, string Address, string PostalCode, string City)
{
    public const string MissingAddressError = "Bitte zuerst die Klubadresse in den Einstellungen erfassen";

    /// <summary>The creditor from the club settings, or null when the settings lack the IBAN or any part of the Klubadresse.</summary>
    public static QrBillCreditor? From(backend.Accounting.Settings? settings)
    {
        if (settings is null)
            return null;
        string[] parts = [settings.Iban, settings.Name, settings.Address, settings.PostalCode, settings.City];
        if (parts.Any(string.IsNullOrWhiteSpace))
            return null;
        return new QrBillCreditor(
            settings.Iban.Trim(),
            settings.Name.Trim(),
            settings.Address.Trim(),
            settings.PostalCode.Trim(),
            settings.City.Trim()
        );
    }
}

/// <summary>The Person as QR-bill debtor, with a complete structured address.</summary>
public record QrBillDebtor(
    string Name,
    string Street,
    string Number,
    string PostalCode,
    string City,
    string CountryCode
);

/// <summary>The Swiss QR-bill payment part (210×105 mm) of a Rechnung, rendered as PNG with SwissQRBill.NET.</summary>
public static class QrBillSlip
{
    /// <summary>Height of the payment part.</summary>
    public const float HeightMm = 105;

    /// <summary>Width of the payment part.</summary>
    public const float WidthMm = 210;

    private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Schweiz"] = "CH",
        ["Suisse"] = "CH",
        ["Svizzera"] = "CH",
        ["Switzerland"] = "CH",
        ["Liechtenstein"] = "LI",
        ["Deutschland"] = "DE",
        ["Germany"] = "DE",
        ["Österreich"] = "AT",
        ["Austria"] = "AT",
        ["Frankreich"] = "FR",
        ["France"] = "FR",
        ["Italien"] = "IT",
        ["Italia"] = "IT",
        ["Italy"] = "IT",
    };

    public static Bill Build(QrBillCreditor creditor, string invoiceNumber, decimal amount, QrBillDebtor? debtor)
    {
        var (street, houseNo) = SplitAddressLine(creditor.Address.Trim());
        var bill = new Bill
        {
            Account = creditor.Iban,
            Amount = amount,
            Currency = "CHF",
            Creditor = new Address
            {
                Name = creditor.Name.Trim(),
                Street = street,
                HouseNo = houseNo,
                PostalCode = creditor.PostalCode.Trim(),
                Town = creditor.City.Trim(),
                CountryCode = "CH",
            },
            UnstructuredMessage = $"Rechnung #{invoiceNumber}",
        };
        if (debtor is not null)
        {
            bill.Debtor = new Address
            {
                Name = debtor.Name,
                Street = debtor.Street,
                HouseNo = debtor.Number,
                PostalCode = debtor.PostalCode,
                Town = debtor.City,
                CountryCode = debtor.CountryCode,
            };
        }
        bill.CreateAndSetCreditorReference(invoiceNumber);
        return bill;
    }

    /// <summary>
    /// The Person is the debtor when street, number, PLZ, city and country are all set and fit the QR-bill
    /// (country must be a known country or a two-letter code). Otherwise null: the debtor is left out.
    /// </summary>
    public static QrBillDebtor? DebtorFor(
        string firstName,
        string lastName,
        string? street,
        string? number,
        string? postalCode,
        string? city,
        string? country
    )
    {
        if (
            string.IsNullOrWhiteSpace(street)
            || string.IsNullOrWhiteSpace(number)
            || string.IsNullOrWhiteSpace(postalCode)
            || string.IsNullOrWhiteSpace(city)
            || string.IsNullOrWhiteSpace(country)
        )
            return null;
        var code = CountryCode(country.Trim());
        if (code is null)
            return null;
        var debtor = new QrBillDebtor(
            $"{firstName} {lastName}".Trim(),
            street.Trim(),
            number.Trim(),
            postalCode.Trim(),
            city.Trim(),
            code
        );
        return debtor.Name.Length <= 70
            && debtor.Street.Length <= 70
            && debtor.Number.Length <= 16
            && debtor.PostalCode.Length <= 16
            && debtor.City.Length <= 35
            ? debtor
            : null;
    }

    /// <summary>The payment part as PNG (210×105 mm).</summary>
    public static byte[] RenderPng(Bill bill)
    {
        bill.Format = new BillFormat
        {
            GraphicsFormat = GraphicsFormat.PNG,
            OutputSize = OutputSize.QrBillOnly,
            Language = Language.DE,
            Resolution = 300,
        };
        return QRBill.Generate(bill);
    }

    private static string? CountryCode(string country)
    {
        if (CountryNames.TryGetValue(country, out var code))
            return code;
        return country.Length == 2 && country.All(char.IsAsciiLetter) ? country.ToUpperInvariant() : null;
    }

    /// <summary>"Sportweg 12" becomes street and house number; a line without a trailing number stays the street.</summary>
    private static (string Street, string? HouseNo) SplitAddressLine(string line)
    {
        var i = line.LastIndexOf(' ');
        if (i > 0 && char.IsDigit(line[i + 1]))
            return (line[..i].Trim(), line[(i + 1)..]);
        return (line, null);
    }
}
