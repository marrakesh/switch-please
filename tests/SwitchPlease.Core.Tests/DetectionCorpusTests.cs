using SwitchPlease.Core.Detection;
using Xunit;
using Xunit.Abstractions;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// The same measurement as <see cref="DetectionAccuracyTests"/>, over a sample large enough
/// for the percentage to mean something, and over whole sentences rather than isolated
/// words.
///
/// The sentences are the part that was missing. Automatic correction fires at the end of
/// every word in a line, so a false-positive rate that reads as "zero out of a hundred
/// words" can still amount to a mangled word every few paragraphs. Running real text through
/// it word by word, with the preceding words as context, is the closest thing to how it
/// behaves in use.
/// </summary>
public class DetectionCorpusTests(ITestOutputHelper output)
{
    private readonly HeuristicWrongLayoutDetector _detector = new();

    [Fact]
    public void MostWrongLayoutWordsAreCaught()
    {
        var missed = new List<string>();
        int total = 0;

        foreach (var (words, typeIt) in Samples())
        {
            foreach (string word in words)
            {
                total++;

                if (!_detector.Evaluate(typeIt(word), word).ShouldConvert)
                {
                    missed.Add(word);
                }
            }
        }

        double recall = (double)(total - missed.Count) / total;

        output.WriteLine($"recall over {total} words: {recall:P1}");
        output.WriteLine($"missed ({missed.Count}): {string.Join(", ", missed)}");

        Assert.True(recall >= 0.85, $"recall dropped to {recall:P1} over {total} words");
    }

    [Fact]
    public void CorrectWordsAreNotRewritten()
    {
        // The bar that matters. A missed correction costs one keypress; a wrong rewrite
        // costs the user a word they had typed correctly and may not notice losing.
        var mangled = new List<string>();
        int total = 0;

        foreach (var (words, typeIt) in Samples())
        {
            foreach (string word in words)
            {
                total++;

                if (_detector.Evaluate(word, typeIt(word)).ShouldConvert)
                {
                    mangled.Add($"{word} -> {typeIt(word)}");
                }
            }
        }

        output.WriteLine($"false positives over {total} words: {mangled.Count}");

        Assert.True(mangled.Count == 0, $"correct text would have been rewritten: {string.Join(", ", mangled)}");
    }

    [Fact]
    public void RealSentencesSurviveWordByWord()
    {
        // Every word of every sentence, judged with what came before it as context, exactly
        // as automatic correction would as the user typed it.
        var mangled = new List<string>();
        int words = 0;

        foreach (var (sentences, typeIt) in Sentences())
        {
            foreach (string sentence in sentences)
            {
                string[] parts = sentence.Split(' ');

                for (int i = 0; i < parts.Length; i++)
                {
                    words++;

                    string context = string.Join(' ', parts.Take(i));
                    var verdict = _detector.Evaluate(parts[i], typeIt(parts[i]), context);

                    if (verdict.ShouldConvert)
                    {
                        mangled.Add($"\"{parts[i]}\" in \"{sentence}\" [{verdict.Reason}]");
                    }
                }
            }
        }

        output.WriteLine($"{words} words of running text, {mangled.Count} would have been rewritten");

        foreach (string problem in mangled)
        {
            output.WriteLine($"  {problem}");
        }

        Assert.True(mangled.Count == 0, $"{mangled.Count} of {words} words of correct prose would have been rewritten");
    }

    [Fact]
    public void SentencesTypedInTheWrongLayoutAreMostlyRecovered()
    {
        var missed = new List<string>();
        int words = 0;

        foreach (var (sentences, typeIt) in Sentences())
        {
            foreach (string sentence in sentences)
            {
                string[] parts = sentence.Split(' ');

                for (int i = 0; i < parts.Length; i++)
                {
                    // Only words the guards allow through are worth counting: two-letter
                    // words are deliberately never touched.
                    if (TextGuards.IsIneligible(typeIt(parts[i]), out _))
                    {
                        continue;
                    }

                    words++;

                    // The context is in the wrong layout too, because the whole line was --
                    // which is exactly the evidence the second reading of it carries.
                    string context = string.Join(' ', parts.Take(i).Select(typeIt));
                    string converted = string.Join(' ', parts.Take(i));

                    if (!_detector.Evaluate(typeIt(parts[i]), parts[i], context, converted).ShouldConvert)
                    {
                        missed.Add(parts[i]);
                    }
                }
            }
        }

        double recall = (double)(words - missed.Count) / words;

        output.WriteLine($"recall over {words} eligible words of running text: {recall:P1}");
        output.WriteLine($"missed ({missed.Count}): {string.Join(", ", missed)}");

        Assert.True(recall >= 0.85, $"recall over running text dropped to {recall:P1}");
    }

    /// <summary>
    /// What the sensitivity setting buys on the larger sample. Printed rather than asserted
    /// beyond the one rule that matters: at the shipped default and above, nothing correct
    /// is ever rewritten.
    /// </summary>
    [Fact]
    public void SensitivityTradeoffOnTheLargerSample()
    {
        output.WriteLine("threshold   recall   false-positives");

        foreach (double threshold in (double[])[0.15, 0.20, 0.25, 0.30, 0.35, 0.45])
        {
            var detector = new HeuristicWrongLayoutDetector(threshold);
            int caught = 0;
            int falsePositives = 0;
            int total = 0;

            foreach (var (words, typeIt) in Samples())
            {
                foreach (string word in words)
                {
                    total++;

                    if (detector.Evaluate(typeIt(word), word).ShouldConvert)
                    {
                        caught++;
                    }

                    if (detector.Evaluate(word, typeIt(word)).ShouldConvert)
                    {
                        falsePositives++;
                    }
                }
            }

            output.WriteLine($"{threshold:F2}        {(double)caught / total:P1}    {falsePositives}");

            if (threshold >= 0.25)
            {
                Assert.Equal(0, falsePositives);
            }
        }
    }

    private static (string[] Words, Func<string, string> AsTyped)[] Samples() =>
    [
        (DetectionCorpus.Russian, LayoutFixture.TypedInEnglish),
        (DetectionCorpus.English, LayoutFixture.TypedInRussian),
    ];

    private static (string[] Sentences, Func<string, string> AsTyped)[] Sentences() =>
    [
        (DetectionCorpus.RussianSentences, LayoutFixture.TypedInEnglish),
        (DetectionCorpus.EnglishSentences, LayoutFixture.TypedInRussian),
    ];
}
