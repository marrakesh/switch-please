namespace SwitchPlease.Core.Tests;

/// <summary>
/// A larger sample to measure the detector against.
///
/// The original hundred words were enough to show the model works and not enough to trust a
/// percentage from. These are ordinary words and real sentences, none of them on the model's
/// frequent-word list, so what is measured is the letter statistics generalising rather than
/// a lookup succeeding.
///
/// The sentences matter as much as the words: automatic correction fires at the end of every
/// word in a line, so a rate that looks fine word by word can still mean one mangled word in
/// every other paragraph.
/// </summary>
internal static class DetectionCorpus
{
    public static readonly string[] Russian =
    [
        "компьютер", "программа", "клавиатура", "раскладка", "сообщение", "вопрос", "ответ",
        "встреча", "договор", "задача", "проект", "команда", "разработка", "тестирование",
        "документ", "письмо", "телефон", "адрес", "город", "улица", "машина", "дорога",
        "погода", "деньги", "неделя", "месяц", "утро", "вечер", "друг", "семья", "школа",
        "университет", "компания", "клиент", "продукт", "сервис", "система", "данные",
        "картинка", "музыка", "фотография", "разговор", "решение", "проблема", "ошибка",
        "версия", "страница", "запрос", "правило", "порядок", "библиотека", "магазин",
        "аптека", "больница", "поликлиника", "вокзал", "аэропорт", "самолёт", "поезд",
        "автобус", "трамвай", "метро", "билет", "багаж", "гостиница", "квартира", "комната",
        "кухня", "окно", "дверь", "лестница", "потолок", "стена", "мебель", "кресло",
        "диван", "полка", "лампа", "зеркало", "ковёр", "занавеска", "холодильник", "чайник",
        "тарелка", "чашка", "ложка", "вилка", "нож", "кастрюля", "сковорода", "духовка",
        "завтрак", "обед", "ужин", "хлеб", "молоко", "масло", "сахар", "соль", "перец",
        "картошка", "морковь", "капуста", "огурец", "помидор", "яблоко", "апельсин",
        "виноград", "клубника", "малина", "черника", "орехи", "печенье", "конфета",
        "шоколад", "мороженое", "варенье", "компот", "напиток", "бутылка", "стакан",
        "погоду", "зима", "весна", "лето", "осень", "снег", "дождь", "ветер", "туман",
        "гроза", "молния", "радуга", "облако", "солнце", "звезда", "планета", "космос",
        "природа", "лес", "поле", "река", "озеро", "море", "океан", "гора", "долина",
        "остров", "берег", "песок", "камень", "дерево", "трава", "цветок", "листья",
        "корень", "ветка", "птица", "рыба", "кошка", "собака", "лошадь", "корова", "овца",
        "медведь", "волк", "лиса", "заяц", "белка", "ёжик", "бабочка", "пчела", "муравей",
        "работник", "учитель", "студент", "инженер", "художник", "писатель", "музыкант",
        "актёр", "режиссёр", "фотограф", "программист", "дизайнер", "переводчик", "юрист",
        "бухгалтер", "продавец", "водитель", "строитель", "электрик", "сантехник",
        "исследование", "образование", "производство", "управление", "обслуживание",
        "оборудование", "материал", "инструмент", "устройство", "механизм", "двигатель",
        "температура", "давление", "скорость", "расстояние", "количество", "качество",
        "стоимость", "прибыль", "убыток", "налог", "счёт", "платёж", "перевод", "кредит",
    ];

    public static readonly string[] English =
    [
        "computer", "program", "keyboard", "layout", "message", "question", "answer",
        "meeting", "contract", "task", "project", "team", "development", "testing",
        "document", "letter", "phone", "address", "city", "street", "machine", "road",
        "weather", "money", "week", "month", "morning", "evening", "friend", "family",
        "school", "university", "company", "client", "product", "service", "system",
        "picture", "music", "photograph", "conversation", "decision", "problem", "mistake",
        "version", "page", "request", "rule", "order", "release", "library", "shop",
        "pharmacy", "hospital", "clinic", "station", "airport", "aeroplane", "train",
        "bus", "tram", "underground", "ticket", "luggage", "hotel", "flat", "room",
        "kitchen", "window", "door", "staircase", "ceiling", "wall", "furniture", "chair",
        "sofa", "shelf", "lamp", "mirror", "carpet", "curtain", "fridge", "kettle",
        "plate", "cup", "spoon", "fork", "knife", "saucepan", "frying", "oven",
        "breakfast", "lunch", "dinner", "bread", "milk", "butter", "sugar", "salt",
        "pepper", "potato", "carrot", "cabbage", "cucumber", "tomato", "apple", "orange",
        "grape", "strawberry", "raspberry", "blueberry", "walnut", "biscuit", "sweet",
        "chocolate", "icecream", "marmalade", "juice", "drink", "bottle", "glass",
        "winter", "spring", "summer", "autumn", "snow", "rain", "wind", "fog", "storm",
        "lightning", "rainbow", "cloud", "sunshine", "star", "planet", "space", "nature",
        "forest", "field", "river", "lake", "ocean", "mountain", "valley", "island",
        "shore", "sand", "stone", "tree", "grass", "flower", "leaves", "root", "branch",
        "bird", "fish", "horse", "sheep", "bear", "wolf", "rabbit", "squirrel",
        "butterfly", "worker", "teacher", "student", "engineer", "artist", "writer",
        "musician", "actor", "director", "photographer", "programmer", "designer",
        "translator", "lawyer", "accountant", "seller", "driver", "builder", "plumber",
        "research", "education", "production", "management", "maintenance", "equipment",
        "material", "instrument", "device", "mechanism", "engine", "temperature",
        "pressure", "speed", "distance", "quantity", "quality", "expense", "profit",
        "loss", "invoice", "payment", "transfer", "credit", "balance", "account",
    ];

    /// <summary>
    /// Ordinary sentences, which is what people actually type. Word by word these exercise
    /// the same path automatic correction takes, including the words too short or too common
    /// for the word lists above.
    /// </summary>
    public static readonly string[] RussianSentences =
    [
        "завтра утром будет совещание в главном офисе",
        "положи документы на стол рядом с окном",
        "поезд отправляется через пятнадцать минут",
        "я забыл переключить раскладку и всё пропало",
        "проверь пожалуйста последнюю версию отчёта",
        "мы договорились встретиться возле старого моста",
        "нужно купить хлеб молоко и немного сахара",
        "температура за окном упала ниже нуля",
        "эта программа перестала отвечать на запросы",
        "она читала книгу весь вечер под лампой",
        "дорога до аэропорта заняла почти два часа",
        "новая клавиатура оказалась гораздо удобнее",
        "менеджер попросил прислать счёт до пятницы",
        "в лесу пахло мокрой листвой после дождя",
        "давай обсудим детали проекта на следующей неделе",
    ];

    public static readonly string[] EnglishSentences =
    [
        "tomorrow morning there will be a meeting in the main office",
        "put the documents on the table next to the window",
        "the train leaves in fifteen minutes from platform three",
        "i forgot to switch the layout and lost the whole sentence",
        "please check the latest version of the report",
        "we agreed to meet near the old bridge at noon",
        "we need to buy bread milk and a little sugar",
        "the temperature outside dropped below freezing",
        "this program has stopped responding to requests",
        "she read the book all evening under the lamp",
        "the drive to the airport took almost two hours",
        "the new keyboard turned out to be much better",
        "the manager asked me to send the invoice by friday",
        "the forest smelled of wet leaves after the rain",
        "let us discuss the details of the project next week",
    ];
}
