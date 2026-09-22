using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace Albin.Export.Pdf;

/// <summary>
/// A PDFsharp font resolver that finds TrueType and OpenType faces already installed on the host.
/// </summary>
/// <remarks>
/// <para>
/// PDFsharp needs font bytes to embed, and has no built-in way to find them on Linux or in a
/// container. This resolver indexes the standard font directories of Windows, Linux and macOS once,
/// then answers requests by matching the family name and style, falling back through a chain of
/// common sans-serif families (Arial, Helvetica, Liberation Sans, DejaVu Sans, Noto Sans, ...) and
/// finally to any face it found.
/// </para>
/// <para>
/// Install it once at startup with <see cref="Register"/>, which <see cref="PdfExporter"/> also does
/// automatically. Replace it with your own <see cref="IFontResolver"/> — for example one that returns
/// a font embedded in your assembly — when you need byte-identical output everywhere.
/// </para>
/// </remarks>
public sealed class SystemFontResolver : IFontResolver
{
    private static readonly string[] FallbackFamilies =
    [
        "arial", "helvetica", "liberationsans", "dejavusans", "notosans", "freesans",
        "segoeui", "verdana", "tahoma", "calibri", "roboto", "opensans", "ubuntu", "cantarell",
        "timesnewroman", "liberationserif", "dejavuserif",
    ];

    private static readonly string[] StyleSuffixes =
    [
        "-regular", "-book", "-bold", "-italic", "-oblique", "-bolditalic", "-boldoblique",
        "-italicbold", "-semibold", "-medium", "-light",
    ];

    private readonly Dictionary<FaceKey, string> _faces;
    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.Ordinal);
    private readonly string? _anyFace;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemFontResolver"/> class and indexes the host's fonts.
    /// </summary>
    public SystemFontResolver()
    {
        _faces = BuildIndex();
        _anyFace = _faces.Count == 0 ? null : _faces.Values.First();
    }

    /// <summary>
    /// Gets the shared resolver instance.
    /// </summary>
    public static SystemFontResolver Instance { get; } = new();

    /// <summary>
    /// Gets a value indicating whether at least one usable font was found on this host.
    /// </summary>
    public bool HasFonts => _anyFace is not null;

    /// <summary>
    /// Gets the number of distinct faces that were indexed.
    /// </summary>
    public int FaceCount => _faces.Count;

    /// <summary>
    /// Installs this resolver as PDFsharp's global font resolver unless one is already set.
    /// Calling it repeatedly is safe.
    /// </summary>
    public static void Register()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = Instance;
        }
    }

    /// <inheritdoc />
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var requested = Normalize(familyName);

        if (TryResolve(requested, isBold, isItalic, out var face))
        {
            return new FontResolverInfo(face);
        }

        foreach (var fallback in FallbackFamilies)
        {
            if (!string.Equals(fallback, requested, StringComparison.Ordinal)
                && TryResolve(fallback, isBold, isItalic, out face))
            {
                return new FontResolverInfo(face);
            }
        }

        return _anyFace is null ? null : new FontResolverInfo(_anyFace);
    }

    /// <inheritdoc />
    public byte[]? GetFont(string faceName)
    {
        if (string.IsNullOrEmpty(faceName))
        {
            return null;
        }

        return _cache.GetOrAdd(faceName, static path =>
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                return [];
            }
            catch (UnauthorizedAccessException)
            {
                return [];
            }
        }) is { Length: > 0 } bytes ? bytes : null;
    }

    private bool TryResolve(string family, bool bold, bool italic, out string face)
    {
        // Exact style, then progressively relaxed: PDFsharp synthesises the missing emphasis.
        Span<FaceKey> candidates =
        [
            new(family, bold, italic),
            new(family, bold, false),
            new(family, false, italic),
            new(family, false, false),
        ];

        foreach (var candidate in candidates)
        {
            if (_faces.TryGetValue(candidate, out var path))
            {
                face = path;
                return true;
            }
        }

        face = string.Empty;
        return false;
    }

    private static Dictionary<FaceKey, string> BuildIndex()
    {
        var index = new Dictionary<FaceKey, string>();

        foreach (var directory in FontDirectories())
        {
            IEnumerable<string> files;
            try
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                files = Directory.EnumerateFiles(directory, "*.*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 4,
                    IgnoreInaccessible = true,
                });
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase))
                {
                    // Font collections (.ttc) and bitmap formats are skipped: PDFsharp cannot embed them.
                    continue;
                }

                var key = Classify(Path.GetFileNameWithoutExtension(file));
                if (key.Family.Length > 0)
                {
                    index.TryAdd(key, file);
                }
            }
        }

        return index;
    }

    private static IEnumerable<string> FontDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (windows.Length > 0)
            {
                yield return Path.Combine(windows, "Fonts");
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (localAppData.Length > 0)
            {
                yield return Path.Combine(localAppData, "Microsoft", "Windows", "Fonts");
            }

            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
            if (home.Length > 0)
            {
                yield return Path.Combine(home, "Library", "Fonts");
            }

            yield break;
        }

        yield return "/usr/share/fonts";
        yield return "/usr/local/share/fonts";
        if (home.Length > 0)
        {
            yield return Path.Combine(home, ".fonts");
            yield return Path.Combine(home, ".local", "share", "fonts");
        }
    }

    /// <summary>
    /// Derives a family key and style flags from a font file name, handling both the
    /// <c>LiberationSans-BoldItalic</c> and the compact Microsoft <c>arialbi</c> conventions.
    /// </summary>
    private static FaceKey Classify(string fileName)
    {
        var name = fileName.ToLowerInvariant();
        var bold = false;
        var italic = false;

        foreach (var suffix in StyleSuffixes)
        {
            if (!name.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            bold = suffix.Contains("bold", StringComparison.Ordinal) || suffix.Contains("semibold", StringComparison.Ordinal);
            italic = suffix.Contains("italic", StringComparison.Ordinal) || suffix.Contains("oblique", StringComparison.Ordinal);
            name = name[..^suffix.Length];
            return new FaceKey(Normalize(name), bold, italic);
        }

        if (name.EndsWith("bi", StringComparison.Ordinal) || name.EndsWith("z", StringComparison.Ordinal))
        {
            bold = true;
            italic = true;
            name = name.EndsWith("bi", StringComparison.Ordinal) ? name[..^2] : name[..^1];
        }
        else if (name.EndsWith("bd", StringComparison.Ordinal) || name.EndsWith("b", StringComparison.Ordinal))
        {
            bold = true;
            name = name.EndsWith("bd", StringComparison.Ordinal) ? name[..^2] : name[..^1];
        }
        else if (name.EndsWith('i') && name.Length > 3)
        {
            italic = true;
            name = name[..^1];
        }

        return new FaceKey(Normalize(name), bold, italic);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var buffer = new char[value.Length];
        var length = 0;
        foreach (var c in value)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                buffer[length++] = char.ToLowerInvariant(c);
            }
        }

        return new string(buffer, 0, length);
    }

    private readonly record struct FaceKey(string Family, bool Bold, bool Italic);
}
