namespace SwitchPlease.Core.Localization;

/// <summary>
/// One interface language: how the setting stores it, how Windows identifies it, and its
/// strings.
/// </summary>
/// <param name="Tag">Two-letter code written to settings.json, e.g. "uk".</param>
/// <param name="WindowsPrimaryLanguage">
/// Low ten bits of the Windows LANGID for this language, used to follow the display language.
/// </param>
public sealed record UiLanguage(string Tag, int WindowsPrimaryLanguage, UiStrings Strings)
{
    /// <summary>The language written in itself, for the menu.</summary>
    public string Name => Strings.LanguageName;
}

/// <summary>
/// The shipped translations. English is the reference: when a string changes, change it here
/// first, and the compiler will point at every translation that still needs updating.
///
/// Adding a language means adding a <see cref="UiStrings"/> block below and one line to
/// <see cref="All"/>. Nothing outside this file knows which languages exist -- the menu, the
/// setting, the Windows display-language check and the translation tests all read that list.
/// </summary>
public static class Translations
{
    public static UiStrings English { get; } = new()
    {
        LanguageName = "English",

        MenuEnabled = "Enabled",
        MenuAutoDetect = "Detect wrong layout automatically",
        MenuConvertWord = "Fix last word ({0})",
        MenuConvertLine = "Fix selection or whole line ({0})",
        MenuHotkeys = "Hotkeys...",
        MenuSound = "Sound when correcting",
        MenuStartup = "Start with Windows",
        MenuDiagnostics = "Measure hook latency",
        MenuStatus = "Status and latency...",
        MenuRefreshLayouts = "Refresh layout list",
        MenuOpenSettings = "Open settings folder",
        MenuLanguage = "Language",
        MenuLanguageAuto = "Same as Windows",
        MenuAbout = "About...",
        MenuExit = "Exit",

        TooltipActive = "Switch Please - {0} | {1} word, {2} line",
        TooltipDisabled = "Switch Please - disabled",

        StatusTitle = "Switch Please - status",
        StatusHook = "Keyboard hook: {0}",
        StatusInstalled = "installed",
        StatusHookRecovered = "  Windows dropped it {0} time(s); it was put back each time.",
        StatusNotInstalled = "NOT installed",
        StatusProcessing = "Input processing: {0}",
        StatusAutoDetect = "Automatic detection: {0}",
        StatusOn = "on",
        StatusOff = "off",
        StatusLayouts = "Layouts:",
        StatusActiveMarker = " <- active",
        StatusMaps = "Character tables from the active layout:",
        StatusMapLine = "  {0} -> {1}: {2} characters",
        StatusDictionaries = "Windows dictionaries: {0}",
        StatusDictionariesNone = "none (detection relies on statistics alone)",
        StatusLanguages = "Languages modelled: {0}",
        StatusLatencyTitle = "Hook callback latency:",
        StatusLatencySamples = "  samples:  {0}",
        StatusLatencyAverage = "  average:  {0:F1} us",
        StatusLatencyMaximum = "  maximum:  {0:F1} us",
        StatusLatencyDropped = "  events dropped: {0}",
        StatusLatencyLimit = "The Windows limit is 300000 us (LowLevelHooksTimeout).",
        StatusLatencyDisabled = "Latency measurement is off. Switch it on in the menu, type\nsomething, then open this window again.",
        StatusRecentEvents = "Recent events:",

        HotkeysTitle = "Switch Please - hotkeys",
        HotkeysWord = "Fix word:",
        HotkeysSelection = "Fix selection:",
        HotkeysPress = "Press a combination...",
        HotkeysHint = "Click a field, then press the combination you want; Backspace clears one. "
            + "Double-tapping Shift or Ctrl works too, and works on any keyboard. Not Alt: "
            + "on its own it opens the menu bar of whatever window you are in.",
        HotkeysDefaults = "Defaults",
        HotkeysDuplicate = "Both commands are on the same combination. Choose different ones.",

        AboutTitle = "About Switch Please",
        AboutTagline = "Fixes text typed in the wrong keyboard layout.",
        AboutVersion = "Version {0}",
        AboutLicense = "MIT License",
        AboutSource = "Source code",
        AboutDonate = "Support the project",

        ButtonOk = "OK",
        ButtonCancel = "Cancel",
        ButtonClose = "Close",

        ErrorHookFailed = "Could not install the keyboard hook.\n\n{0}",
        ErrorStartup = "Could not change the startup setting.\n\n{0}",
        ErrorOpenFolder = "Could not open the folder.\n\n{0}",
        ErrorAlreadyRunning = "Switch Please is already running - look for the tray icon.",
        ErrorCrash = "Switch Please ran into an error:\n\n{0}\n\nDetails: {1}",

        MenuUndo = "Undo last correction ({0})",
        MenuExclude = "Never run in {0}",
        MenuInclude = "Run in {0} again",
        MenuExcludeUnknown = "Never run in this application",
        MenuSettings = "Settings...",
        MenuCheckUpdates = "Check for updates...",

        SettingsTitle = "Switch Please - settings",
        SettingsSensitivity = "Caution when correcting automatically:",
        SettingsSensitivityHint = "Higher means the other reading must be clearly better before a word is rewritten.",
        SettingsDelay = "Pause before rewriting, ms:",
        SettingsDelayHint = "The key that ended the word is still on its way to the application.",
        SettingsTapWindow = "Double tap - longest gap, ms:",
        SettingsTapHold = "Double tap - longest press, ms:",
        SettingsMinimumWord = "Shortest word to correct:",
        SettingsPasswordFields = "Stay out of password fields",
        SettingsLogText = "Write the typed text to the log",
        SettingsLogTextHint = "Off by default: with it on, the log records everything typed while diagnostics are running.",
        SettingsUpdates = "Look for a newer version at startup",
        SettingsExcluded = "Never run in these applications:",
        SettingsExcludedHint = "One executable name per line, e.g. keepass.exe",

        UpdateTitle = "Switch Please - updates",
        UpdateAvailable = "Version {0} is available. You are running {1}.\n\nOpen the download page?",
        UpdateCurrent = "You are running the newest version ({0}).",
        UpdateFailed = "Could not check for updates.\n\n{0}",

        BlockedTitle = "Corrections are not getting through",
        BlockedBody = "The focused window belongs to a program running as administrator. Windows will not accept input from Switch Please into it.",

        StatusCopy = "Copy",
        HotkeysUndo = "Undo:",

        SettingsFullscreen = "Stand aside while a game or a presentation is on screen",
        SettingsFullscreenHint = "In most games Shift is sprint, and tapping it twice is what running feels like. Without this the hotkey fires while you play.",

        SettingsTypewriter = "Type corrections one character at a time",
        SettingsTypewriterHint = "A typewriter effect. Off by default: it adds time to the one operation that is meant to finish before you can type over it.",

        WelcomeTitle = "Switch Please",
        WelcomeHeading = "Typed it in the wrong layout?",
        WelcomeBody = "The icon below is Switch Please. Not there? Look under the ^ arrow.",
        WelcomeActionWord = "fixes the last word",
        WelcomeActionSelection = "fixes the selection, or the whole line",

        SettingsGroupCorrection = "Automatic correction",
        SettingsGroupHotkeys = "Double tap",
        SettingsGroupPrivacy = "Privacy and safety",
        SettingsGroupApplications = "Applications",
        WelcomeDemoTyped = "руддщ цщкдв",
        WelcomeDemoFixed = "hello world",

        MenuAutoDetectHere = "Correct automatically in {0}",

        StatusWorker = "Worker thread: {0}",
        StatusWorkerRunning = "running",
        StatusWorkerStopped = "STOPPED",
        StatusQueue = "Keystrokes waiting: {0}, last handled {1:F1} s ago",
        StatusCorrections = "Corrections this session: {0}",
        StatusSlowest = "Slowest keystroke to handle: {0} ms",
    };

