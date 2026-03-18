var builder = Host.CreateApplicationBuilder(args);

// Register services
builder.Services.AddOptions<OpenAIOptions>().Bind(builder.Configuration.GetSection(OpenAIOptions.Section));
builder.Services.AddSingleton<ISoundService, SoundService>();
builder.Services.AddSingleton<IEpubService, EpubService>();

// Register commands
builder.AddCommand("devices", "List available audio devices", () =>
{
    foreach (var device in DirectSoundOut.Devices)
    {
        Console.WriteLine($"{device.Guid} - {device.ModuleName}: {device.Description}");
    }
});

builder.AddCommand("tts-text", "Generate and play text-to-speech", async (
    ISoundService soundService,
    string text,
    [Option("voice", Description = "Voice to use (e.g., Nova)")] string? voice = null,
    [Option("device", Description = "Device ID to use")] string? deviceId = null) =>
{
    GeneratedSpeechVoice voiceEnum;
    if (voice is null)
    {
        voiceEnum = GeneratedSpeechVoice.Nova;
    }
    else if (!Enum.TryParse<GeneratedSpeechVoice>(voice, ignoreCase: true, out voiceEnum))
    {
        Console.Error.WriteLine($"Invalid voice '{voice}'. Valid voices: {string.Join(", ", Enum.GetNames(typeof(GeneratedSpeechVoice)))}");
        return;
    }

    Guid? device = null;
    if (deviceId != null)
    {
        if (!Guid.TryParse(deviceId, out var parsedDevice))
        {
            Console.Error.WriteLine($"Invalid device ID '{deviceId}'. Please provide a valid GUID.");
            return;
        }

        device = parsedDevice;
    }

    await soundService.Play(text, voiceEnum, device);
});

builder.AddCommand("tts-file", "Generate and play audio from a text file", async (
    ISoundService soundService,
    string filename,
    [Option("voice", Description = "Voice to use (e.g., Nova)")] string? voice = null,
    [Option("device", Description = "Device ID to use")] string? deviceId = null) =>
{
    GeneratedSpeechVoice voiceEnum;
    if (voice is null)
    {
        voiceEnum = GeneratedSpeechVoice.Nova;
    }
    else if (!Enum.TryParse<GeneratedSpeechVoice>(voice, ignoreCase: true, out voiceEnum))
    {
        Console.Error.WriteLine($"Invalid voice '{voice}'. Valid voices: {string.Join(", ", Enum.GetNames(typeof(GeneratedSpeechVoice)))}");
        return;
    }

    Guid? device = null;
    if (deviceId != null)
    {
        if (!Guid.TryParse(deviceId, out var parsedDevice))
        {
            Console.Error.WriteLine($"Invalid device ID '{deviceId}'. Please provide a valid GUID.");
            return;
        }

        device = parsedDevice;
    }

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

builder.AddCommand("epub-to-audio", "Convert an epub book to markdown and audio files (one per chapter)", async (
    IEpubService epubService,
    string filename,
    [Option("voice", Description = "Voice to use (e.g., Nova)")] string? voice = null) =>
{
    GeneratedSpeechVoice voiceEnum;
    if (voice is null)
    {
        voiceEnum = GeneratedSpeechVoice.Nova;
    }
    else if (!Enum.TryParse<GeneratedSpeechVoice>(voice, ignoreCase: true, out voiceEnum))
    {
        Console.Error.WriteLine($"Invalid voice '{voice}'. Valid voices: {string.Join(", ", Enum.GetNames(typeof(GeneratedSpeechVoice)))}");
        return;
    }

    await epubService.ConvertEpubToAudioAsync(filename, voiceEnum);
});

var host = builder.Build();
return await host.RunCommandsAsync(args);