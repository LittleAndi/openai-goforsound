var builder = Host.CreateApplicationBuilder(args);

// Register services
builder.Services.AddOptions<OpenAIOptions>().Bind(builder.Configuration.GetSection(OpenAIOptions.Section));
builder.Services.AddSingleton<ISoundService, SoundService>();

// Register commands
builder.AddCommand("devices", "List available audio devices", () =>
{
    foreach (var device in DirectSoundOut.Devices)
    {
        Console.WriteLine($"{device.ModuleName}: {device.Description}");
    }
});

builder.AddCommand("tts-text", "Generate and play text-to-speech", async (
    ISoundService soundService,
    string text,
    [Option("voice", Description = "Voice to use (e.g., Nova)")] string? voice = null,
    [Option("device", Description = "Device ID to use")] string? deviceId = null) =>
{
    var voiceEnum = voice != null ? (GeneratedSpeechVoice)Enum.Parse(typeof(GeneratedSpeechVoice), voice) : GeneratedSpeechVoice.Nova;
    Guid? device = deviceId != null ? Guid.Parse(deviceId) : null;
    await soundService.Play(text, voiceEnum, device);
});

builder.AddCommand("tts-file", "Generate and play audio from a text file", async (
    ISoundService soundService,
    string filename,
    [Option("voice", Description = "Voice to use (e.g., Nova)")] string? voice = null,
    [Option("device", Description = "Device ID to use")] string? deviceId = null) =>
{
    var voiceEnum = voice != null ? (GeneratedSpeechVoice)Enum.Parse(typeof(GeneratedSpeechVoice), voice) : GeneratedSpeechVoice.Nova;
    Guid? device = deviceId != null ? Guid.Parse(deviceId) : null;
    var text = File.ReadAllText(filename);
    await soundService.Play(text, voiceEnum, device);
});

builder.AddCommand("analyze", "Analyze audio file frequencies", async (
    ISoundService soundService,
    string filename,
    [Option("output", Description = "Output file path")] string? output = null) =>
{
    await soundService.AnalyzeFrequencies(filename, output);
});

var host = builder.Build();
return await host.RunCommandsAsync(args);