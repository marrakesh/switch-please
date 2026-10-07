using System.Buffers;
using System.Collections.Frozen;

namespace SwitchPlease.Core.Detection;

/// <summary>
/// A compact plausibility model for one language: does this look like a real word?
///
/// Three signals, in order of usefulness:
///
/// 1. Bigram coverage. What separates a word from keyboard noise is that its letter pairs
///    are ones the language actually uses. Real words hit the common-bigram list most of
///    the time; wrong-layout text hits it rarely. This is a whitelist rather than a
///    blacklist of impossible pairs, because "unusual" is far more common than "illegal"
///    and a blacklist scores gibberish almost as highly as real words.
///
/// 2. Vowel structure. Typing Russian on a US layout produces vowel-starved latin text
///    (RU "привет" comes out as "ghbdtn"), because most Russian vowels sit on latin
///    consonants.
///
/// 3. Outright impossible pairs and a small frequent-word list, which sharpen the edges.
///
/// These lists are hand-built, which is what keeps the assembly small and dependency-free.
/// A model derived from a real corpus would score better on rare words; see
/// docs/internals.md.
/// </summary>
public sealed class LanguageProfile
{
    private const double ImpossibleBigramPenalty = 0.25;

    /// <summary>Highest score a word missing from an installed dictionary may reach.</summary>
    private const double UnrecognisedCeiling = 0.6;

    /// <summary>
    /// Where an unrecognised word sits for a language modelled only from its keyboard
    /// layout. Neutral rather than damning: the dictionary is the only evidence available,
    /// and its silence about a name or a piece of jargon proves nothing.
    /// </summary>
    private const double UnrecognisedFloorWithoutModel = 0.3;

    private static readonly char[] Trimmable = ['.', ',', '!', '?', ':', ';', '(', ')', '"'];

    private readonly SearchValues<char> _alphabet;
    private readonly StatisticalModel? _statistical;

    private LanguageProfile(
        string name,
        string languageTag,
        Script script,
        string alphabet,
        StatisticalModel? statistical)
    {
        Name = name;
        LanguageTag = languageTag;
        Script = script;
        _alphabet = SearchValues.Create(alphabet);
        _statistical = statistical;
    }

    private LanguageProfile(
        string name,
        string languageTag,
        Script script,
        string vowels,
        string alphabet,
        string commonBigrams,
        IEnumerable<string> impossibleBigrams,
        IEnumerable<string> commonWords,
        string[] commonSuffixes,
        double expectedVowelRatio)
        : this(
            name,
            languageTag,
            script,
            alphabet,
            new StatisticalModel(
                vowels,
                commonBigrams,
                impossibleBigrams,
                commonWords,
                commonSuffixes,
                expectedVowelRatio))
    {
    }

    /// <summary>
    /// Builds a profile for a language the application has no hand-written model of, using
    /// the alphabet the keyboard layout itself produces.
    ///
    /// This is what lets the switcher follow the layouts installed on the machine rather
    /// than a list baked into the code. Such a profile can say which alphabet a word belongs
    /// to but not whether it reads naturally, so it leans on a dictionary and abstains
    /// without one.
    /// </summary>
    public static LanguageProfile FromLayoutAlphabet(string name, string languageTag, Script script, string alphabet) =>
        new(name, languageTag, script, alphabet, statistical: null);

    public string Name { get; }

    /// <summary>BCP-47 tag used to look this language up in the system dictionary.</summary>
    public string LanguageTag { get; }

    public Script Script { get; }

    /// <summary>
    /// Whether this profile carries frequency data. Without it the profile can only confirm
    /// that letters belong to its alphabet, and any judgement must come from a dictionary.
    /// </summary>
    public bool HasStatisticalModel => _statistical is not null;

