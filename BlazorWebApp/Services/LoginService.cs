using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using web_api_base.Models.ViewModel;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorWebApp.Services
{
    public class LoginService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigationManager;
        private readonly AuthenticationStateProvider _authStateProvider;

        public LoginService(
            HttpClient httpClient,
            ILocalStorageService localStorage,
            IJSRuntime jsRuntime,
            NavigationManager navigationManager,
            AuthenticationStateProvider authStateProvider)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _jsRuntime = jsRuntime;
            _navigationManager = navigationManager;
            _authStateProvider = authStateProvider;
        }

        /// <summary>
        /// Kiểm tra trạng thái đăng nhập - Đơn giản hóa vì AuthHttpClientHandler đã handle refresh
        /// </summary>
        public async Task<bool> CheckAuthenticationStatus()
        {
            try
            {
                var refreshToken = await _localStorage.GetItemAsStringAsync("refreshToken");

                // Không có refresh token = chưa đăng nhập
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return false;
                }

                // Clean token
                refreshToken = CleanToken(refreshToken);

                // Validate refresh token format
                if (!IsValidJwtFormat(refreshToken))
                {
                    Console.WriteLine("Refresh token không đúng định dạng JWT");
                    await ClearAuthData();
                    return false;
                }

                // Có refresh token hợp lệ = đã đăng nhập
                // AuthHttpClientHandler và CustomAuthStateProvider sẽ lo việc refresh
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi xác thực: {ex.Message}");
                await ClearAuthData();
                return false;
            }
        }

        /// <summary>
        /// Lấy thông tin user từ token
        /// </summary>
        public async Task<string> GetUserName()
        {
            try
            {
                var token = await _localStorage.GetItemAsStringAsync("token");
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                token = CleanToken(token);
                
                if (!IsValidJwtFormat(token))
                {
                    return null;
                }

                var handler = new JwtSecurityTokenHandler();
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
                Console.WriteLine($"Lỗi lấy username: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Đăng nhập vào hệ thống
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> Login(UserLoginVM loginModel)
        {
            try
            {
                var deviceInfo = await GetDeviceInfo();
                if (deviceInfo == null)
                {
                    return (false, "Không thể thu thập thông tin thiết bị");
                }

                var loginRequest = new LoginRequestVM
                {
                    Username = loginModel.Username,
                    Password = loginModel.Password,
                    DeviceID = deviceInfo.DeviceID,
                    DeviceName = deviceInfo.DeviceName,
                    DeviceOS = deviceInfo.DeviceOS,
                    ClientName = deviceInfo.ClientName,
                    IPAddress = deviceInfo.IPAddress,
                    CollectedAt = deviceInfo.CollectedAt
                };

                var response = await _httpClient.PostAsJsonAsync("api/UserLogin/Login", loginRequest);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, $"Lỗi server: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<UserLoginResponseVM>>();
                
                if (result?.Success == true && result.Data != null)
                {
                    // Lưu tokens
                    await _localStorage.SetItemAsStringAsync("token", result.Data.AccessToken);
                    await _localStorage.SetItemAsStringAsync("refreshToken", result.Data.RefreshToken);
                    
                    // Notify authentication state changed
                    if (_authStateProvider is CustomAuthStateProvider customProvider)
                    {
                        customProvider.NotifyStateChanged();
                    }
                    
                    return (true, "Đăng nhập thành công");
                }
                else
                {
                    return (false, result?.Message ?? "Đăng nhập thất bại");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi đăng nhập: {ex.Message}");
                return (false, "Có lỗi xảy ra trong quá trình đăng nhập");
            }
        }

        /// <summary>
        /// Đăng ký tài khoản mới
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> Register(RegisterLoginVM registerModel)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/UserLogin/Register", registerModel);
                
                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"Lỗi server: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
                
                if (result?.Success == true)
                {
                    return (true, "Đăng ký thành công");
                }
                else
                {
                    return (false, result?.Message ?? "Đăng ký thất bại");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi đăng ký: {ex.Message}");
                return (false, "Có lỗi xảy ra trong quá trình đăng ký");
            }
        }

        /// <summary>
        /// Đăng xuất khỏi hệ thống
        /// </summary>
        public async Task Logout()
        {
            try
            {
                var refreshToken = await _localStorage.GetItemAsStringAsync("refreshToken");
                
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    refreshToken = CleanToken(refreshToken);
                    
                    // Gọi API logout - AuthHttpClientHandler sẽ tự động thêm token
                    var response = await _httpClient.PutAsJsonAsync("api/UserLogin/Logout", refreshToken);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Lỗi khi đăng xuất từ server");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi đăng xuất: {ex.Message}");
            }
            finally
            {
                // Luôn clear local data
                await ClearAuthData();
                
                // Notify authentication state changed
                if (_authStateProvider is CustomAuthStateProvider customProvider)
                {
                    customProvider.NotifyStateChanged();
                }
            }
        }

        // HELPER METHODS
        private string CleanToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            
            if (token.StartsWith("\"") && token.EndsWith("\""))
            {
                return token.Trim('"');
            }
            return token;
        }

        private bool IsValidJwtFormat(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            
            var handler = new JwtSecurityTokenHandler();
            return handler.CanReadToken(token);
        }

        private async Task ClearAuthData()
        {
            await _localStorage.RemoveItemAsync("token");
            await _localStorage.RemoveItemAsync("refreshToken");
            await _jsRuntime.InvokeVoidAsync("deleteCookie", "token");
        }

        public async Task<DeviceInfoVM> GetDeviceInfo()
        {
            try
            {
                var existingInfo = await _localStorage.GetItemAsync<DeviceInfoVM>("deviceInfo");
                if (existingInfo != null)
                {
                    return existingInfo;
                }

                var jsTask = _jsRuntime.InvokeAsync<DeviceInfoVM>("collectDeviceInfo").AsTask();
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(jsTask, timeoutTask);

                if (completedTask == jsTask && !jsTask.IsFaulted && !jsTask.IsCanceled)
                {
                    var deviceInfo = await jsTask;
                    if (deviceInfo != null)
                    {
                        await _localStorage.SetItemAsync("deviceInfo", deviceInfo);
                        return deviceInfo;
                    }
                }

                // Fallback device info
                return CreateFallbackDeviceInfo();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi thu thập device info: {ex.Message}");
                return CreateFallbackDeviceInfo();
            }
        }

        private DeviceInfoVM CreateFallbackDeviceInfo()
        {
            return new DeviceInfoVM
            {
                DeviceID = Guid.NewGuid().ToString(),
                DeviceName = "Unknown Device",
                DeviceOS = "Unknown OS",
                ClientName = "BlazorWebApp",
                IPAddress = "Unknown",
                CollectedAt = DateTime.Now
            };
        }
    }
}