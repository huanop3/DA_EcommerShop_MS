using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Blazored.LocalStorage;
using MainEcommerceService.Models.ViewModel;
using MudBlazor;
using System;
using System.Threading.Tasks;

namespace BlazorWebApp.Services
{
    public class SignalRService : IAsyncDisposable
    {
        private readonly NavigationManager _navigationManager;
        private readonly ILocalStorageService _localStorage;
        private readonly ISnackbar _snackbar;
        private HubConnection? _hubConnection;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

        // Events cho user management
        public event Action<string>? UserCreated;
        public event Action<int, string>? UserUpdated;
        public event Action<int>? UserDeleted;
        public event Action<int, string>? UserStatusChanged;

        // Events cho category management
        public event Action<string>? CategoryCreated;
        public event Action<int, string>? CategoryUpdated;
        public event Action<int>? CategoryDeleted;

        // Events cho coupon management
        public event Action<string>? CouponCreated;
        public event Action<int, string>? CouponUpdated;
        public event Action<int>? CouponDeleted;
        public event Action<int, string>? CouponStatusChanged;

        // Events cho address management
        public event Action<int, string>? AddressCreated;
        public event Action<int, string>? AddressUpdated;
        public event Action<int>? AddressDeleted;
        public event Action<int, int>? DefaultAddressChanged; // userId, addressId

        // Events cho thông báo
        public event Action<string>? PrivateNotificationReceived;
        public event Action<string, string>? MessageReceived;

        // Events cho kết nối
        public event Action<string>? UserConnected;
        public event Action<string>? UserDisconnected;

        // Seller Profile events
        public event Action<string>? SellerProfileCreated;
        public event Action<int, string>? SellerProfileUpdated;
        public event Action<int>? SellerProfileDeleted;
        public event Action<int, string>? SellerProfileVerified;
        public event Action<int, string>? SellerProfileUnverified;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public SignalRService(NavigationManager navigationManager,
                             ILocalStorageService localStorage,
                             ISnackbar snackbar)
        {
            _navigationManager = navigationManager;
            _localStorage = localStorage;
            _snackbar = snackbar;
        }

