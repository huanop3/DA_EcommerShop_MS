using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using web_api_base.Models.ViewModel;

public class AuthHttpClientHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(1, 1);
    private bool _isRefreshing = false;

    public AuthHttpClientHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Thêm token vào header nếu chưa có
        await EnsureAuthorizationHeader(request);

        var response = await base.SendAsync(request, cancellationToken);

        // Nếu nhận 401 và không phải refresh-token endpoint, thử refresh token
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && 
            !request.RequestUri.AbsolutePath.Contains("refresh-Token") &&
            !_isRefreshing)
        {
            var refreshSuccess = await TryRefreshToken();
            
            if (refreshSuccess)
            {
                // Retry request với token mới
                await EnsureAuthorizationHeader(request);
                response = await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }

    private async Task EnsureAuthorizationHeader(HttpRequestMessage request)
    {
        // Clear existing authorization header
        request.Headers.Authorization = null;
        
        var token = await _localStorage.GetItemAsStringAsync("token");
        if (!string.IsNullOrEmpty(token))
        {
            token = CleanToken(token);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<bool> TryRefreshToken()
    {
        await _refreshSemaphore.WaitAsync();
        try
        {
            if (_isRefreshing) return false;
            
            _isRefreshing = true;
            Console.WriteLine("🔄 AuthHttpClientHandler: Attempting to refresh token...");

            var userName = await GetUserNameFromToken();
            var accessToken = await _localStorage.GetItemAsStringAsync("token");
            var refreshToken = await _localStorage.GetItemAsStringAsync("refreshToken");

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(refreshToken))
            {
                Console.WriteLine("❌ Missing credentials for refresh token");
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
                            await _localStorage.SetItemAsStringAsync("token", result.Data.AccessToken);
                            await _localStorage.SetItemAsStringAsync("refreshToken", result.Data.RefreshToken);
                            Console.WriteLine("✅ AuthHttpClientHandler: Token refreshed successfully");
                            return true;
                            
                        case 401: // Refresh token expired
                            Console.WriteLine("🔒 AuthHttpClientHandler: Refresh token expired");
                            await ClearAuthData();
                            return false;
                            
                        case 200: // Token still valid
                            Console.WriteLine("✅ AuthHttpClientHandler: Token still valid");
                            return true;
                    }
                }
            }
            
            Console.WriteLine("❌ AuthHttpClientHandler: Refresh token failed");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ AuthHttpClientHandler Error: {ex.Message}");
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
            var token = await _localStorage.GetItemAsStringAsync("token");
            if (string.IsNullOrEmpty(token)) return null;

            token = CleanToken(token);
            
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token)) return null;

            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            if (jsonToken != null)
            {
                var usernameClaim = jsonToken.Claims.FirstOrDefault(x => 
                    x.Type == "unique_name" || x.Type == "username" || x.Type == "sub");
                return usernameClaim?.Value;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting username from token: {ex.Message}");
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
}