    /// <summary>
    /// Plausibility of <paramref name="text"/> as this language, judged on letter structure
    /// alone. Zero when the profile carries no frequency data.
    /// </summary>
    /// <param name="wordDigits">
    /// Digits to judge as part of the word, as letters this language does not have; one flag
    /// per character of <paramref name="text"/>. Otherwise a digit is passed over like a
    /// space, which is right for a number and wrong for a letter that came out as one -- see
    /// ConversionPlanner, the only caller that can tell the two apart.
    /// </param>
    public double Score(string text, bool[]? wordDigits = null)
    {
        if (_statistical is null)
        {
            return 0;
        }

        string lower = text.ToLowerInvariant();
        int letters = 0;
        int inAlphabet = 0;

        for (int i = 0; i < lower.Length; i++)
        {
            if (!IsWordCharacter(lower, i, wordDigits))
            {
                continue;
            }

            letters++;

            if (_alphabet.Contains(lower[i]))
            {
                inAlphabet++;
            }
        }

        if (letters == 0)
        {
            return 0;
        }

        double validity = (double)inAlphabet / letters;

        // Letters outside this alphabet mean the word simply is not in this language.
        if (validity < 0.6)
        {
            return validity * 0.1;
        }

        return _statistical.Score(lower, wordDigits, validity, _alphabet);
    }

    /// <summary>
    /// Plausibility with a dictionary consulted where one exists.
    ///
    /// A recognised word is certain, so it scores 1. An unrecognised one is held below
    /// <see cref="UnrecognisedCeiling"/> rather than driven to zero: dictionaries do not
    /// contain every name, abbreviation or piece of slang, and text the user typed
    /// deliberately must not be treated as noise merely for being uncommon.
    /// </summary>
    /// <param name="wordDigits">As for <see cref="Score(string, bool[])"/>.</param>
    public double Score(string text, IWordValidator validator, bool[]? wordDigits = null)
    {
        double statistical = Score(text, wordDigits);

        if (!validator.HasDictionary(LanguageTag))
        {
            return statistical;
        }

        (int known, int checkable) = CountKnownWords(text, validator);

        if (checkable == 0)
        {
            return statistical;
        }

        // A profile with no frequency data has nothing of its own to contribute, so the
        // dictionary alone decides and unknown words sit at a neutral floor.
        double floor = _statistical is null
            ? UnrecognisedFloorWithoutModel
            : Math.Min(statistical, UnrecognisedCeiling);

        double recognised = (double)known / checkable;

        // All words known lands on 1, none known stays at the floor, and a mixture sits
        // proportionally between the two.
        return floor + (recognised * (1.0 - floor));
    }

    /// <summary>Fraction of the letters in <paramref name="text"/> that this alphabet contains.</summary>
    public double AlphabetCoverage(string text)
    {
        int letters = 0;
        int covered = 0;

        foreach (char c in text.ToLowerInvariant())
        {
            if (!char.IsLetter(c))
            {
                continue;
            }

            letters++;

            if (_alphabet.Contains(c))
            {
                covered++;
            }
        }

        return letters == 0 ? 0 : (double)covered / letters;
    }

    private (int Known, int Checkable) CountKnownWords(string text, IWordValidator validator)
    {
        int known = 0;
        int checkable = 0;

        foreach (string token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string word = token.Trim(Trimmable);

            // Anything with a digit in it is an identifier or a code, not a word. Nor could
            // asking help: Windows passes every such token, "m2sto" included.
            if (word.Length < 2 || word.Any(char.IsDigit))
            {
                continue;
            }

            checkable++;

            if (validator.Check(word, LanguageTag) == WordStatus.Known)
            {
                known++;
            }
        }

        return (known, checkable);
    }

    private static bool IsWordCharacter(string lower, int index, bool[]? wordDigits) =>
        char.IsLetter(lower[index]) || (wordDigits is not null && wordDigits[index]);

