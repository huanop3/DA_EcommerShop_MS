using Blazored.LocalStorage;
using MainEcommerceService.Models.ViewModel;

namespace BlazorWebApp.Services
{
    public class UserService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        public IEnumerable<UserVM> userVM { get; set; }

        public UserService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }
        private async Task SetAuthorizationHeader()
        {
            var token = await _localStorage.GetItemAsStringAsync("token");
            if (string.IsNullOrEmpty(token))
            {
                // Nếu không có token, thử refresh
                token = await _localStorage.GetItemAsStringAsync("refreshToken");
            }

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }
        public async Task<IEnumerable<UserVM>> GetAllUserAsync()
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/User/GetAllUser");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<UserVM>>>();
            //Lấy ra các user có IsDeleted = false
            var activeUsers = result.Data.Where(user => user.IsDeleted == false);
            if (result != null)
            {
                userVM = activeUsers;
            }
            return userVM;
        }

        public async Task<IEnumerable<UserVM>> GetUsersByPageAsync(int pageIndex, int pageSize)
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/User/GetUsersByPage?pageIndex={pageIndex}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<UserVM>>>();
            //Lấy ra các user có IsActive = true
            var activeUsers = result.Data.Where(user => user.IsActive == true);
            if (result != null)
            {
                userVM = activeUsers;
            }
            return userVM;
        }
        public async Task<IEnumerable<RoleVM>> GetAllRoleAsync()
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/User/GetAllRole");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<RoleVM>>>();
            if (result != null)
            {
                return result.Data;
            }
            return null;
        }
        public async Task<bool> UpdateUserAsync(UserListVM user)
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"http://localhost:5166/api/User/UpdateUser", user);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            if (result != null)
            {
                return result.Success;
            }
            return result.Success;
        }
        public async Task<bool> DeleteUserAsync(string id)
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"http://localhost:5166/api/User/DeleteUser?id={id}");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            if (result != null)
            {
                return true;
            }
            return false;
        }
        public async Task<ProfileVM> GetProfileAsync(string userName)
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/User/GetUserProfile?userName={userName}");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<ProfileVM>>();
            if (result != null)
            {
                return result.Data;
            }
            return null;
        }
        public async Task<bool> UpdateProfileAsync(ProfileVM profile)
        {
            await SetAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"http://localhost:5166/api/User/UpdateUserProfile", profile);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            if (result != null)
            {
                return result.Success;
            }
            return false;
        }
        
    }
}