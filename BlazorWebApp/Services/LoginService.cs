using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using web_api_base.Models.ViewModel;

namespace BlazorWebApp.Services
{
    public class LoginService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigationManager;

        public LoginService(
            HttpClient httpClient,
            ILocalStorageService localStorage,
            IJSRuntime jsRuntime,
            NavigationManager navigationManager)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _jsRuntime = jsRuntime;
            _navigationManager = navigationManager;
        }

        /// <summary>
        /// Kiểm tra trạng thái đăng nhập của người dùng
        /// </summary>
        public async Task<bool> CheckAuthenticationStatus()
        {
            try
            {
                // Lấy token từ local storage
                var token = await _localStorage.GetItemAsStringAsync("token");

                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                // Bỏ dấu ngoặc kép nếu có
                if (token.StartsWith("\"") && token.EndsWith("\""))
                {
                    token = token.Trim('"');
                    // Lưu lại token đã sửa
                    await _localStorage.SetItemAsStringAsync("token", token);
                }

                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                {
                    Console.WriteLine("Token không đúng định dạng JWT");
                    await _localStorage.RemoveItemAsync("token");
                    return false;
                }

                // Đọc thông tin từ token
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                if (jsonToken != null)
                {
                    // Kiểm tra hết hạn
                    if (jsonToken.ValidTo < DateTime.UtcNow)
                    {
                        await _localStorage.RemoveItemAsync("token");
                        return false;
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi xác thực: {ex.Message}");
                await _localStorage.RemoveItemAsync("token");
                return false;
            }
        }
        /// <summary>
        /// Lấy thông tin user từ token
        /// </summary>
        public async Task<string> GetUserName(){
            try
            {
                var token = await _localStorage.GetItemAsStringAsync("token");
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                if (jsonToken != null)
                {
                    var userName = jsonToken.Claims.First(claim => claim.Type == "UserName").Value;
                    return userName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy tên người dùng: {ex.Message}");
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
                // Thu thập thông tin thiết bị từ JavaScript thông qua phương thức GetDeviceInfo
                var deviceInfo = await GetDeviceInfo();
                if (deviceInfo == null)
                {
                    return (false, "Không thể thu thập thông tin thiết bị. Vui lòng bật JavaScript và thử lại.");
                }

                // Tạo LoginRequestVM với thông tin đăng nhập và thông tin thiết bị
                var loginRequest = new LoginRequestVM
                {
                    Username = loginModel.Username,
                    Password = loginModel.Password,
                    DeviceID = deviceInfo.DeviceID,
                    DeviceName = deviceInfo.DeviceName,
                    DeviceOS = deviceInfo.DeviceOS,
                    ClientName = deviceInfo.ClientName,
                    IPAddress = deviceInfo.IPAddress,
                    CollectedAt = DateTime.Now
                };

                // Gọi API đăng nhập
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5166/api/User/Login", loginRequest);
                
                // Phân tích phản hồi từ server
                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<UserLoginResponseVM>>();
                
                if (result == null)
                {
                    return (false, "Không nhận được phản hồi từ server");
                }

                // Nếu đăng nhập thành công
                if (result.Success && result.Data != null)
                {
                    // Lưu token vào local storage và cookie
                    await _localStorage.SetItemAsStringAsync("token", result.Data.AccessToken);
                    
                    // Lưu refresh token
                    if (!string.IsNullOrEmpty(result.Data.RefreshToken))
                    {
                        await _localStorage.SetItemAsStringAsync("refreshToken", result.Data.RefreshToken);
                        await _jsRuntime.InvokeVoidAsync("setCookie", "refreshToken", result.Data.RefreshToken);
                    }
                    
                    // Lưu token vào cookie
                    await _jsRuntime.InvokeVoidAsync("setCookie", "token", result.Data.AccessToken);
                    
                    return (true, result.Message ?? "Đăng nhập thành công");
                }
                else
                {
                    // Trả về thông báo lỗi từ server
                    return (false, result.Message ?? "Đăng nhập không thành công");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi đăng nhập: {ex.Message}");
                return (false, "Có lỗi xảy ra khi đăng nhập. Vui lòng thử lại sau.");
            }
        }

        /// <summary>
        /// Đăng ký tài khoản mới
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> Register(RegisterLoginVM registerModel)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5166/api/User/RegisterUser", registerModel);
                
                // Đọc phản hồi từ server
                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<RegisterLoginVM>>();
                
                if (result == null)
                {
                    return (false, "Không nhận được phản hồi từ server");
                }
                
                if (result.Success)
                {
                    return (true, result.Message ?? "Đăng ký thành công");
                }
                else
                {
                    return (false, result.Message ?? "Đăng ký không thành công");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi đăng ký: {ex.Message}");
                return (false, "Có lỗi xảy ra khi đăng ký. Vui lòng thử lại sau.");
            }
        }

        /// <summary>
        /// Đăng xuất khỏi hệ thống
        /// </summary>
        public async Task Logout(bool redirectToHome = true)
        {
            await _localStorage.RemoveItemAsync("token");
            await _localStorage.RemoveItemAsync("refreshToken");
            await _jsRuntime.InvokeVoidAsync("deleteCookie", "token");
            await _jsRuntime.InvokeVoidAsync("deleteCookie", "refreshToken");
            
            if (redirectToHome)
            {
                _navigationManager.NavigateTo("/", true);
            }
        }

        public async Task<LoginRequestVM> CollectDeviceInfo()
        {
            try
            {
                // Kiểm tra xem đã có thông tin thiết bị chưa
                var existingInfo = await _localStorage.GetItemAsync<LoginRequestVM>("deviceInfo");
                if (existingInfo != null)
                {
                    // Cập nhật thời gian thu thập
                    existingInfo.CollectedAt = DateTime.Now;
                    await _localStorage.SetItemAsync("deviceInfo", existingInfo);
                    return existingInfo;
                }

                // Thiết lập timeout cho JavaScript interop
                var timeoutTask = Task.Delay(5000); // 5 giây timeout

                // Thu thập thông tin thiết bị từ JavaScript
                var jsTask = _jsRuntime.InvokeAsync<LoginRequestVM>("getFullDeviceInfo").AsTask();

                // Chờ task nào hoàn thành trước
                var completedTask = await Task.WhenAny(jsTask, timeoutTask);

                // Nếu JavaScript interop hoàn thành trước timeout
                if (completedTask == jsTask && !jsTask.IsFaulted && !jsTask.IsCanceled)
                {
                    var deviceInfo = await jsTask;
                    if (deviceInfo != null)
                    {
                        deviceInfo.CollectedAt = DateTime.Now;
                        // Lưu vào localStorage
                        await _localStorage.SetItemAsync("deviceInfo", deviceInfo);
                        return deviceInfo;
                    }
                }

                // Nếu JavaScript interop bị lỗi hoặc timeout, tạo thông tin mặc định
                Console.WriteLine("Không thể thu thập thông tin thiết bị từ JavaScript - sử dụng thông tin mặc định");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi thu thập thông tin thiết bị: {ex.Message}");
                return null;
            }
        }
        public async Task<LoginRequestVM> GetDeviceInfo()
        {
            try
            {
                var deviceInfo = await _localStorage.GetItemAsync<LoginRequestVM>("deviceInfo");
                if (deviceInfo == null)
                {
                    deviceInfo = await CollectDeviceInfo();
                }
                return deviceInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy thông tin thiết bị: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// refresh token
        /// </summary>
        public async Task RefreshToken()
        {
            try
            {
                var refreshToken = await _localStorage.GetItemAsStringAsync("refreshToken");
                if (string.IsNullOrEmpty(refreshToken))
                {
                    Console.WriteLine("Refresh token không tồn tại");
                    await Logout();
                    return;
                }

                var response = await _httpClient.PostAsJsonAsync("http://localhost:5166/api/User/refresh-Token", refreshToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<UserLoginResponseVM>>();
                    if (result != null && result.Success && result.Data != null)
                    {
                        await _localStorage.SetItemAsStringAsync("token", result.Data.AccessToken);
                        await _localStorage.SetItemAsStringAsync("refreshToken", result.Data.RefreshToken);
                    }
                }
                else {
                    await Logout();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi làm mới token: {ex.Message}");
            }
        }
    }
}