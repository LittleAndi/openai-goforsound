using ReverseMarkdown;
using VersOne.Epub;

namespace Services;

public interface IEpubService
{
    Task ConvertEpubToAudioAsync(string epubPath, GeneratedSpeechVoice voice);
}

public class EpubService(IOptions<OpenAIOptions> options) : IEpubService
{
    private const int MaxTtsChunkSize = 4096;
    private readonly OpenAIOptions _options = options.Value;
    private readonly Converter _markdownConverter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.PassThrough,
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true
    });

    public async Task ConvertEpubToAudioAsync(string epubPath, GeneratedSpeechVoice voice)
    {
        if (!File.Exists(epubPath))
        {
            Console.Error.WriteLine($"File not found: {epubPath}");
            return;
        }

        Console.WriteLine($"Opening epub: {epubPath}");
        var book = await EpubReader.ReadBookAsync(epubPath);

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(epubPath)) ?? ".";

        Console.WriteLine($"Book title: {book.Title}");
        Console.WriteLine($"Chapters found: {book.ReadingOrder.Count}");

        var client = new OpenAIClient(_options.ApiKey).GetAudioClient(_options.Model);

        int chapterIndex = 1;
        foreach (var textItem in book.ReadingOrder)
        {
            var htmlContent = textItem.Content;
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                chapterIndex++;
                continue;
            }

            // Derive a chapter title from the navigation (TOC) if available
            var chapterTitle = GetChapterTitle(book, textItem.FilePath) ?? $"Chapter {chapterIndex}";
            var safeTitle = SanitizeFilename(chapterTitle);
            var filePrefix = $"{chapterIndex:D2}_{safeTitle}";

            Console.WriteLine($"Processing [{chapterIndex}]: {chapterTitle}");

            // Convert HTML to Markdown and save
            var markdown = _markdownConverter.Convert(htmlContent);
            var markdownPath = Path.Combine(outputDir, $"{filePrefix}.md");
            await File.WriteAllTextAsync(markdownPath, markdown);
            Console.WriteLine($"  Saved markdown: {markdownPath}");

            // Extract plain text for TTS
            var plainText = ExtractPlainText(htmlContent);
            if (!string.IsNullOrWhiteSpace(plainText))
            {
                await GenerateAudioAsync(plainText, voice, outputDir, filePrefix, client);
            }

            chapterIndex++;
        }

        Console.WriteLine("Done.");
    }

    private static async Task GenerateAudioAsync(string text, GeneratedSpeechVoice voice, string outputDir, string filePrefix, AudioClient client)
    {
        var speechOptions = new SpeechGenerationOptions { ResponseFormat = GeneratedSpeechFormat.Mp3 };

        var chunks = SplitIntoChunks(text, MaxTtsChunkSize);

        if (chunks.Count == 1)
        {
            var audioPath = Path.Combine(outputDir, $"{filePrefix}.mp3");
            var result = await client.GenerateSpeechAsync(chunks[0], voice, speechOptions);
            await File.WriteAllBytesAsync(audioPath, result.Value.ToArray());
            Console.WriteLine($"  Saved audio:    {audioPath}");
        }
        else
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                var audioPath = Path.Combine(outputDir, $"{filePrefix}_part{i + 1:D2}.mp3");
                var result = await client.GenerateSpeechAsync(chunks[i], voice, speechOptions);
                await File.WriteAllBytesAsync(audioPath, result.Value.ToArray());
                Console.WriteLine($"  Saved audio:    {audioPath}");
            }
        }
    }

    private static string? GetChapterTitle(EpubBook book, string filePath)
    {
        return FindTitleInNavigation(book.Navigation, filePath);
    }

    private static string? FindTitleInNavigation(IEnumerable<EpubNavigationItem>? items, string filePath)
    {
        if (items is null) return null;

        foreach (var item in items)
        {
            if (item.Link?.ContentFilePath is string linkPath &&
                string.Equals(linkPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return item.Title;
            }

            var nested = FindTitleInNavigation(item.NestedItems, filePath);
            if (nested is not null) return nested;
        }

        return null;
    }

    private static string ExtractPlainText(string html)
    {
        // Use HtmlAgilityPack to strip tags cleanly
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var text = System.Net.WebUtility.HtmlDecode(body.InnerText);

        // Collapse excessive whitespace
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s{3,}", "\n\n").Trim();
    }

    private static List<string> SplitIntoChunks(string text, int maxSize)
    {
        var chunks = new List<string>();
        if (text.Length <= maxSize)
        {
            chunks.Add(text);
            return chunks;
        }

        // Split on paragraph breaks first, then sentences
        var paragraphs = text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);
        var current = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (current.Length + paragraph.Length + 2 > maxSize)
            {
                if (current.Length > 0)
                {
                    chunks.Add(current.ToString().Trim());
                    current.Clear();
                }

                // Single paragraph exceeds limit — split by sentence
                if (paragraph.Length > maxSize)
                {
                    var sentences = System.Text.RegularExpressions.Regex.Split(paragraph, @"(?<=[.!?])\s+");
                    foreach (var sentence in sentences)
                    {
                        if (current.Length + sentence.Length + 1 > maxSize)
                        {
                            if (current.Length > 0)
                            {
                                chunks.Add(current.ToString().Trim());
                                current.Clear();
                            }

                            // Single sentence exceeds limit — split by words
                            if (sentence.Length > maxSize)
                            {
                                var words = sentence.Split(' ');
                                foreach (var word in words)
                                {
                                    if (current.Length + word.Length + 1 > maxSize)
                                    {
                                        if (current.Length > 0)
                                        {
                                            chunks.Add(current.ToString().Trim());
                                            current.Clear();
                                        }
                                    }

                                    current.Append(word).Append(' ');
                                }

                                continue;
                            }
                        }

                        current.Append(sentence).Append(' ');
                    }
                }
                else
                {
                    current.Append(paragraph).Append("\n\n");
                }
            }
            else
            {
                current.Append(paragraph).Append("\n\n");
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString().Trim());
        }

        return chunks;
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        // Replace multiple spaces/underscores with a single underscore
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[\s_]+", "_");
        return sanitized.Trim('_');
    }
}
