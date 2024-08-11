var builder = CoconaApp.CreateBuilder();
builder.Services.AddOptions<OpenAIOptions>().Bind(builder.Configuration.GetSection(OpenAIOptions.Section));
builder.Services.AddSingleton<ISoundCreator, SoundCreator>();

var app = builder.Build();

app.AddCommand("devices", () =>
{
    foreach (var device in DirectSoundOut.Devices)
    {
        Console.WriteLine($"{device.ModuleName}: {device.Description}");
    }
});

app.AddCommand("tts-text", async (string text, ISoundCreator creator, GeneratedSpeechVoice voice = GeneratedSpeechVoice.Nova, string? deviceId = null) =>
{
    Guid? device = null;
    if (deviceId != null)
    {
        device = Guid.Parse(deviceId);
    }
    await creator.Play(text, voice, device);
});

app.AddCommand("tts-file", async (string filename, ISoundCreator creator, GeneratedSpeechVoice voice = GeneratedSpeechVoice.Nova, string? deviceId = null) =>
{
    Guid? device = null;
    if (deviceId != null)
    {
        device = Guid.Parse(deviceId);
    }
    var text = File.ReadAllText(filename);
    await creator.Play(text, voice, device);
});

await app.RunAsync();