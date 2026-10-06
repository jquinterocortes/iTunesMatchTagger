namespace iTunesMatchTagger.Core.Lookup;

/// <summary>
/// iTunes Store country codes (ISO 3166-1 alpha-2), ported from the
/// upstream user settings.
/// </summary>
public static class StoreCountries
{
    public static readonly IReadOnlyList<string> All =
    [
        "US", "GB", "AU", "FR", "DE", "CA", "IT", "JP",
        "DZ", "AO", "AI", "AG", "AR", "AM", "AT", "AZ", "BS", "BH", "BD", "BB",
        "BY", "BE", "BZ", "BM", "BO", "BW", "BR", "BN", "BG", "CM", "KY", "CL",
        "CN", "CO", "CR", "CI", "HR", "CY", "CZ", "DK", "DM", "DO", "EC", "EG",
        "SV", "EE", "ET", "FI", "GH", "GR", "GD", "GT", "GY", "HN", "HK", "HU",
        "IS", "IN", "ID", "IE", "IL", "JM", "JO", "KZ", "KE", "KW", "KR", "LV",
        "LB", "LY", "LI", "LT", "LU", "MO", "MK", "MG", "MY", "MV", "ML", "MT",
        "MU", "MX", "MD", "MS", "MM", "NP", "NL", "NZ", "NI", "NE", "NG", "NO",
        "OM", "PK", "PA", "PY", "PE", "PH", "PL", "PT", "QA", "RO", "RU", "KN",
        "LC", "VC", "SA", "SN", "RS", "SG", "SK", "SI", "ZA", "ES", "LK", "SR",
        "SE", "CH", "TW", "TZ", "TH", "TT", "TN", "TR", "TC", "UG", "UA", "AE",
        "UY", "UZ", "VE", "VN", "VG", "YE",
    ];
}
