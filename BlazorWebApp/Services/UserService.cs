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


        public async Task<IEnumerable<UserVM>> GetAllUserAsync()
        {

            var response = await _httpClient.GetAsync("http://localhost:5166/api/User/GetAllUser");
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<UserVM>>>();
            userVM = result?.Data?.Where(user => user.IsDeleted == false) ?? Enumerable.Empty<UserVM>();
            return userVM;
        }

        public async Task<IEnumerable<UserVM>> GetUsersByPageAsync(int pageIndex, int pageSize)
        {

            var response = await _httpClient.GetAsync($"http://localhost:5166/api/User/GetUsersByPage?pageIndex={pageIndex}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<UserVM>>>();
            userVM = result?.Data?.Where(user => user.IsActive == true) ?? Enumerable.Empty<UserVM>();
            return userVM;
        }

        public async Task<IEnumerable<RoleVM>> GetAllRoleAsync()
        {

            var response = await _httpClient.GetAsync("http://localhost:5166/api/User/GetAllRole");
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<RoleVM>>>();
            return result?.Data ?? Enumerable.Empty<RoleVM>();
        }

        public async Task<bool> UpdateUserAsync(UserListVM user)
        {

            var response = await _httpClient.PutAsJsonAsync("http://localhost:5166/api/User/UpdateUser", user);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            return result?.Success ?? false;
        }

        public async Task<bool> DeleteUserAsync(string id)
        {

            var response = await _httpClient.DeleteAsync($"http://localhost:5166/api/User/DeleteUser?id={id}");
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            return result?.Success ?? false;
        }

        public async Task<ProfileVM> GetProfileAsync(int id)
        {

            var response = await _httpClient.GetAsync($"http://localhost:5166/api/User/GetUserProfile?userId={id}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API returned {response.StatusCode}: {errorContent}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<ProfileVM>>();
            
            if (result?.Success == true && result.Data != null)
                return result.Data;
                
            throw new InvalidOperationException($"API returned success=false or null data: {result?.Message}");
        }

        public async Task<bool> UpdateProfileAsync(ProfileVM profile)
        {
            if (profile == null) return false;
            

            var response = await _httpClient.PutAsJsonAsync("http://localhost:5166/api/User/UpdateUserProfile", profile);
            
            if (!response.IsSuccessStatusCode) return false;
            
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            return result?.Success ?? false;
        }
    }
}