        public async Task StartConnectionAsync()
        {
            await _connectionSemaphore.WaitAsync();
            try
            {
                if (_hubConnection != null && IsConnected)
                {
                    return;
                }

                string baseUrl = "http://localhost:5166/notificationHub";

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(baseUrl, options =>
                    {
                        options.AccessTokenProvider = async () =>
                        {
                            var token = await _localStorage.GetItemAsStringAsync("token");
                            return token;
                        };
                        // Tối ưu hóa transport
                        options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                        options.SkipNegotiation = true;
                    })
                    .WithAutomaticReconnect(new[] {
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5)
                    })
                    // Tối ưu hóa serialization
                    .ConfigureLogging(logging =>
                    {
                        logging.SetMinimumLevel(LogLevel.Warning);
                    })
                    .Build();

                // Đăng ký các event handlers một cách tối ưu
                RegisterEventHandlers();

                await _hubConnection.StartAsync();

                // Chỉ hiển thị thông báo khi debug
#if DEBUG
                _snackbar.Add("Connected to notification system", Severity.Success);
#endif

                // Đăng ký user connection ngay lập tức
                string userId = await GetCurrentUserIdAsync();
                if (!string.IsNullOrEmpty(userId))
                {
                    await RegisterUserConnectionAsync(userId);
                }
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Error connecting to notification hub: {ex.Message}", Severity.Error);
                Console.WriteLine($"Error connecting to notification hub: {ex.Message}");
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private void RegisterEventHandlers()
        {
            if (_hubConnection == null) return;

            // Đăng ký các event handlers cho user management với xử lý bất đồng bộ
            _hubConnection.On<string>("UserCreated", async (name) =>
            {
                await Task.Run(() => UserCreated?.Invoke(name));
            });

            _hubConnection.On<int, string>("UserUpdated", async (userId, name) =>
            {
                await Task.Run(() => UserUpdated?.Invoke(userId, name));
            });

            _hubConnection.On<int>("UserDeleted", async (userId) =>
            {
                await Task.Run(() => UserDeleted?.Invoke(userId));
            });

            _hubConnection.On<int, string>("UserStatusChanged", async (userId, status) =>
            {
                await Task.Run(() => UserStatusChanged?.Invoke(userId, status));
            });

            // Đăng ký event handlers cho thông báo
            _hubConnection.On<string>("PrivateNotification", async (message) =>
            {
                await Task.Run(() => PrivateNotificationReceived?.Invoke(message));
            });

            _hubConnection.On<string, string>("ReceiveMessage", async (user, message) =>
            {
                await Task.Run(() => MessageReceived?.Invoke(user, message));
            });

            // Đăng ký event handlers cho kết nối
            _hubConnection.On<string>("UserConnected", async (connectionId) =>
            {
                await Task.Run(() => UserConnected?.Invoke(connectionId));
            });

            _hubConnection.On<string>("UserDisconnected", async (connectionId) =>
            {
                await Task.Run(() => UserDisconnected?.Invoke(connectionId));
            });

            // Category management events
            _hubConnection.On<string>("CategoryCreated", async (name) =>
            {
                await Task.Run(() => CategoryCreated?.Invoke(name));
            });

            _hubConnection.On<int, string>("CategoryUpdated", async (categoryId, name) =>
            {
                await Task.Run(() => CategoryUpdated?.Invoke(categoryId, name));
            });

            _hubConnection.On<int>("CategoryDeleted", async (categoryId) =>
            {
                await Task.Run(() => CategoryDeleted?.Invoke(categoryId));
            });

            // Coupon management events
            _hubConnection.On<string>("CouponCreated", async (couponCode) =>
            {
                await Task.Run(() => CouponCreated?.Invoke(couponCode));
            });

            _hubConnection.On<int, string>("CouponUpdated", async (couponId, couponCode) =>
            {
                await Task.Run(() => CouponUpdated?.Invoke(couponId, couponCode));
            });

            _hubConnection.On<int>("CouponDeleted", async (couponId) =>
            {
                await Task.Run(() => CouponDeleted?.Invoke(couponId));
            });

            _hubConnection.On<int, string>("CouponStatusChanged", async (couponId, status) =>
            {
                await Task.Run(() => CouponStatusChanged?.Invoke(couponId, status));
            });

            // Address management events
            _hubConnection.On<int, string>("AddressCreated", async (userId, addressInfo) =>
            {
                await Task.Run(() => AddressCreated?.Invoke(userId, addressInfo));
            });

            _hubConnection.On<int, string>("AddressUpdated", async (userId, addressInfo) =>
            {
                await Task.Run(() => AddressUpdated?.Invoke(userId, addressInfo));
            });

            _hubConnection.On<int>("AddressDeleted", async (addressId) =>
            {
                await Task.Run(() => AddressDeleted?.Invoke(addressId));
            });

            _hubConnection.On<int, int>("DefaultAddressChanged", async (userId, addressId) =>
            {
                await Task.Run(() => DefaultAddressChanged?.Invoke(userId, addressId));
            });

            // Seller Profile management events
            _hubConnection.On<string>("SellerProfileCreated", async (storeName) =>
            {
                await Task.Run(() => SellerProfileCreated?.Invoke(storeName));
            });

            _hubConnection.On<int, string>("SellerProfileUpdated", async (sellerId, storeName) =>
            {
                await Task.Run(() => SellerProfileUpdated?.Invoke(sellerId, storeName));
            });

            _hubConnection.On<int>("SellerProfileDeleted", async (sellerId) =>
            {
                await Task.Run(() => SellerProfileDeleted?.Invoke(sellerId));
            });

            _hubConnection.On<int, string>("SellerProfileVerified", async (sellerId, storeName) =>
            {
                await Task.Run(() => SellerProfileVerified?.Invoke(sellerId, storeName));
            });

            _hubConnection.On<int, string>("SellerProfileUnverified", async (sellerId, storeName) =>
            {
                await Task.Run(() => SellerProfileUnverified?.Invoke(sellerId, storeName));
            });

            // Xử lý khi kết nối đóng
            _hubConnection.Closed += async (error) =>
            {
                if (error != null)
                {
                    Console.WriteLine($"Connection closed due to error: {error.Message}");
                }
                return;
            };
        }

        private async Task<string> GetCurrentUserIdAsync()
        {
            try
            {
                var userId = await _localStorage.GetItemAsStringAsync("userId");
                return userId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Tối ưu hóa các phương thức gửi tin nhắn
        public async Task RegisterUserConnectionAsync(string userId)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("RegisterUserConnection", userId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error registering user connection: {ex.Message}");
                }
            }
        }

        public async Task SendMessageAsync(string user, string message)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("SendMessage", user, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message: {ex.Message}");
                }
            }
        }

