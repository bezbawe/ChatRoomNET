using System.Net;
using System.Net.Http.Headers;
using ChatRoomNET.Web.UI.Blazor.Auth;
using Microsoft.AspNetCore.Components;

namespace ChatRoomNET.Web.UI.Blazor.Services;

// Подкладывает JWT в заголовок Authorization для всех вызовов ChatApiClient.
public class BearerTokenHandler(
    JwtAuthenticationStateProvider authState,
    NavigationManager navigation) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await authState.GetTokenAsync();
        var authenticated = !string.IsNullOrWhiteSpace(token);
        if (authenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Токен был, но сервер его отклонил (протух/отозван) — разлогиниваем и уводим на /login.
        // Гейт по authenticated: 401 от /login (неверный пароль) шлётся без токена и сюда не попадает.
        if (authenticated && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await authState.MarkUserAsLoggedOutAsync();
            var returnUrl = navigation.ToBaseRelativePath(navigation.Uri);
            navigation.NavigateTo($"login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        return response;
    }
}
