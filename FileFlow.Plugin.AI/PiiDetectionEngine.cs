using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FileFlow.Plugin.AI;

/// <summary>
/// Representa una entidad sensible detectada en un texto.
/// </summary>
public record PiiMatch(string Category, string RawValue, int Index, int Length);

/// <summary>
/// Resultado del análisis y anonimización de datos personales bajo RGPD / PII.
/// </summary>
public record PiiAnonymizationResult(
    bool PiiDetected,
    int TotalCount,
    IReadOnlyList<string> Categories,
    Dictionary<string, int> CountsByCategory,
    string SanitizedText);

/// <summary>
/// Opciones de configuración para el análisis y anonimización de datos personales.
/// </summary>
public record PiiOptions(
    string Mode = "TagReplacement",
    bool FilterDniNie = true,
    bool FilterIban = true,
    bool FilterCreditCards = true,
    bool FilterEmails = true,
    bool FilterPhones = true,
    bool FilterIpAddresses = true,
    bool FilterPersonNames = true);

/// <summary>
/// Motor centralizado de detección y anonimización de información de identificación personal (PII) bajo RGPD.
/// Implementa detectores deterministas de alta precisión (algoritmos Luhn, MOD-97, dígitos de control DNI/NIE) y ofuscación segura.
/// </summary>
public static class PiiDetectionEngine
{
    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        @"\b(?:\+?\d{1,3}[ -]?)?(?:\(?\d{2,4}\)?[ -]?)?[6789]\d{2}[ -]?\d{3}[ -]?\d{3}\b|\b(?:\+34|0034)?[ -]?[9]\d{2}[ -]?\d{3}[ -]?\d{3}\b",
        RegexOptions.Compiled);

    private static readonly Regex DniNieRegex = new(
        @"\b(?:[XYZ]\d{7}[A-Z]|\d{8}[A-Z])\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IbanRegex = new(
        @"\b[A-Z]{2}\d{2}[ -]?(?:\d{4}[ -]?){4}\d{2,4}\b|\b[A-Z]{2}\d{2}[A-Z0-9]{11,30}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d{4}[ -]?){3}\d{4}\b|\b\d{13,19}\b",
        RegexOptions.Compiled);

    private static readonly Regex Ipv4Regex = new(
        @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
        RegexOptions.Compiled);

    private static readonly Regex PersonTitleRegex = new(
        @"\b(?:Sr\.|Sra\.|Don|Doña|D\.|Dña\.|Mr\.|Mrs\.|Dr\.|Dra\.)\s+([A-ZÁÉÍÓÚÑ][a-záéíóúñ]+(?:\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+){1,3})\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Analiza y anonimiza el texto según las opciones y el modo de reemplazo seleccionado.
    /// </summary>
    public static PiiAnonymizationResult AnonymizeText(string text, PiiOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new PiiAnonymizationResult(false, 0, [], new Dictionary<string, int>(), text ?? string.Empty);
        }

        var matches = new List<PiiMatch>();

        // 1. DNI / NIE
        if (options.FilterDniNie)
        {
            foreach (Match m in DniNieRegex.Matches(text))
            {
                if (IsValidDniOrNie(m.Value))
                {
                    matches.Add(new PiiMatch("DNI/NIE", m.Value, m.Index, m.Length));
                }
            }
        }

        // 2. IBAN
        if (options.FilterIban)
        {
            foreach (Match m in IbanRegex.Matches(text))
            {
                string cleanIban = m.Value.Replace(" ", "").Replace("-", "");
                if (cleanIban.Length >= 15 && IsValidIban(cleanIban))
                {
                    matches.Add(new PiiMatch("IBAN", m.Value, m.Index, m.Length));
                }
            }
        }

        // 3. Tarjetas de Crédito (con validación de Luhn)
        if (options.FilterCreditCards)
        {
            foreach (Match m in CreditCardRegex.Matches(text))
            {
                string digits = new(m.Value.Where(char.IsDigit).ToArray());
                if (digits.Length >= 13 && digits.Length <= 19 && IsValidLuhn(digits))
                {
                    matches.Add(new PiiMatch("CreditCard", m.Value, m.Index, m.Length));
                }
            }
        }

        // 4. Correos Electrónicos
        if (options.FilterEmails)
        {
            foreach (Match m in EmailRegex.Matches(text))
            {
                matches.Add(new PiiMatch("Email", m.Value, m.Index, m.Length));
            }
        }

        // 5. Teléfonos
        if (options.FilterPhones)
        {
            foreach (Match m in PhoneRegex.Matches(text))
            {
                matches.Add(new PiiMatch("Phone", m.Value, m.Index, m.Length));
            }
        }

        // 6. Direcciones IP
        if (options.FilterIpAddresses)
        {
            foreach (Match m in Ipv4Regex.Matches(text))
            {
                matches.Add(new PiiMatch("IPAddress", m.Value, m.Index, m.Length));
            }
        }

        // 7. Nombres Propios de Personas (Patrones contextuales honoríficos y títulos)
        if (options.FilterPersonNames)
        {
            foreach (Match m in PersonTitleRegex.Matches(text))
            {
                if (m.Groups.Count > 1)
                {
                    var g = m.Groups[1];
                    matches.Add(new PiiMatch("Person", g.Value, g.Index, g.Length));
                }
            }
        }

        if (matches.Count == 0)
        {
            return new PiiAnonymizationResult(false, 0, [], new Dictionary<string, int>(), text);
        }

        // Eliminar solapamientos ordenando por posición descendente
        var distinctMatches = matches
            .OrderByDescending(m => m.Index)
            .ThenByDescending(m => m.Length)
            .ToList();

        var nonOverlapping = new List<PiiMatch>();
        int lastStart = int.MaxValue;
        foreach (var m in distinctMatches)
        {
            if (m.Index + m.Length <= lastStart)
            {
                nonOverlapping.Add(m);
                lastStart = m.Index;
            }
        }

        // Construir estadísticas
        var countsByCategory = nonOverlapping
            .GroupBy(m => m.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var categories = countsByCategory.Keys.OrderBy(c => c).ToList();

        // Aplicar reemplazos de derecha a izquierda
        var sb = new StringBuilder(text);
        foreach (var match in nonOverlapping)
        {
            string replacement = GetReplacement(match, options.Mode);
            sb.Remove(match.Index, match.Length);
            sb.Insert(match.Index, replacement);
        }

        return new PiiAnonymizationResult(
            true,
            nonOverlapping.Count,
            categories,
            countsByCategory,
            sb.ToString());
    }

    private static string GetReplacement(PiiMatch match, string mode)
    {
        return mode switch
        {
            "Mask" => GenerateMask(match),
            "Hash" => $"[ID_{ComputeShortHash(match.RawValue)}]",
            "Remove" => string.Empty,
            _ => $"[{match.Category.ToUpperInvariant()}]" // TagReplacement por defecto
        };
    }

    private static string GenerateMask(PiiMatch match)
    {
        string val = match.RawValue;
        if (match.Category == "Email" && val.Contains('@'))
        {
            var parts = val.Split('@');
            string user = parts[0].Length > 2 ? parts[0][0] + new string('*', parts[0].Length - 2) + parts[0][^1] : "**";
            return $"{user}@{parts[1]}";
        }

        if (match.Category == "CreditCard")
        {
            string digits = new(val.Where(char.IsDigit).ToArray());
            return digits.Length > 4 ? $"****-****-****-{digits[^4..]}" : "****";
        }

        if (match.Category == "IBAN")
        {
            string clean = val.Replace(" ", "").Replace("-", "");
            return clean.Length > 8 ? $"{clean[..4]} **** **** **** {clean[^4..]}" : "ES** ****";
        }

        if (match.Category == "DNI/NIE")
        {
            return val.Length > 3 ? $"{val[..2]}****{val[^1]}" : "***";
        }

        return new string('*', Math.Max(val.Length, 4));
    }

    private static string ComputeShortHash(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input.Trim().ToLowerInvariant()));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    private static bool IsValidDniOrNie(string val)
    {
        if (string.IsNullOrWhiteSpace(val) || val.Length != 9) return false;

        val = val.ToUpperInvariant();
        char first = val[0];
        string digits;

        if (first is 'X' or 'Y' or 'Z')
        {
            int prefix = first switch { 'X' => 0, 'Y' => 1, 'Z' => 2, _ => -1 };
            digits = prefix + val.Substring(1, 7);
        }
        else if (char.IsDigit(first))
        {
            digits = val[..8];
        }
        else
        {
            return false;
        }

        if (!int.TryParse(digits, out int num)) return false;

        const string letters = "TRWAGMYFPDXBNJZSQVHLCKE";
        char expectedLetter = letters[num % 23];
        return val[^1] == expectedLetter;
    }

    private static bool IsValidIban(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban) || iban.Length < 15 || iban.Length > 34) return false;

        iban = iban.ToUpperInvariant();
        string rearranged = iban[4..] + iban[..4];

        var sb = new StringBuilder();
        foreach (char c in rearranged)
        {
            if (char.IsLetter(c))
                sb.Append((c - 'A' + 10).ToString());
            else if (char.IsDigit(c))
                sb.Append(c);
            else
                return false;
        }

        string numericIban = sb.ToString();
        int checksum = 0;
        foreach (char c in numericIban)
        {
            checksum = (checksum * 10 + (c - '0')) % 97;
        }

        return checksum == 1;
    }

    private static bool IsValidLuhn(string digits)
    {
        int sum = 0;
        bool alternate = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }

        return (sum % 10) == 0;
    }
}
