using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed class TTSManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    
    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
    }

    public async Task<SoundPathSpecifier> ConvertTextToSpeech(string speeker, string text)
    {
        var url = _cfg.GetCVar(CCVars.TTSApiUrl);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new Exception("TTS Api url not specified");
        }
        
        var token = _cfg.GetCVar(CCVars.TTSApiToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("TTS Api token not specified");
        }
        
        // TODO: Use cache by text

        var body = new GenerateVoiceRequest
        {
            ApiToken = token,
            Text = text,
            Speaker = speeker,
        };
        var response = await _httpClient.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"TTS request returned bad status code: {response.StatusCode}");
        }

        var json = await response.Content.ReadFromJsonAsync<GenerateVoiceResponse>();
        var resourcePath = new ResourcePath($"tts-{json.Hash}.ogg").ToRelativePath();
        var oggData = Convert.FromBase64String(json.Results.First().Audio);
        _resourceManager.UploadFile(resourcePath, oggData); // TODO: Should send only by PVS and delete after play
        
        _sawmill.Debug($"Saved new TTS sound by path: {resourcePath.ToRelativePath()}");
        return new SoundPathSpecifier(resourcePath);
    }

    private struct GenerateVoiceRequest
    {
        public GenerateVoiceRequest()
        {
        }

        [JsonPropertyName("api_token")]
        public string ApiToken { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("speaker")]
        public string Speaker { get; set; } = "";

        [JsonPropertyName("sample_rate")]
        public int SampleRate { get; private set; } = 24000;

        [JsonPropertyName("format")]
        public string Format { get; private set; } = "ogg";
    }

    private struct GenerateVoiceResponse
    {
        [JsonPropertyName("results")]
        public List<VoiceResult> Results { get; set; }

        [JsonPropertyName("original_sha1")]
        public string Hash { get; set; }
    }
    
    private struct VoiceResult
    {
        [JsonPropertyName("audio")]
        public string Audio { get; set; }
    }
}