    public static LanguageProfile Russian { get; } = new(
        name: "ru",
        languageTag: "ru-RU",
        script: Script.Cyrillic,
        vowels: "аеёиоуыэюя",
        alphabet: "абвгдеёжзийклмнопрстуфхцчшщъыьэюя",
        commonBigrams:
            "ав аз ак ал ам ан ап ар ас ат ах ач ая ая " +
            "бе би бл бо бр бу бы " +
            "ва ве ви вл вн во вр вс вы " +
            "га ги гл го гр гу " +
            "да де дл дн до др ду ды дь дя " +
            "ев ег ед ез ей ек ел ем ен ер ес ет ех ец еч ещ " +
            "жа жд же жи жн " +
            "за зв зд зи зн зо зы " +
            "ив из ик ил им ин ир ис ит их ич ищ ии ия ие ий " +
            "ка ке ки кл ко кр кт ку " +
            "ла ле ли лн ло лу ль ля " +
            "ма ме ми мн мо му мы " +
            "на нд не ни но нс нт ну ны ня нн " +
            "об ов ог од ож оз ой ок ол ом он оп ор ос от ох оч ош ощ " +
            "па пе пи пл по пр пт пу " +
            "ра ре ри рм рн ро рс рт ру ры рь ря " +
            "са се си ск сл см сн со сп ср ст су сь ся " +
            "та те ти тн то тр тс ту тв ть " +
            "уб ув уг уд уж уз ук ул ум ун уп ур ус ут ух уч ущ " +
            "фа фе фи фо фр " +
            "ха хи хо хр " +
            "ца це ци " +
            "ча че чи чт чо " +
            "ша ше ши шт шо " +
            "ща ще щи " +
            "ыв ыд ый ык ыл ым ын ыс ыт ых ыш " +
            "ье ьи ьк ьм ьн ьс ьт ья " +
            "эк эл эн эр эс эт " +
            "юб юг юд юз юч " +
            "яв яз як ял ям ян яс ят ях яч",
        impossibleBigrams:
        [
            // Hard or soft sign directly after a vowel.
            "аъ", "оъ", "уъ", "еъ", "иъ", "ыъ", "эъ", "юъ", "яъ", "ёъ",
            "аь", "оь", "уь", "еь", "иь", "ыь", "эь", "юь", "яь", "ёь",
            // Sibilants never take these vowels.
            "жы", "шы", "чы", "щы", "жэ", "шэ", "чэ", "щэ", "жя", "шя", "чя", "щя",
            // Velars never take Ы.
            "кы", "гы", "хы",
            // Doubled signs and semivowel.
            "ьь", "ъъ", "ьъ", "ъь", "йй", "йь", "йъ",
            // Clusters that do not occur.
            "щщ", "цщ", "щц", "фщ", "щф", "ыы", "ээ", "юю", "яя",
        ],
        commonWords:
        [
            "и", "в", "не", "на", "быть", "он", "что", "по", "это", "она",
            "этот", "но", "они", "мы", "как", "из", "который", "то", "за", "свой",
            "весь", "год", "от", "так", "для", "ты", "же", "все", "тот", "мочь", "вы",
            "человек", "такой", "его", "сказать", "только", "или", "еще", "бы", "себя",
            "один", "уже", "до", "время", "если", "сам", "когда", "другой", "вот",
            "говорить", "наш", "мой", "знать", "стать", "при", "чтобы", "дело", "жизнь",
            "кто", "первый", "очень", "два", "день", "ее", "новый", "рука", "даже", "во",
            "со", "раз", "где", "там", "под", "можно", "ну", "какой", "после", "их", "ведь",
            "хорошо", "спасибо", "привет", "пока", "да", "нет", "почему", "зачем", "надо",
            "давай", "сейчас", "сегодня", "завтра", "вчера", "работа", "делать", "хотеть",
            "думать", "видеть", "идти", "просто", "конечно", "может", "нужно", "тоже",
        ],
        commonSuffixes:
        [
            "ть", "ся", "ый", "ий", "ой", "ая", "ое", "ые", "ие", "ов", "ам", "ах",
            "ем", "ет", "ут", "ют", "ла", "ли", "но", "ка", "ость", "ение", "ция",
        ],
        expectedVowelRatio: 0.42);

