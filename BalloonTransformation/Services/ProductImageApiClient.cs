using System.Net.Http.Json;
using System.Text.Json;

namespace Naranja.BalloonTransformation.Services;

internal sealed class ProductImageApiClient
{
    private const string BaseAddress = "http://localhost:5180";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(BaseAddress),
        Timeout = TimeSpan.FromSeconds(15)
    };

    public ProductColorLookup? GetByFullName(string fullName)
    {
        try
        {
            using var response = Http.GetAsync(
                $"/api/product-images/by-full-name?fullName={Uri.EscapeDataString(fullName)}")
                .GetAwaiter().GetResult();
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            EnsureSuccess(response);
            return response.Content.ReadFromJsonAsync<ProductColorLookup>(JsonOptions)
                .GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Naranja.Api (http://localhost:5180) に接続できません。Api が起動しているか確認してください。",
                ex);
        }
    }

    public void UpdateOriginalImageFile(int productColorCodeId, string fileName)
    {
        try
        {
            using var response = Http.PutAsJsonAsync(
                $"/api/product-images/{productColorCodeId}/original-file",
                new { fileName },
                JsonOptions).GetAwaiter().GetResult();
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return;
            EnsureSuccess(response);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Naranja.Api (http://localhost:5180) に接続できません。Api が起動しているか確認してください。",
                ex);
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        throw new InvalidOperationException(
            $"Naranja.Api が {(int)response.StatusCode} を返しました。{detail}");
    }
}

internal sealed class ProductColorLookup
{
    public int ProductColorCodeId { get; init; }
    public int ProductId { get; init; }
    public short BrandId { get; init; }
    public string OriginalImageFile { get; init; } = string.Empty;
    public bool IsDiscontinued { get; init; }
    public bool IsAvailable { get; init; }
}
