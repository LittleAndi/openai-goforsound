namespace Services;

public interface ISoundCreator
{
    Task Play(string text, GeneratedSpeechVoice voice, Guid? deviceId = null);
}

public class SoundCreator(IOptions<OpenAIOptions> options) : ISoundCreator
{
    private readonly OpenAIOptions options = options.Value;

    public async Task Play(string text, GeneratedSpeechVoice voice, Guid? deviceId)
    {
        OpenAIClient openAIClient = new(options.ApiKey);

        var client = openAIClient.GetAudioClient(options.Model);

        var speechGenerationOptions = new SpeechGenerationOptions()
        {
            ResponseFormat = GeneratedSpeechFormat.Mp3,
        };

        var result = await client.GenerateSpeechFromTextAsync(text, voice, speechGenerationOptions);

        var filename = $"{DateTime.Now:yyyyMMdd_HHmmss}.mp3";

        // using TextWriter tw = new StreamWriter("sound-and-prompt.txt", append: true);
        // tw.WriteLine($"{filename}\t{text}");

        File.WriteAllBytes(filename, result.Value.ToArray());

        using var stream = new MemoryStream(result.Value.ToArray());
        using var mp3 = new Mp3FileReader(stream);

        if (deviceId != null)
        {
            using var outputDevice = new DirectSoundOut((Guid)deviceId);
            outputDevice.Init(mp3);
            outputDevice.Play();

            while (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                Thread.Sleep(1000);
            }
        }
    }
}

public sealed class OpenAIOptions
{
    public const string Section = "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}