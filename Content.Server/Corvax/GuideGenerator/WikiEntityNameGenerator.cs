using JetBrains.Annotations;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Corvax.GuideGenerator;

public static class WikiEntityNameGenerator
{
    private const string ApiEndpoint = "https://station14.ru/api.php";
    private const string CategoryTitle = "Категория:Сущности";

    private static readonly HttpClient HttpClient = new();

    public static void PublishJson(StreamWriter writer, IResourceManager resourceManager, ResPath destRoot)
    {
        var entityNamePath = destRoot.WithName("entity_name.json");

        HashSet<string> existing = new(StringComparer.Ordinal);

        try
        {
            using var readStream = resourceManager.UserData.OpenRead(entityNamePath);
            using var reader = new StreamReader(readStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var json = reader.ReadToEnd();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (!string.IsNullOrWhiteSpace(prop.Name))
                        existing.Add(prop.Name);
                }
            }
        }
        catch
        {
            // ignore
        }

        HashSet<string> wikiTitles = new(StringComparer.Ordinal);
        try
        {
            wikiTitles = FetchAllCategoryTitles();
        }
        catch
        {
            // ignore
        }

        var missing = new List<string>();
        foreach (var title in wikiTitles)
        {
            if (string.IsNullOrWhiteSpace(title))
                continue;

            if (!existing.Contains(title))
                missing.Add(title);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var outputJson = JsonSerializer.Serialize(missing, options);
        writer.Write(outputJson);
    }

    private static HashSet<string> FetchAllCategoryTitles()
    {
        var titles = new HashSet<string>(StringComparer.Ordinal);
        string? cmContinue = null;

        while (true)
        {
            var url =
                $"{ApiEndpoint}?action=query&list=categorymembers&format=json&cmtype=page&cmlimit=max&cmnamespace=0&cmtitle={Uri.EscapeDataString(CategoryTitle)}";
            if (!string.IsNullOrEmpty(cmContinue))
            {
                url += "&cmcontinue=" + Uri.EscapeDataString(cmContinue);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = HttpClient.Send(request);
            response.EnsureSuccessStatusCode();

            using var responseStream = response.Content.ReadAsStream();
            var payload = JsonSerializer.Deserialize<MediaWikiCategoryMembersResponse>(responseStream);
            if (payload?.Query?.CategoryMembers != null)
            {
                foreach (var member in payload.Query.CategoryMembers)
                {
                    if (!string.IsNullOrWhiteSpace(member.Title))
                        titles.Add(member.Title);
                }
            }

            if (payload?.Continue?.CmContinue == null)
                break;

            cmContinue = payload.Continue.CmContinue;
        }

        return titles;
    }

    private sealed class MediaWikiCategoryMembersResponse
    {
        [JsonPropertyName("continue")]
        public MediaWikiContinue? Continue { get; init; }

        [JsonPropertyName("query")]
        public MediaWikiQuery? Query { get; init; }
    }

    [UsedImplicitly]
    private sealed class MediaWikiContinue
    {
        [UsedImplicitly]
        [JsonPropertyName("cmcontinue")]
        public string? CmContinue { get; init; }
    }

    [UsedImplicitly]
    private sealed class MediaWikiQuery
    {
        [UsedImplicitly]
        [JsonPropertyName("categorymembers")]
        public List<MediaWikiCategoryMember>? CategoryMembers { get; init; }
    }

    [UsedImplicitly]
    private sealed class MediaWikiCategoryMember
    {
        [UsedImplicitly]
        [JsonPropertyName("title")]
        public string? Title { get; init; }
    }
}