    public static LanguageProfile Ukrainian { get; } = new(
        name: "uk",
        languageTag: "uk-UA",
        script: Script.Cyrillic,
        vowels: "аеєиіїоуюя",
        alphabet: "абвгґдеєжзиіїйклмнопрстуфхцчшщьюя",
        commonBigrams:
            "ав аз ак ал ам ан ап ар ас ат ах ач ая " +
            "бе би бі бл бо бр бу " +
            "ва ве ви ві вл вн во вр вс " +
            "га ги гі гл го гр ґа " +
            "да де ди ді дл дн до др ду дь " +
            "ег ед ей ек ел ем ен ер ес ет ех ец еч " +
            "єд єм єн єт єю " +
            "жа жд же жи жі " +
            "за зв зд зи зі зн зо " +
            "ив из ий ик ил им ин ир ис ит их ич иш " +
            "ів ід ій ік іл ім ін іс іт іч ія іс " +
            "ї їв їд їж їх " +
            "ка ке ки кі кл ко кр ку " +
            "ла ле ли лі ло лу ль ля " +
            "ма ме ми мі мо му " +
            "на не ни ні но нс нт ну ня нн " +
            "об ов ог од ож оз ой ок ол ом он оп ор ос от ох оч ош " +
            "па пе пи пі пл по пр пу " +
            "ра ре ри рі рм рн ро рс рт ру рь ря " +
            "са се си сі ск сл см сн со сп ср ст су сь ся " +
            "та те ти ті тн то тр тв ть ту " +
            "уб ув уг уд уж уз ук ул ум ун уп ур ус ут ух уч " +
            "фа фе фі фо фр " +
            "ха хи хі хо хр " +
            "ца це ци ці " +
            "ча че чи чі чн чт " +
            "ша ше ши ші шт " +
            "ща ще щи щі " +
            "ьк ьн ьо ьс ьт " +
            "юв юд юч " +
            "яв яз як ял ям ян яс ят ях",
        impossibleBigrams:
        [
            // Letters that belong to the Russian alphabet, not this one. Their presence is
            // the clearest sign the text was typed with the wrong Cyrillic layout.
            "ыа", "ые", "ыи", "ыо", "ыу", "аы", "ея", "ыы", "ъъ", "ээ", "ёё",
            // Soft sign after a vowel, as in Russian.
            "аь", "оь", "уь", "еь", "иь", "яь", "юь", "іь", "єь",
            // Sibilants do not take these vowels.
            "жя", "шя", "чя", "щя", "жю", "щю",
            "ьь", "йй", "йь", "щщ", "цщ", "щц",
        ],
        commonWords:
        [
            "і", "в", "не", "на", "що", "з", "я", "до", "як", "за", "а", "це", "ти", "ми",
            "ви", "він", "вона", "вони", "бути", "мати", "або", "так", "ні", "дякую",
            "будь", "ласка", "привіт", "добрий", "день", "вечір", "ранок", "час", "рік",
            "робота", "робити", "знати", "хотіти", "могти", "казати", "говорити", "бачити",
            "йти", "дуже", "тільки", "ще", "вже", "тут", "там", "де", "коли", "чому",
            "треба", "потрібно", "звичайно", "звідки", "гаразд", "добре", "погано",
            "сьогодні", "завтра", "вчора", "зараз", "потім", "разом", "також", "інший",
            "перший", "новий", "великий", "малий", "людина", "життя", "справа", "місто",
            "країна", "мова", "слово", "рука", "око", "друг", "сім'я", "дитина", "жінка",
            "чоловік", "гроші", "місце", "питання", "відповідь", "будинок", "вода", "світ",
        ],
        commonSuffixes:
        [
            "ти", "ся", "ий", "ій", "ою", "ами", "ах", "ів", "ов", "ість", "ення", "ція",
            "ський", "ська", "ське", "ла", "ли", "но", "ка", "ки", "ють", "ать", "ить",
        ],
        expectedVowelRatio: 0.43);

