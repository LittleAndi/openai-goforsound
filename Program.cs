var builder = CoconaApp.CreateBuilder();
builder.Services.AddOptions<OpenAIOptions>().Bind(builder.Configuration.GetSection(OpenAIOptions.Section));
builder.Services.AddSingleton<ISoundService, SoundService>();

var app = builder.Build();

app.AddCommand("devices", () =>
{
    foreach (var device in DirectSoundOut.Devices)
    {
        Console.WriteLine($"{device.ModuleName}: {device.Description}");
    }
});

app.AddCommand("tts-text", async (string text, ISoundService soundService, GeneratedSpeechVoice? voice, string? deviceId = null) =>
{
    voice ??= GeneratedSpeechVoice.Nova;

    Guid? device = null;
    if (deviceId != null)
    {
        device = Guid.Parse(deviceId);
    }
    await soundService.Play(text, (GeneratedSpeechVoice)voice, device);
});

app.AddCommand("tts-file", async (string filename, ISoundService soundService, GeneratedSpeechVoice? voice, string? deviceId = null) =>
{
    voice ??= GeneratedSpeechVoice.Nova;

    Guid? device = null;
    if (deviceId != null)
    {
        device = Guid.Parse(deviceId);
    }
    var text = File.ReadAllText(filename);
    await soundService.Play(text, (GeneratedSpeechVoice)voice, device);
});

app.AddCommand("analyze", async (string filename, ISoundService soundService, string? output = null) =>
{
    await soundService.AnalyzeFrequencies(filename, output);
});

await app.RunAsync();