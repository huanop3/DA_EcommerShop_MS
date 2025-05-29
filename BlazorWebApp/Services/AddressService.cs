using Blazored.LocalStorage;
using MainEcommerceService.Models.ViewModel;

namespace BlazorWebApp.Services
{
    public class AddressService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;

        public AddressService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }


        public async Task<IEnumerable<AddressVM>> GetAllAddressesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://localhost:5166/api/Address/GetAllAddresses");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<AddressVM>>>();
                
                if (result != null && result.Success && result.Data != null)
                {
                    // Lấy ra các address có IsDeleted = false
                    return result.Data.Where(address => address.IsDeleted != true);
                }
                return new List<AddressVM>();
            }
            catch (Exception)
            {
                return new List<AddressVM>();
            }
        }

        public async Task<IEnumerable<AddressVM>> GetAddressesByUserIdAsync(int userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://localhost:5166/api/Address/GetAddressesByUserId?userId={userId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<AddressVM>>>();
                    
                    if (result != null && result.Success && result.Data != null)
                    {
                        // Lấy ra các address có IsDeleted != true
                        return result.Data.Where(address => address.IsDeleted != true);
                    }
                }
                
                // Trả về empty list thay vì null khi không có data hoặc có lỗi
                return new List<AddressVM>();
            }
            catch (Exception)
            {
                // Trả về empty list khi có exception
                return new List<AddressVM>();
            }
        }

        public async Task<AddressVM> GetAddressByIdAsync(int addressId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://localhost:5166/api/Address/GetAddressById?addressId={addressId}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<AddressVM>>();
                
                if (result != null && result.Success)
                {
                    return result.Data;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> CreateAddressAsync(AddressVM address)
        {
            try
            {
                
                // Kiểm tra xem user đã có địa chỉ nào chưa
                var existingAddresses = await GetAddressesByUserIdAsync(address.UserId);
                
                // Nếu đây là địa chỉ đầu tiên, tự động set làm default
                if (!existingAddresses.Any())
                {
                    address.IsDefault = true;
                }
                // Nếu user muốn set làm default và đã có địa chỉ khác
                else if (address.IsDefault == true)
                {
                    // Unset tất cả địa chỉ default khác trước
                    await UnsetOtherDefaultAddresses(address.UserId);
                }
                
                var response = await _httpClient.PostAsJsonAsync($"http://localhost:5166/api/Address/CreateAddress", address);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
                    return result?.Success ?? false;
                }
                
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> UpdateAddressAsync(AddressVM address)
        {
            try
            {
                
                // Nếu user muốn set làm default, cần unset địa chỉ default cũ
                if (address.IsDefault == true)
                {
                    await UnsetOtherDefaultAddresses(address.UserId, address.AddressId);
                }
                
                var response = await _httpClient.PutAsJsonAsync($"http://localhost:5166/api/Address/UpdateAddress", address);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
                
                if (result != null)
                {
                    return result.Success;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeleteAddressAsync(int addressId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"http://localhost:5166/api/Address/DeleteAddress?addressId={addressId}");
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
                
                if (result != null)
                {
                    return result.Success;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SetDefaultAddressAsync(int addressId, int userId)
        {
            try
            {
                
                // Unset tất cả địa chỉ default khác của user này
                await UnsetOtherDefaultAddresses(userId, addressId);
                
                var response = await _httpClient.PutAsync($"http://localhost:5166/api/Address/SetDefaultAddress?addressId={addressId}&userId={userId}", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
                    return result?.Success ?? false;
                }
                
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task UnsetOtherDefaultAddresses(int userId, int? excludeAddressId = null)
        {
            try
            {
                var addresses = await GetAddressesByUserIdAsync(userId);
                foreach (var addr in addresses.Where(a => a.IsDefault == true && a.AddressId != excludeAddressId))
                {
                    addr.IsDefault = false;
                    await _httpClient.PutAsJsonAsync($"http://localhost:5166/api/Address/UpdateAddress", addr);
                }
            }
            catch (Exception)
            {
                // Log error but don't throw - this is not critical
            }
        }
    }
}
