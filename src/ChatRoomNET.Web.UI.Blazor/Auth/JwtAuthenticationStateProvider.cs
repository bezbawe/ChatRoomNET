using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace ChatRoomNET.Web.UI.Blazor.Auth;

// Хранит JWT в localStorage и собирает ClaimsPrincipal из его payload.
// Единый источник токена для BearerTokenHandler и <AuthorizeView>.
// Payload парсим вручную (без System.IdentityModel.Tokens.Jwt — он тяжёлый для WASM);
// подпись здесь не проверяем — это делает backend при каждом запросе.
public class JwtAuthenticationStateProvider(ILocalStorageService storage) : AuthenticationStateProvider
{
    private const string TokenKey = "authToken";

    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public async Task<string?> GetTokenAsync() => await storage.GetItemAsStringAsync(TokenKey);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await storage.GetItemAsStringAsync(TokenKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Anonymous;
        }

        var payload = ParsePayload(token);
        if (payload is null || IsExpired(payload))
        {
            await storage.RemoveItemAsync(TokenKey);
            return Anonymous;
        }

        var identity = new ClaimsIdentity(BuildClaims(payload), "jwt", ClaimTypes.Name, ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task MarkUserAsAuthenticatedAsync(string token)
    {
        await storage.SetItemAsStringAsync(TokenKey, token);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await storage.RemoveItemAsync(TokenKey);
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private static Dictionary<string, JsonElement>? ParsePayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var json = Convert.FromBase64String(PadBase64(parts[1]));
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static bool IsExpired(Dictionary<string, JsonElement> payload) =>
        payload.TryGetValue("exp", out var exp) &&
        DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) < DateTimeOffset.UtcNow;

    // Маппим короткие имена claim'ов JWT (sub/unique_name) в стандартные ClaimTypes,
    // чтобы User.Identity.Name и NameIdentifier работали как на backend.
    private static IEnumerable<Claim> BuildClaims(Dictionary<string, JsonElement> payload)
    {
        foreach (var (key, value) in payload)
        {
            var type = key switch
            {
                "sub" => ClaimTypes.NameIdentifier,
                "unique_name" => ClaimTypes.Name,
                "email" => ClaimTypes.Email,
                _ => key
            };
            yield return new Claim(type, value.ToString());
        }
    }

    // base64url → base64: добавляем padding и заменяем алфавит.
    private static string PadBase64(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        return (value.Length % 4) switch
        {
            2 => value + "==",
            3 => value + "=",
            _ => value
        };
    }
}
