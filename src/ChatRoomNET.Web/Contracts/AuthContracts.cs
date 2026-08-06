namespace ChatRoomNET.Web.Contracts;

public record RegisterRequest(string UserName, string Email, string Password);

public record LoginRequest(string UserName, string Password);

public record AuthResponse(string Token, string UserId, string UserName);
