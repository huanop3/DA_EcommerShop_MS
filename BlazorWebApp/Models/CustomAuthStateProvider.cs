using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using System;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net.Http;
using System.Net.Http.Json;
using web_api_base.Models.ViewModel;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();
    private readonly SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(1, 1);
    private bool _isRefreshing = false;

    public CustomAuthStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Lấy token từ localStorage
        var token = await _localStorage.GetItemAsync<string>("token");
        
        // Trường hợp không có token => Không đăng nhập
        if (string.IsNullOrWhiteSpace(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Clean token (remove quotes if any)
        token = CleanToken(token);

        try
        {
            // Đọc token để kiểm tra expiry trước khi validate
            if (_tokenHandler.CanReadToken(token))
            {
                var jsonToken = _tokenHandler.ReadJwtToken(token);
                
                // Nếu token sắp hết hạn (còn dưới 5 phút), thử refresh
                if (jsonToken.ValidTo <= DateTime.UtcNow.AddMinutes(5))
                {
                    Console.WriteLine("🔄 Token sắp hết hạn, attempting refresh trong AuthStateProvider...");
                    var refreshSuccess = await TryRefreshToken();
                    
                    if (refreshSuccess)
                    {
                        // Lấy token mới sau khi refresh
                        token = await _localStorage.GetItemAsync<string>("token");
                        token = CleanToken(token);
                        Console.WriteLine("✅ Token refreshed successfully on AuthStateProvider");
                    }
                    else
                    {
                        Console.WriteLine("❌ Token refresh failed on AuthStateProvider");
                        await ClearAuthData();
                        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                    }
                }
            }

            // Cấu hình kiểm tra token
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("NGUYEN_CONG_HUAN_DO_AN_HUAN_DOTNET_230102!")),
                ValidateIssuer = true,
                ValidIssuer = "huan",
                ValidateAudience = true,
                ValidAudience = "sv_ecomerce",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.Name
            };

            // Giải mã token để xác thực
            var principal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            
            // Trả về trạng thái xác thực
            return new AuthenticationState(principal);
        }
        catch (SecurityTokenExpiredException)
        {
            Console.WriteLine("Token expired, attempting refresh...");
            var refreshSuccess = await TryRefreshToken();
            
            if (refreshSuccess)
            {
                // Retry validation với token mới
                var newToken = await _localStorage.GetItemAsync<string>("token");
                return await GetAuthenticationStateAsync();
            }
            else
            {
                await ClearAuthData();
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token validation failed: {ex.Message}");
            // Nếu token không hợp lệ => Đăng xuất
            await ClearAuthData();
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    private async Task<bool> TryRefreshToken()
    {
        await _refreshSemaphore.WaitAsync();
        try
        {
            if (_isRefreshing) return false;
            _isRefreshing = true;

            var userName = await GetUserNameFromToken();
            var accessToken = await _localStorage.GetItemAsync<string>("token");
            var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(refreshToken))
            {
                Console.WriteLine("❌ Missing credentials for refresh token on AuthStateProvider");
                return false;
            }

            var loginCheck = new UserLoginResponseVM
            {
                Username = userName,
                AccessToken = CleanToken(accessToken),
                RefreshToken = CleanToken(refreshToken)
            };

            // Tạo HttpClient riêng để tránh circular dependency
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("http://localhost:5166/");
            
            var response = await httpClient.PostAsJsonAsync("api/UserLogin/refresh-Token", loginCheck);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<UserLoginResponseVM>>();
                
                if (result?.Success == true)
                {
                    switch (result.StatusCode)
                    {
                        case 201: // Token refreshed successfully
                            await _localStorage.SetItemAsync("token", result.Data.AccessToken);
                            await _localStorage.SetItemAsync("refreshToken", result.Data.RefreshToken);
                            Console.WriteLine("✅ AuthStateProvider: Token refreshed successfully");
                            return true;
                            
                        case 401: // Refresh token expired
                            Console.WriteLine("🔒 AuthStateProvider: Refresh token expired");
                            return false;
                            
                        case 200: // Token still valid
                            Console.WriteLine("✅ AuthStateProvider: Token still valid");
                            return true;
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AuthStateProvider refresh error: {ex.Message}");
            return false;
        }
        finally
        {
            _isRefreshing = false;
            _refreshSemaphore.Release();
        }
    }

    private async Task<string> GetUserNameFromToken()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>("token");
            if (string.IsNullOrEmpty(token)) return null;

            token = CleanToken(token);
            
            if (!_tokenHandler.CanReadToken(token)) return null;

            var jsonToken = _tokenHandler.ReadJwtToken(token);
            var usernameClaim = jsonToken.Claims.FirstOrDefault(x => 
                x.Type == "unique_name" || x.Type == "username" || x.Type == "sub" || x.Type == ClaimTypes.Name);
            return usernameClaim?.Value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting username from token on AuthStateProvider: {ex.Message}");
        }
        return null;
    }

    private string CleanToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return token;
        return token.StartsWith("\"") && token.EndsWith("\"") ? token.Trim('"') : token;
    }

    private async Task ClearAuthData()
    {
        await _localStorage.RemoveItemAsync("token");
        await _localStorage.RemoveItemAsync("refreshToken");
    }

    public async Task MarkUserAsAuthenticated(string token)
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task MarkUserAsLoggedOut()
    {
        await ClearAuthData();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void NotifyStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
