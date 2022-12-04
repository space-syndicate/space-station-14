using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed class TTSManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    
    private readonly HttpClient _httpClient = new();

    private ISawmill _sawmill = default!;
    private readonly Dictionary<string, byte[]> _cache = new();

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
    }

    public async Task<byte[]> ConvertTextToSpeech(string speaker, string text)
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

        var cacheKey = GenerateCacheKey(speaker, text);
        if (_cache.TryGetValue(cacheKey, out var data))
        {
            _sawmill.Debug($"Use cached sound for '{text}' speech by '{speaker}' speaker");
            return data;
        }

        var body = new GenerateVoiceRequest
        {
            ApiToken = token,
            Text = text,
            Speaker = speaker,
        };
        var response = await _httpClient.PostAsJsonAsync(url, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"TTS request returned bad status code: {response.StatusCode}");
        }

        var json = await response.Content.ReadFromJsonAsync<GenerateVoiceResponse>();
        var soundData = Convert.FromBase64String(json.Results.First().Audio);
        _cache.Add(cacheKey, soundData);
        
        _sawmill.Debug($"Generated new sound for '{text}' speech by '{speaker}' speaker ({soundData.Length} bytes)");

        return soundData;
    }

    public void ResetCache()
    {
        _cache.Clear();
    }

    private string GenerateCacheKey(string speaker, string text)
    {
        var key = $"{speaker}/{text}";
        byte[] keyData = Encoding.UTF8.GetBytes(key);
        var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(keyData);
        return Convert.ToHexString(bytes);
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