    public static UiStrings Russian { get; } = new()
    {
        LanguageName = "Русский",

        MenuEnabled = "Включено",
        MenuAutoDetect = "Определять раскладку автоматически",
        MenuConvertWord = "Исправить последнее слово ({0})",
        MenuConvertLine = "Исправить выделение или всю строку ({0})",
        MenuHotkeys = "Горячие клавиши...",
        MenuSound = "Звук при исправлении",
        MenuStartup = "Запускать при входе в Windows",
        MenuDiagnostics = "Замерять задержку хука",
        MenuStatus = "Состояние и задержка...",
        MenuRefreshLayouts = "Обновить список раскладок",
        MenuOpenSettings = "Открыть папку настроек",
        MenuLanguage = "Язык",
        MenuLanguageAuto = "Как в Windows",
        MenuAbout = "О программе...",
        MenuExit = "Выход",

        TooltipActive = "Switch Please - {0} | {1} слово, {2} строка",
        TooltipDisabled = "Switch Please - выключено",

        StatusTitle = "Switch Please - состояние",
        StatusHook = "Клавиатурный хук: {0}",
        StatusInstalled = "установлен",
        StatusHookRecovered = "  Windows сбрасывала его {0} раз(а); каждый раз он ставился заново.",
        StatusNotInstalled = "НЕ установлен",
        StatusProcessing = "Обработка ввода: {0}",
        StatusAutoDetect = "Автоопределение: {0}",
        StatusOn = "включено",
        StatusOff = "выключено",
        StatusLayouts = "Раскладки:",
        StatusActiveMarker = " <- активная",
        StatusMaps = "Таблицы соответствия от активной раскладки:",
        StatusMapLine = "  {0} -> {1}: {2} символов",
        StatusDictionaries = "Словари Windows: {0}",
        StatusDictionariesNone = "нет (детекция работает только на статистике)",
        StatusLanguages = "Языки в модели: {0}",
        StatusLatencyTitle = "Задержка callback-а хука:",
        StatusLatencySamples = "  замеров:  {0}",
        StatusLatencyAverage = "  средняя:  {0:F1} мкс",
        StatusLatencyMaximum = "  максимум: {0:F1} мкс",
        StatusLatencyDropped = "  потеряно событий: {0}",
        StatusLatencyLimit = "Лимит Windows - 300000 мкс (LowLevelHooksTimeout).",
        StatusLatencyDisabled = "Замер задержки выключен. Включите его в меню, наберите текст,\nзатем откройте это окно снова.",
        StatusRecentEvents = "Последние события:",

        HotkeysTitle = "Switch Please - горячие клавиши",
        HotkeysWord = "Исправить слово:",
        HotkeysSelection = "Исправить выделение:",
        HotkeysPress = "Нажмите комбинацию...",
        HotkeysHint = "Нажмите на поле, затем нажмите нужную комбинацию; Backspace снимает её. "
            + "Двойное нажатие Shift или Ctrl тоже подойдёт и работает на любой клавиатуре. "
            + "Alt - нет: сам по себе он открывает меню того окна, где вы находитесь.",
        HotkeysDefaults = "По умолчанию",
        HotkeysDuplicate = "Обе команды назначены на одну комбинацию. Выберите разные.",

        AboutTitle = "О программе Switch Please",
        AboutTagline = "Исправляет текст, набранный не в той раскладке.",
        AboutVersion = "Версия {0}",
        AboutLicense = "Лицензия MIT",
        AboutSource = "Исходный код",
        AboutDonate = "Поддержать проект",

        ButtonOk = "OK",
        ButtonCancel = "Отмена",
        ButtonClose = "Закрыть",

        ErrorHookFailed = "Не удалось установить клавиатурный хук.\n\n{0}",
        ErrorStartup = "Не удалось изменить автозапуск.\n\n{0}",
        ErrorOpenFolder = "Не удалось открыть папку.\n\n{0}",
        ErrorAlreadyRunning = "Switch Please уже запущен - значок в трее.",
        ErrorCrash = "Switch Please столкнулся с ошибкой:\n\n{0}\n\nПодробности: {1}",

        MenuUndo = "Отменить последнее исправление ({0})",
        MenuExclude = "Не работать в {0}",
        MenuInclude = "Снова работать в {0}",
        MenuExcludeUnknown = "Не работать в этом приложении",
        MenuSettings = "Настройки...",
        MenuCheckUpdates = "Проверить обновления...",

        SettingsTitle = "Switch Please - настройки",
        SettingsSensitivity = "Осторожность при автоисправлении:",
        SettingsSensitivityHint = "Чем больше, тем заметнее должен выигрывать другой вариант, чтобы слово переписали.",
        SettingsDelay = "Пауза перед заменой, мс:",
        SettingsDelayHint = "Клавиша, завершившая слово, ещё летит в приложение.",
        SettingsTapWindow = "Двойное нажатие - макс. пауза, мс:",
        SettingsTapHold = "Двойное нажатие - макс. удержание, мс:",
        SettingsMinimumWord = "Минимальная длина слова:",
        SettingsPasswordFields = "Не трогать поля с паролями",
        SettingsLogText = "Писать набранный текст в лог",
        SettingsLogTextHint = "По умолчанию выключено: иначе лог запишет всё, что набрано при включённой диагностике.",
        SettingsUpdates = "Искать новую версию при запуске",
        SettingsExcluded = "Не работать в этих приложениях:",
        SettingsExcludedHint = "По одному имени файла в строке, например keepass.exe",

        UpdateTitle = "Switch Please - обновления",
        UpdateAvailable = "Доступна версия {0}. У вас {1}.\n\nОткрыть страницу загрузки?",
        UpdateCurrent = "У вас самая свежая версия ({0}).",
        UpdateFailed = "Не удалось проверить обновления.\n\n{0}",

        BlockedTitle = "Исправления не доходят",
        BlockedBody = "Активное окно принадлежит программе, запущенной от администратора. Windows не пропускает в неё ввод от Switch Please.",

        StatusCopy = "Копировать",
        HotkeysUndo = "Отменить:",

        SettingsFullscreen = "Не работать, пока на экране игра или презентация",
        SettingsFullscreenHint = "В большинстве игр Shift - это спринт, и нажать его дважды подряд там совершенно обычно. Без этого хоткей срабатывает прямо во время игры.",

        SettingsTypewriter = "Печатать исправление посимвольно",
        SettingsTypewriterHint = "Эффект печатной машинки. По умолчанию выключено: он добавляет время операции, которая должна успеть закончиться раньше, чем вы наберёте что-то поверх.",

        WelcomeTitle = "Switch Please",
        WelcomeHeading = "Набрали не в той раскладке?",
        WelcomeBody = "Значок внизу - это Switch Please. Не видно? Посмотрите под стрелкой ^.",
        WelcomeActionWord = "исправляет последнее слово",
        WelcomeActionSelection = "исправляет выделение или всю строку",

        SettingsGroupCorrection = "Автоисправление",
        SettingsGroupHotkeys = "Двойное нажатие",
        SettingsGroupPrivacy = "Приватность и безопасность",
        SettingsGroupApplications = "Приложения",
        WelcomeDemoTyped = "ghbdtn rfr ltkf",
        WelcomeDemoFixed = "привет как дела",

        MenuAutoDetectHere = "Исправлять автоматически в {0}",

        StatusWorker = "Рабочий поток: {0}",
        StatusWorkerRunning = "работает",
        StatusWorkerStopped = "ОСТАНОВЛЕН",
        StatusQueue = "Нажатий в очереди: {0}, последнее обработано {1:F1} с назад",
        StatusCorrections = "Исправлений за сессию: {0}",
        StatusSlowest = "Самое долгое нажатие: {0} мс",
    };

