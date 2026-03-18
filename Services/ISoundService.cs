namespace Services;

public interface ISoundService
{
    Task Play(string text, GeneratedSpeechVoice voice, Guid? deviceId = null);
    Task AnalyzeFrequencies(string filename, string? outputPath = null);
}

public class SoundService(IOptions<OpenAIOptions> options) : ISoundService
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

        var result = await client.GenerateSpeechAsync(text, voice, speechGenerationOptions);

        var filename = $"{DateTime.Now:yyyyMMdd_HHmmss}.mp3";

        // using TextWriter tw = new StreamWriter("sound-and-prompt.txt", append: true);
        // tw.WriteLine($"{filename}	{text}");

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

    public Task AnalyzeFrequencies(string filename, string? outputPath = null)
    {
        const int m = 12; // FFT size 2^12 = 4096
        const int fftLength = 1 << m;

        using var reader = new AudioFileReader(filename);
        var fftProvider = new FftProvider(m);
        var buffer = new float[fftLength];
        int read;
        var allFrequencies = new List<FrequencyResult[]>();
        double time = 0;

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            fftProvider.Add(buffer, read);
            var frequencies = fftProvider.GetFrequencies(reader.WaveFormat.SampleRate);
            allFrequencies.Add(frequencies.Select(f => new FrequencyResult { Time = time, Frequency = f.Frequency, Intensity = f.Intensity }).ToArray());
            time += (double)read / reader.WaveFormat.SampleRate;
        }

        if (!string.IsNullOrEmpty(outputPath))
        {
            using var writer = new StreamWriter(outputPath);
            writer.WriteLine("Time,Frequency,Intensity");
            foreach (var freqArray in allFrequencies)
            {
                foreach (var freq in freqArray)
                {
                    writer.WriteLine($"{freq.Time},{freq.Frequency},{freq.Intensity}");
                }
            }
        }
        else
        {
            // Group frequencies into bands for more meaningful output
            var bands = new[] { 0, 200, 400, 800, 1600, 3200, 6400, 12800, 22000 };
            var bandIntensities = new double[bands.Length - 1];

            foreach (var freqArray in allFrequencies)
            {
                foreach (var freq in freqArray)
                {
                    for (int i = 0; i < bands.Length - 1; i++)
                    {
                        if (freq.Frequency >= bands[i] && freq.Frequency < bands[i + 1])
                        {
                            bandIntensities[i] += freq.Intensity;
                            break;
                        }
                    }
                }
            }

            Console.WriteLine("Frequency Band Analysis:");
            for (int i = 0; i < bandIntensities.Length; i++)
            {
                Console.WriteLine($"{bands[i]} - {bands[i + 1]} Hz: {bandIntensities[i]:F2}");
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class OpenAIOptions
{
    public const string Section = "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
