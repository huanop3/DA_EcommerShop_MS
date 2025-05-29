using Blazored.LocalStorage;
using MainEcommerceService.Models.ViewModel;

namespace BlazorWebApp.Services
{
    public class CouponService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        public IEnumerable<CouponVM> couponVM { get; set; }

        public CouponService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        public async Task<IEnumerable<CouponVM>> GetAllCouponsAsync()
        {
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/Coupon/GetAllCoupons");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<CouponVM>>>();
            if (result != null)
            {
                couponVM = result.Data;
            }
            return couponVM;
        }

        public async Task<CouponVM> GetCouponByIdAsync(int couponId)
        {
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/Coupon/GetCouponById/{couponId}");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<CouponVM>>();
            if (result != null)
            {
                return result.Data;
            }
            return null;
        }

        public async Task<CouponVM> GetCouponByCodeAsync(string couponCode)
        {
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/Coupon/GetCouponByCode/{couponCode}");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<CouponVM>>();
            if (result != null)
            {
                return result.Data;
            }
            return null;
        }

        public async Task<bool> CreateCouponAsync(CouponVM coupon)
        {
            var response = await _httpClient.PostAsJsonAsync($"http://localhost:5166/api/Coupon/CreateCoupon", coupon);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            if (result != null)
            {
                return result.Success;
            }
            return false;
        }

        public async Task<bool> UpdateCouponAsync(CouponVM coupon)
        {
            var response = await _httpClient.PutAsJsonAsync($"http://localhost:5166/api/Coupon/UpdateCoupon", coupon);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            if (result != null)
            {
                return result.Success;
            }
            return false;
        }

        public async Task<bool> DeleteCouponAsync(int couponId)
        {
            var response = await _httpClient.DeleteAsync($"http://localhost:5166/api/Coupon/DeleteCoupon/{couponId}");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            if (result != null)
            {
                return result.Success;
            }
            return false;
        }

        public async Task<bool> ActivateCouponAsync(int couponId)
        {
            var response = await _httpClient.PutAsync($"http://localhost:5166/api/Coupon/ActivateCoupon/{couponId}", null);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            if (result != null)
            {
                return result.Success;
            }
            return false;
        }

        public async Task<bool> DeactivateCouponAsync(int couponId)
        {
            var response = await _httpClient.PutAsync($"http://localhost:5166/api/Coupon/DeactivateCoupon/{couponId}", null);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<string>>();
            if (result != null)
            {
                return result.Success;
            }
            return false;
        }

        public async Task<IEnumerable<CouponVM>> GetActiveCouponsAsync()
        {
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/Coupon/GetActiveCoupons");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<IEnumerable<CouponVM>>>();
            if (result != null)
            {
                return result.Data;
            }
            return null;
        }

        public async Task<CouponVM> ValidateCouponAsync(string couponCode)
        {
            var response = await _httpClient.GetAsync($"http://localhost:5166/api/Coupon/ValidateCoupon/{couponCode}");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<HTTPResponseClient<CouponVM>>();
            if (result != null)
            {
                return result.Data;
            }
            return null;
        }
    }
}