    public static UiStrings Ukrainian { get; } = new()
    {
        LanguageName = "Українська",

        MenuEnabled = "Увімкнено",
        MenuAutoDetect = "Визначати розкладку автоматично",
        MenuConvertWord = "Виправити останнє слово ({0})",
        MenuConvertLine = "Виправити виділення або весь рядок ({0})",
        MenuHotkeys = "Гарячі клавіші...",
        MenuSound = "Звук під час виправлення",
        MenuStartup = "Запускати разом із Windows",
        MenuDiagnostics = "Вимірювати затримку хука",
        MenuStatus = "Стан і затримка...",
        MenuRefreshLayouts = "Оновити список розкладок",
        MenuOpenSettings = "Відкрити теку налаштувань",
        MenuLanguage = "Мова",
        MenuLanguageAuto = "Як у Windows",
        MenuAbout = "Про програму...",
        MenuExit = "Вихід",

        TooltipActive = "Switch Please - {0} | {1} слово, {2} рядок",
        TooltipDisabled = "Switch Please - вимкнено",

        StatusTitle = "Switch Please - стан",
        StatusHook = "Клавіатурний хук: {0}",
        StatusInstalled = "встановлено",
        StatusHookRecovered = "  Windows скидала його {0} раз(и); щоразу його встановлено заново.",
        StatusNotInstalled = "НЕ встановлено",
        StatusProcessing = "Обробка вводу: {0}",
        StatusAutoDetect = "Автовизначення: {0}",
        StatusOn = "увімкнено",
        StatusOff = "вимкнено",
        StatusLayouts = "Розкладки:",
        StatusActiveMarker = " <- активна",
        StatusMaps = "Таблиці відповідності від активної розкладки:",
        StatusMapLine = "  {0} -> {1}: {2} символів",
        StatusDictionaries = "Словники Windows: {0}",
        StatusDictionariesNone = "немає (визначення працює лише на статистиці)",
        StatusLanguages = "Мови в моделі: {0}",
        StatusLatencyTitle = "Затримка callback-а хука:",
        StatusLatencySamples = "  вимірів:  {0}",
        StatusLatencyAverage = "  середня:  {0:F1} мкс",
        StatusLatencyMaximum = "  максимум: {0:F1} мкс",
        StatusLatencyDropped = "  втрачено подій: {0}",
        StatusLatencyLimit = "Ліміт Windows - 300000 мкс (LowLevelHooksTimeout).",
        StatusLatencyDisabled = "Вимірювання затримки вимкнено. Увімкніть його в меню, наберіть\nтекст, потім відкрийте це вікно знову.",
        StatusRecentEvents = "Останні події:",

        HotkeysTitle = "Switch Please - гарячі клавіші",
        HotkeysWord = "Виправити слово:",
        HotkeysSelection = "Виправити виділення:",
        HotkeysPress = "Натисніть комбінацію...",
        HotkeysHint = "Натисніть на поле, потім натисніть потрібну комбінацію; Backspace знімає її. "
            + "Подвійне натискання Shift або Ctrl теж підійде і працює на будь-якій клавіатурі. "
            + "Alt - ні: сам по собі він відкриває меню того вікна, де ви перебуваєте.",
        HotkeysDefaults = "За замовчуванням",
        HotkeysDuplicate = "Обидві команди призначено на одну комбінацію. Виберіть різні.",

        AboutTitle = "Про програму Switch Please",
        AboutTagline = "Виправляє текст, набраний не в тій розкладці.",
        AboutVersion = "Версія {0}",
        AboutLicense = "Ліцензія MIT",
        AboutSource = "Вихідний код",
        AboutDonate = "Підтримати проєкт",

        ButtonOk = "OK",
        ButtonCancel = "Скасувати",
        ButtonClose = "Закрити",

        ErrorHookFailed = "Не вдалося встановити клавіатурний хук.\n\n{0}",
        ErrorStartup = "Не вдалося змінити автозапуск.\n\n{0}",
        ErrorOpenFolder = "Не вдалося відкрити теку.\n\n{0}",
        ErrorAlreadyRunning = "Switch Please вже запущено - значок у треї.",
        ErrorCrash = "Switch Please натрапив на помилку:\n\n{0}\n\nПодробиці: {1}",

        MenuUndo = "Скасувати останнє виправлення ({0})",
        MenuExclude = "Не працювати в {0}",
        MenuInclude = "Знову працювати в {0}",
        MenuExcludeUnknown = "Не працювати в цій програмі",
        MenuSettings = "Налаштування...",
        MenuCheckUpdates = "Перевірити оновлення...",

        SettingsTitle = "Switch Please - налаштування",
        SettingsSensitivity = "Обережність при автовиправленні:",
        SettingsSensitivityHint = "Що більше, то помітніше має вигравати інший варіант, щоб слово переписали.",
        SettingsDelay = "Пауза перед заміною, мс:",
        SettingsDelayHint = "Клавіша, що завершила слово, ще летить до програми.",
        SettingsTapWindow = "Подвійне натискання - макс. пауза, мс:",
        SettingsTapHold = "Подвійне натискання - макс. утримання, мс:",
        SettingsMinimumWord = "Найкоротше слово для виправлення:",
        SettingsPasswordFields = "Не чіпати поля з паролями",
        SettingsLogText = "Записувати набраний текст до журналу",
        SettingsLogTextHint = "Типово вимкнено: інакше журнал запише все, що набрано при увімкненій діагностиці.",
        SettingsUpdates = "Шукати новішу версію під час запуску",
        SettingsExcluded = "Не працювати в цих програмах:",
        SettingsExcludedHint = "По одному імені файлу в рядку, наприклад keepass.exe",

        UpdateTitle = "Switch Please - оновлення",
        UpdateAvailable = "Доступна версія {0}. У вас {1}.\n\nВідкрити сторінку завантаження?",
        UpdateCurrent = "У вас найновіша версія ({0}).",
        UpdateFailed = "Не вдалося перевірити оновлення.\n\n{0}",

        BlockedTitle = "Виправлення не доходять",
        BlockedBody = "Активне вікно належить програмі, запущеній від адміністратора. Windows не пропускає до неї ввід від Switch Please.",

        StatusCopy = "Копіювати",
        HotkeysUndo = "Скасувати:",

        SettingsFullscreen = "Не працювати, поки на екрані гра або презентація",
        SettingsFullscreenHint = "У більшості ігор Shift - це спринт, і натиснути його двічі поспіль там цілком звично. Без цього гаряча клавіша спрацьовує просто під час гри.",

        SettingsTypewriter = "Друкувати виправлення посимвольно",
        SettingsTypewriterHint = "Ефект друкарської машинки. Типово вимкнено: він додає час операції, яка має встигнути завершитися раніше, ніж ви наберете щось поверх.",

        WelcomeTitle = "Switch Please",
        WelcomeHeading = "Набрали не в тій розкладці?",
        WelcomeBody = "Значок унизу - це Switch Please. Не видно? Погляньте під стрілкою ^.",
        WelcomeActionWord = "виправляє останнє слово",
        WelcomeActionSelection = "виправляє виділення або весь рядок",

        SettingsGroupCorrection = "Автовиправлення",
        SettingsGroupHotkeys = "Подвійне натискання",
        SettingsGroupPrivacy = "Приватність і безпека",
        SettingsGroupApplications = "Програми",
        WelcomeDemoTyped = "ghbdsn zr cghfdb",
        WelcomeDemoFixed = "привіт як справи",

        MenuAutoDetectHere = "Виправляти автоматично в {0}",

        StatusWorker = "Робочий потік: {0}",
        StatusWorkerRunning = "працює",
        StatusWorkerStopped = "ЗУПИНЕНО",
        StatusQueue = "Натискань у черзі: {0}, останнє оброблено {1:F1} с тому",
        StatusCorrections = "Виправлень за сесію: {0}",
        StatusSlowest = "Найдовше натискання: {0} мс",
    };

    /// <summary>
    /// Every shipped language, English first: it is the reference the tests compare the rest
    /// against, and the fallback when Windows is in a language that is not here.
    ///
    /// Declared after the translations it names, because a static initialiser runs in
    /// declaration order and would otherwise capture them before they exist.
    /// </summary>
    public static IReadOnlyList<UiLanguage> All { get; } =
    [
        new("en", 0x09, English),
        new("ru", 0x19, Russian),
        new("uk", 0x22, Ukrainian),
    ];

    /// <summary>What the interface falls back to.</summary>
    public static UiLanguage Default => All[0];

    /// <summary>Finds a language by its tag, or null. Case and whitespace are forgiven.</summary>
    public static UiLanguage? Find(string? tag)
    {
        string wanted = tag?.Trim() ?? string.Empty;

        foreach (var language in All)
        {
            if (string.Equals(language.Tag, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return language;
            }
        }

        return null;
    }

    /// <summary>Finds the language matching a Windows LANGID's primary part, or null.</summary>
    public static UiLanguage? ForWindowsLanguage(int primaryLanguage)
    {
        foreach (var language in All)
        {
            if (language.WindowsPrimaryLanguage == primaryLanguage)
            {
                return language;
            }
        }

        return null;
    }
}
