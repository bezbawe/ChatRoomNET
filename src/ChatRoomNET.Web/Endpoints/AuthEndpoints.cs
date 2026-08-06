using ChatRoomNET.Web.Contracts;
using ChatRoomNET.Web.Domain;
using ChatRoomNET.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace ChatRoomNET.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService) =>
        {
            var user = new ApplicationUser { UserName = request.UserName, Email = request.Email };
            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return Results.ValidationProblem(result.Errors.ToDictionary(
                    error => error.Code,
                    error => new[] { error.Description }));
            }

            return Results.Ok(new AuthResponse(tokenService.CreateToken(user), user.Id, user.UserName!));
        });

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService) =>
        {
            var user = await userManager.FindByNameAsync(request.UserName);
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new AuthResponse(tokenService.CreateToken(user), user.Id, user.UserName!));
        });
    }
}
