using System.Buffers;
using System.Text;

namespace Albin.Export;

/// <summary>
/// Turns caller supplied text into a safe, portable file name.
/// </summary>
/// <remarks>
/// Export requests frequently carry a user or report supplied name. Sanitizing it centrally means an
/// export can never escape into a directory, target a drive, or produce a name that Windows refuses
/// to create. Every <see cref="Abstractions.ExportResult"/> file name passes through
/// <see cref="Sanitize(string?, string?)"/>.
/// </remarks>
public static class ExportFileName
{
    private const int MaxLength = 120;
    private const string Fallback = "export";

    private static readonly SearchValues<char> InvalidChars =
        SearchValues.Create("\"<>|:*?\\/\0\u0001\u0002\u0003\u0004\u0005\u0006\a\b\t\n\v\f\r"
            + "\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f");

    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>
    /// Produces a safe file name, optionally ensuring a given extension.
    /// </summary>
    /// <param name="name">
    /// The requested name. Directory separators, drive qualifiers, control characters and
    /// characters that are invalid on Windows or Linux are removed rather than escaped.
    /// </param>
    /// <param name="extension">
    /// The desired extension with or without a leading dot. When supplied and missing from
    /// <paramref name="name"/>, it is appended.
    /// </param>
    /// <returns>A file name that contains no path information and is safe to use in a
    /// <c>Content-Disposition</c> header or as a ZIP entry name.</returns>
    public static string Sanitize(string? name, string? extension = null)
    {
        var span = (name ?? string.Empty).AsSpan().Trim();

        // Drop anything that looks like a path: keep only the last segment.
        var lastSeparator = span.LastIndexOfAny('/', '\\');
        if (lastSeparator >= 0)
        {
            span = span[(lastSeparator + 1)..];
        }

        var colon = span.LastIndexOf(':');
        if (colon >= 0)
        {
            span = span[(colon + 1)..];
        }

        var builder = new StringBuilder(Math.Min(span.Length, MaxLength));
        foreach (var c in span)
        {
            if (InvalidChars.Contains(c) || char.IsControl(c))
            {
                continue;
            }

            builder.Append(c);
            if (builder.Length >= MaxLength)
            {
                break;
            }
        }

        var candidate = builder.ToString().Trim().TrimEnd('.', ' ');
        while (candidate.StartsWith('.'))
        {
            candidate = candidate[1..].TrimStart();
        }

        if (candidate.Length == 0)
        {
            candidate = Fallback;
        }

        var normalizedExtension = NormalizeExtension(extension);
        var stem = normalizedExtension.Length > 0 && candidate.EndsWith(normalizedExtension, StringComparison.OrdinalIgnoreCase)
            ? candidate[..^normalizedExtension.Length]
            : candidate;

        if (IsReserved(stem))
        {
            stem = "_" + stem;
        }

        if (stem.Length == 0)
        {
            stem = Fallback;
        }

        return stem + normalizedExtension;
    }

    /// <summary>
    /// Determines whether a ZIP entry name is safe to add to an archive.
    /// </summary>
    /// <param name="entryName">The candidate entry name. Forward slashes are allowed as directory separators.</param>
    /// <returns>
    /// <see langword="true"/> when the name is relative, contains no <c>..</c> segment, no drive
    /// qualifier, no backslash and no control characters.
    /// </returns>
    public static bool IsSafeEntryName(string? entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        if (entryName.Contains('\\', StringComparison.Ordinal)
            || entryName.Contains(':', StringComparison.Ordinal)
            || entryName.StartsWith('/'))
        {
            return false;
        }

        foreach (var segment in entryName.Split('/'))
        {
            if (segment.Length == 0 || segment is ".." or ".")
            {
                return false;
            }

            if (!string.Equals(Sanitize(segment), segment, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Sanitizes a ZIP entry name, preserving safe <c>/</c> separated folders.
    /// </summary>
    /// <param name="entryName">The candidate entry name.</param>
    /// <returns>A relative entry name that cannot traverse outside the archive root.</returns>
    public static string SanitizeEntryName(string? entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return Fallback;
        }

        var segments = entryName
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static s => s is not ("." or ".."))
            .Select(static s => Sanitize(s))
            .ToArray();

        return segments.Length == 0 ? Fallback : string.Join('/', segments);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    private static bool IsReserved(string stem)
    {
        foreach (var reserved in ReservedNames)
        {
            if (stem.Equals(reserved, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
