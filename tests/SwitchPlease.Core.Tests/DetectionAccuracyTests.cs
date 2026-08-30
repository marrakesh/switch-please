using SwitchPlease.Core.Detection;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// Measures the detector on words that are deliberately absent from its frequent-word
/// list, so the numbers reflect the model generalising rather than remembering.
///
/// The two rates are not equally important. A missed correction costs one hotkey press;
/// a wrong rewrite destroys text the user had typed correctly. The false-positive bar is
/// therefore set far higher than the recall bar.
/// </summary>
public class DetectionAccuracyTests(ITestOutputHelper output)
{
    private static readonly string[] RussianWords =
    [
        "компьютер", "программа", "клавиатура", "раскладка", "сообщение", "вопрос",
        "ответ", "встреча", "договор", "задача", "проект", "команда", "разработка",
        "тестирование", "документ", "письмо", "телефон", "адрес", "город", "улица",
        "машина", "дорога", "погода", "деньги", "неделя", "месяц", "утро", "вечер",
        "друг", "семья", "школа", "университет", "компания", "клиент", "продукт",
        "сервис", "система", "данные", "картинка", "музыка", "фотография", "разговор",
        "решение", "проблема", "ошибка", "версия", "страница", "запрос", "правило",
        "порядок",
    ];

    private static readonly string[] EnglishWords =
    [
        "computer", "program", "keyboard", "layout", "message", "question", "answer",
        "meeting", "contract", "task", "project", "team", "development", "testing",
        "document", "letter", "phone", "address", "city", "street", "machine", "road",
        "weather", "money", "week", "month", "morning", "evening", "friend", "family",
        "school", "university", "company", "client", "product", "service", "system",
        "picture", "music", "photograph", "conversation", "decision", "problem",
        "mistake", "version", "page", "request", "rule", "order", "release",
    ];

    private readonly HeuristicWrongLayoutDetector _detector = new();

    [Fact]
    public void WrongLayoutTextIsUsuallyCaught()
    {
        var (caught, missed) = MeasureRecall();
        double recall = (double)caught.Count / (caught.Count + missed.Count);

        output.WriteLine($"recall: {recall:P1} ({caught.Count}/{caught.Count + missed.Count})");
        output.WriteLine($"missed: {string.Join(", ", missed)}");

        Assert.True(recall >= 0.90, $"recall dropped to {recall:P1}; missed: {string.Join(", ", missed)}");
    }

    [Fact]
    public void CorrectlyTypedTextIsNeverRewritten()
    {
        var falsePositives = new List<string>();
        int total = 0;

        foreach (string word in RussianWords)
        {
            total++;
            string wouldBecome = LayoutFixture.TypedInEnglish(word);

            if (_detector.Evaluate(word, wouldBecome).ShouldConvert)
            {
                falsePositives.Add($"{word} -> {wouldBecome}");
            }
        }

        foreach (string word in EnglishWords)
        {
            total++;
            string wouldBecome = LayoutFixture.TypedInRussian(word);

            if (_detector.Evaluate(word, wouldBecome).ShouldConvert)
            {
                falsePositives.Add($"{word} -> {wouldBecome}");
            }
        }

        output.WriteLine($"false positives: {falsePositives.Count}/{total}");

        Assert.True(
            falsePositives.Count == 0,
            $"correct text would have been mangled: {string.Join(", ", falsePositives)}");
    }

    /// <summary>
    /// Records what the sensitivity setting actually buys, so the shipped default is a
    /// measured choice rather than a guess, and so a change to the model that quietly
    /// starts mangling correct text fails here.
    /// </summary>
    [Fact]
    public void SensitivityTradeoffIsDocumented()
    {
        output.WriteLine("threshold   recall   false-positives");

        foreach (double threshold in (double[])[0.15, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45])
        {
            var detector = new HeuristicWrongLayoutDetector(threshold);
            int caught = 0;
            int falsePositives = 0;

            foreach (string word in RussianWords)
            {
                string typed = LayoutFixture.TypedInEnglish(word);

                if (detector.Evaluate(typed, word).ShouldConvert)
                {
                    caught++;
                }

                if (detector.Evaluate(word, typed).ShouldConvert)
                {
                    falsePositives++;
                }
            }

            foreach (string word in EnglishWords)
            {
                string typed = LayoutFixture.TypedInRussian(word);

                if (detector.Evaluate(typed, word).ShouldConvert)
                {
                    caught++;
                }

                if (detector.Evaluate(word, typed).ShouldConvert)
                {
                    falsePositives++;
                }
            }

            int total = RussianWords.Length + EnglishWords.Length;
            output.WriteLine($"{threshold:F2}        {(double)caught / total:P1}    {falsePositives}");

            // The default must never be in a region where correct text gets rewritten.
            if (threshold >= 0.25)
            {
                Assert.Equal(0, falsePositives);
            }
        }
    }

    private (List<string> Caught, List<string> Missed) MeasureRecall()
    {
        var caught = new List<string>();
        var missed = new List<string>();

        foreach (string word in RussianWords)
        {
            string asTyped = LayoutFixture.TypedInEnglish(word);
            (_detector.Evaluate(asTyped, word).ShouldConvert ? caught : missed).Add($"{asTyped}->{word}");
        }

        foreach (string word in EnglishWords)
        {
            string asTyped = LayoutFixture.TypedInRussian(word);
            (_detector.Evaluate(asTyped, word).ShouldConvert ? caught : missed).Add($"{asTyped}->{word}");
        }

        return (caught, missed);
    }
}