    public static LanguageProfile English { get; } = new(
        name: "en",
        languageTag: "en-US",
        script: Script.Latin,
        vowels: "aeiouy",
        alphabet: "abcdefghijklmnopqrstuvwxyz",
        commonBigrams:
            "th he in er an re on at en nd ti es or te of ed is it al ar " +
            "st to nt ng se ha as ou io le ve co me de hi ri ro ic ne ea " +
            "ra ce li ch ll be ma si om ur ca el ta la ns di fo ho pe ec " +
            "pr no ct us ac ot il tr ly nc et ut ss so rs un lo wa ge ie " +
            "wh ee wi em ad ol rt po we na ul ni ts mo ow pa im mi ai sh " +
            "ir su id os iv ia am fi ci vi pl ig tu ev ld ry mp fe bl ab " +
            "gh ty op sp ay ap um ke ag ph ex ff cl br au sc nn tt ck oo " +
            "ue rn rd nk ds ms ps gs bo bu by cu cr da do du fa fl fr gr " +
            "gu hu ju ki kn nu ob od og op pi pu qu red rm rp sa sk sm " +
            "sn sw tw ty ug uc ud ue ug up va vo wo ya ye yo",
        impossibleBigrams:
        [
            "jj", "qq", "vv", "ww", "xx", "yy", "hh",
            "jb", "jc", "jd", "jf", "jg", "jh", "jk", "jl", "jm", "jn", "jp", "jq", "jr",
            "js", "jt", "jv", "jw", "jx", "jz",
            "qb", "qc", "qd", "qf", "qg", "qh", "qj", "qk", "ql", "qm", "qn", "qp", "qr",
            "qs", "qt", "qv", "qw", "qx", "qy", "qz",
            "vb", "vc", "vd", "vf", "vg", "vh", "vj", "vk", "vm", "vn", "vp", "vq", "vr",
            "vt", "vw", "vx", "vz",
            "xj", "xq", "zx", "kq", "kx", "fq", "fx", "gq", "gx", "hx", "mx", "pq", "sx",
            "wq", "wv", "wx", "bq", "bx", "cj", "cv", "cx", "dx",
        ],
        commonWords:
        [
            "the", "be", "to", "of", "and", "in", "that", "have", "it", "for", "not",
            "on", "with", "he", "as", "you", "do", "at", "this", "but", "his", "by", "from",
            "they", "we", "say", "her", "she", "or", "an", "will", "my", "one", "all",
            "would", "there", "their", "what", "so", "up", "out", "if", "about", "who",
            "get", "which", "go", "me", "when", "make", "can", "like", "time", "no", "just",
            "him", "know", "take", "people", "into", "year", "your", "good", "some", "could",
            "them", "see", "other", "than", "then", "now", "look", "only", "come", "its",
            "over", "think", "also", "back", "after", "use", "two", "how", "our", "work",
            "first", "well", "way", "even", "new", "want", "because", "any", "these", "give",
            "day", "most", "us", "hello", "hi", "please", "thanks", "yes", "sorry", "code",
            "file", "data", "user", "name", "test", "type", "value", "true", "false", "null",
        ],
        commonSuffixes:
        [
            "ing", "ed", "tion", "sion", "ly", "er", "est", "ment", "ness", "able",
            "ible", "ous", "ful", "less", "ance", "ence", "ity", "ive", "ise", "ize",
        ],
        expectedVowelRatio: 0.38);

    /// <summary>
    /// The languages this application carries hand-written frequency data for. Anything
    /// else is modelled from the keyboard layout that produces it; see LanguageCatalog.
    /// </summary>
    public static IReadOnlyList<LanguageProfile> BuiltIn { get; } = [Russian, Ukrainian, English];