        public async Task NotifyUserUpdatedAsync(int userId, string name)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyUserUpdated", userId, name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying user updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyUserCreatedAsync(string username)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyUserCreated", username);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying user created: {ex.Message}");
                }
            }
        }
        // Look for the class containing NotifyUserUpdatedAsync method
        public async Task NotifyUserDeletedAsync(int userId)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.SendAsync("NotifyUserDeleted", userId);
            }
        }

        // Category notification methods
        public async Task NotifyCategoryCreatedAsync(string categoryName)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyCategoryCreated", categoryName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying category created: {ex.Message}");
                }
            }
        }

        public async Task NotifyCategoryUpdatedAsync(int categoryId, string categoryName)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyCategoryUpdated", categoryId, categoryName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying category updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyCategoryDeletedAsync(int categoryId)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyCategoryDeleted", categoryId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying category deleted: {ex.Message}");
                }
            }
        }

        // Coupon notification methods
        public async Task NotifyCouponCreatedAsync(string couponCode)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyCouponCreated", couponCode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying coupon created: {ex.Message}");
                }
            }
        }

        public async Task NotifyCouponUpdatedAsync(int couponId, string couponCode)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyCouponUpdated", couponId, couponCode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying coupon updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyCouponDeletedAsync(int couponId)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyCouponDeleted", couponId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying coupon deleted: {ex.Message}");
                }
            }
        }

        public async Task NotifyCouponStatusChangedAsync(int couponId, string status)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyCouponStatusChanged", couponId, status);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying coupon status changed: {ex.Message}");
                }
            }
        }

        // Address notification methods
        public async Task NotifyAddressCreatedAsync(int userId, string addressInfo)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyAddressCreated", userId, addressInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying address created: {ex.Message}");
                }
            }
        }

        public async Task NotifyAddressUpdatedAsync(int userId, string addressInfo)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyAddressUpdated", userId, addressInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying address updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyAddressDeletedAsync(int addressId)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyAddressDeleted", addressId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying address deleted: {ex.Message}");
                }
            }
        }

        public async Task NotifyDefaultAddressChangedAsync(int userId, int addressId)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyDefaultAddressChanged", userId, addressId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying default address changed: {ex.Message}");
                }
            }
        }

        // Notification methods for Seller Profile
        public async Task NotifySellerProfileCreatedAsync(string storeName)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifySellerProfileCreated", storeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile created: {ex.Message}");
                }
            }
        }

        public async Task NotifySellerProfileUpdatedAsync(int sellerId, string storeName)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifySellerProfileUpdated", sellerId, storeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile updated: {ex.Message}");
                }
            }
        }

        public async Task NotifySellerProfileDeletedAsync(int sellerId)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifySellerProfileDeleted", sellerId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile deleted: {ex.Message}");
                }
            }
        }

        public async Task NotifySellerProfileVerifiedAsync(int sellerId, string storeName)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifySellerProfileVerified", sellerId, storeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile verified: {ex.Message}");
                }
            }
        }

        public async Task NotifySellerProfileUnverifiedAsync(int sellerId, string storeName)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifySellerProfileUnverified", sellerId, storeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile unverified: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Thông báo khi user role được cập nhật
        /// </summary>
        public async Task NotifyUserRoleUpdatedAsync(int userId, string username, string newRole)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyUserRoleUpdated", userId, username, newRole);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying user role updated: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Thông báo khi user status thay đổi
        /// </summary>
        public async Task NotifyUserStatusChangedAsync(int userId, string status)
        {
            if (_hubConnection is not null && IsConnected)
            {
                try
                {
                    await _hubConnection.SendAsync("NotifyUserStatusChanged", userId, status);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying user status changed: {ex.Message}");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _connectionSemaphore?.Dispose();

            if (_hubConnection is not null)
            {
                try
                {
                    await _hubConnection.StopAsync();
                    await _hubConnection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing connection: {ex.Message}");
                }
            }
        }
    }
}