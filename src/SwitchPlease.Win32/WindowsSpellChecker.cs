using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SwitchPlease.Core.Detection;

namespace SwitchPlease.Win32;

/// <summary>
/// Answers "is this a real word?" using the spell-checking service Windows has shipped
/// since Windows 8.
///
/// This is what makes Russian and Ukrainian separable. Their layouts differ by three keys,
/// so "привіт" typed on the Russian layout becomes "привыт" -- flawless-looking Russian that
/// no statistical model will reject. The dictionary simply says no such word exists.
///
/// Which languages are available depends on what the user has installed, so every lookup
/// degrades to "no opinion" rather than failing, and the statistical model carries on alone.
/// </summary>
public sealed class WindowsSpellChecker : IWordValidator
{
    private static readonly Guid SpellCheckerFactoryClsid = new("7AB36653-1796-484B-BDFA-E74F1DB7C1DC");

    /// <summary>Bounded so a long session cannot grow it without limit.</summary>
    private const int MaximumCachedWords = 20_000;

    private readonly ConcurrentDictionary<(string Word, string Language), WordStatus> _cache = new();
    private readonly Dictionary<string, bool> _availability = [];

    private IReadOnlyList<string>? _supportedLanguages;
    private readonly Lock _availabilityGate = new();

    // The COM objects have thread affinity, and both the worker and the UI thread can ask
    // for a score. Giving each thread its own avoids apartment marshalling entirely.
    [ThreadStatic]
    private static Dictionary<string, ISpellChecker?>? _checkers;

    [ThreadStatic]
    private static ISpellCheckerFactory? _factory;

    public bool HasDictionary(string languageTag)
    {
        // A layout whose language Windows mislabels is deliberately given no tag, because
        // consulting some other language's dictionary for it would be worse than having none.
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return false;
        }

        lock (_availabilityGate)
        {
            if (_availability.TryGetValue(languageTag, out bool cached))
            {
                return cached;
            }
        }

        bool supported = QuerySupport(languageTag);

        lock (_availabilityGate)
        {
            _availability[languageTag] = supported;
        }

        return supported;
    }

    public WordStatus Check(string word, string languageTag)
    {
        if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(languageTag))
        {
            return WordStatus.NoDictionary;
        }

        var key = (word, languageTag);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var status = Lookup(word, languageTag);

        if (_cache.Count < MaximumCachedWords)
        {
            _cache[key] = status;
        }

        return status;
    }

    /// <summary>
    /// Language tags the machine can actually spell-check, for the diagnostics view.
    ///
    /// Cached: the answer only changes when a language is added to Windows, and finding it
    /// out means walking a COM enumerator that the status window would otherwise re-walk
    /// every time it is opened.
    /// </summary>
    public IReadOnlyList<string> SupportedLanguages() => _supportedLanguages ??= QuerySupportedLanguages();

    private static List<string> QuerySupportedLanguages()
    {
        try
        {
            var factory = Factory();

            if (factory is null)
            {
                return [];
            }

            factory.get_SupportedLanguages(out var enumerator);

            var result = new List<string>();
            var buffer = new string[1];

            while (enumerator.Next(1, buffer, out int fetched) == 0 && fetched == 1)
            {
                result.Add(buffer[0]);
            }

            return result;
        }
        catch (COMException)
        {
            return [];
        }
        catch (InvalidCastException)
        {
            return [];
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    private static WordStatus Lookup(string word, string languageTag)
    {
        var checker = CheckerFor(languageTag);

        if (checker is null)
        {
            return WordStatus.NoDictionary;
        }

        try
        {
            checker.Check(word, out var errors);

            // An empty error enumeration means the dictionary recognised the word.
            return errors.Next(out _) == 0 ? WordStatus.Unknown : WordStatus.Known;
        }
        catch (COMException)
        {
            return WordStatus.NoDictionary;
        }
    }

    private static ISpellChecker? CheckerFor(string languageTag)
    {
        _checkers ??= [];

        if (_checkers.TryGetValue(languageTag, out var existing))
        {
            return existing;
        }

        ISpellChecker? checker = null;

        try
        {
            var factory = Factory();

            if (factory is not null)
            {
                factory.IsSupported(languageTag, out int supported);

                if (supported != 0)
                {
                    factory.CreateSpellChecker(languageTag, out checker);
                }
            }
        }
        catch (COMException)
        {
            checker = null;
        }
        catch (InvalidCastException)
        {
            checker = null;
        }
        catch (ArgumentException)
        {
            checker = null;
        }

        _checkers[languageTag] = checker;
        return checker;
    }

    private static bool QuerySupport(string languageTag)
    {
        try
        {
            var factory = Factory();

            if (factory is null)
            {
                return false;
            }

            factory.IsSupported(languageTag, out int supported);
            return supported != 0;
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ISpellCheckerFactory? Factory()
    {
        if (_factory is not null)
        {
            return _factory;
        }

        var type = Type.GetTypeFromCLSID(SpellCheckerFactoryClsid);

        if (type is null)
        {
            return null;
        }

        // Older or stripped-down Windows installations have no spell-checking service.
        _factory = Activator.CreateInstance(type) as ISpellCheckerFactory;
        return _factory;
    }
}

[ComImport]
[Guid("8E018A9D-2415-4677-BF08-794EA61F94BB")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISpellCheckerFactory
{
    void get_SupportedLanguages([MarshalAs(UnmanagedType.Interface)] out IEnumString value);

    void IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag, out int value);

    void CreateSpellChecker(
        [MarshalAs(UnmanagedType.LPWStr)] string languageTag,
        [MarshalAs(UnmanagedType.Interface)] out ISpellChecker value);
}

[ComImport]
[Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISpellChecker
{
    void get_LanguageTag([MarshalAs(UnmanagedType.LPWStr)] out string value);

    void Check(
        [MarshalAs(UnmanagedType.LPWStr)] string text,
        [MarshalAs(UnmanagedType.Interface)] out IEnumSpellingError value);
}

[ComImport]
[Guid("803E3BD4-2828-4410-8290-418D1D73C762")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumSpellingError
{
    [PreserveSig]
    int Next([MarshalAs(UnmanagedType.Interface)] out ISpellingError value);
}

[ComImport]
[Guid("B7C82D61-FBE8-4B47-9B27-6C0D2E0DE0A3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISpellingError
{
    void get_StartIndex(out uint value);
}

[ComImport]
[Guid("00000101-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumString
{
    [PreserveSig]
    int Next(
        int celt,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 0)] string[] rgelt,
        out int pceltFetched);
}