    /// <summary>
    /// Frequency data for one language: which letter pairs occur, which never do, and how
    /// vowel-dense its words are. Absent for layout-derived profiles, which is the whole
    /// reason it is a separate object rather than fields on the profile.
    /// </summary>
    private sealed class StatisticalModel
    {
        private readonly SearchValues<char> _vowels;
        private readonly FrozenSet<string> _commonBigrams;
        private readonly FrozenSet<string> _impossibleBigrams;
        private readonly FrozenSet<string> _commonWords;
        private readonly string[] _commonSuffixes;
        private readonly double _expectedVowelRatio;

        internal StatisticalModel(
            string vowels,
            string commonBigrams,
            IEnumerable<string> impossibleBigrams,
            IEnumerable<string> commonWords,
            string[] commonSuffixes,
            double expectedVowelRatio)
        {
            _vowels = SearchValues.Create(vowels);
            _commonBigrams = commonBigrams
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToFrozenSet(StringComparer.Ordinal);
            _impossibleBigrams = impossibleBigrams.ToFrozenSet(StringComparer.Ordinal);
            _commonWords = commonWords.ToFrozenSet(StringComparer.Ordinal);
            _commonSuffixes = commonSuffixes;
            _expectedVowelRatio = expectedVowelRatio;
        }

        internal double Score(string lower, bool[]? wordDigits, double validity, SearchValues<char> alphabet)
        {
            if (_commonWords.Contains(lower))
            {
                return 1.0;
            }

            int letters = 0;
            int vowels = 0;

            for (int i = 0; i < lower.Length; i++)
            {
                if (!IsWordCharacter(lower, i, wordDigits))
                {
                    continue;
                }

                letters++;

                if (alphabet.Contains(lower[i]) && _vowels.Contains(lower[i]))
                {
                    vowels++;
                }
            }

            AnalyseBigrams(lower, wordDigits, out double coverage, out int impossible);

            double vowelScore = ScoreVowels(vowels, letters);
            double suffixBonus = HasCommonSuffix(lower) ? 0.10 : 0.0;

            double score = (validity * ((0.30 * vowelScore) + (0.60 * coverage) + 0.10))
                - (impossible * ImpossibleBigramPenalty)
                + suffixBonus;

            return Math.Clamp(score, 0.0, 1.0);
        }

        private double ScoreVowels(int vowels, int letters)
        {
            // A word with no vowels at all is decisive, and it is exactly what wrong-layout
            // text tends to look like.
            if (vowels == 0)
            {
                return letters >= 4 ? 0.0 : 0.15;
            }

            double ratio = (double)vowels / letters;
            double deviation = Math.Abs(ratio - _expectedVowelRatio) / _expectedVowelRatio;
            return Math.Clamp(1.0 - deviation, 0.0, 1.0);
        }

        private void AnalyseBigrams(string lower, bool[]? wordDigits, out double coverage, out int impossible)
        {
            impossible = 0;

            if (lower.Length < 2)
            {
                coverage = 0.5;
                return;
            }

            int considered = 0;
            int known = 0;

            for (int i = 0; i < lower.Length - 1; i++)
            {
                // A pair with a word digit in it is considered and is never a common one:
                // "m2" and "2s" are not pairs any language uses.
                if (!IsWordCharacter(lower, i, wordDigits) || !IsWordCharacter(lower, i + 1, wordDigits))
                {
                    continue;
                }

                string pair = lower.Substring(i, 2);
                considered++;

                if (_commonBigrams.Contains(pair))
                {
                    known++;
                }

                if (_impossibleBigrams.Contains(pair))
                {
                    impossible++;
                }
            }

            coverage = considered == 0 ? 0.5 : (double)known / considered;
        }

        private bool HasCommonSuffix(string lower)
        {
            foreach (string suffix in _commonSuffixes)
            {
                if (lower.Length > suffix.Length && lower.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
