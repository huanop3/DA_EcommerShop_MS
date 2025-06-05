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
        
        // Hai kết nối hub riêng biệt
        private HubConnection? _mainHubConnection; // MainEcommerceService
        private HubConnection? _productHubConnection; // ProductService
        
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

        // Events cho user management (MainEcommerceService)
        public event Action<string>? UserCreated;
        public event Action<int, string>? UserUpdated;
        public event Action<int>? UserDeleted;
        public event Action<int, string>? UserStatusChanged;

        // Events cho category management (ProductService)
        public event Action<string>? CategoryCreated;
        public event Action<int, string>? CategoryUpdated;
        public event Action<int, string>? CategoryDeleted;

        // Events cho product management (ProductService)
        public event Action<int, string, string>? ProductCreated;
        public event Action<int, string, decimal>? ProductUpdated;
        public event Action<int, string>? ProductDeleted;
        public event Action<int, string, int>? ProductStockChanged;
        public event Action<int, string, decimal, decimal>? ProductPriceChanged;
        public event Action<int, string, int, int>? LowStockAlert;

        // Events cho coupon management (MainEcommerceService)
        public event Action<string>? CouponCreated;
        public event Action<int, string>? CouponUpdated;
        public event Action<int>? CouponDeleted;
        public event Action<int, string>? CouponStatusChanged;

        // Events cho address management (MainEcommerceService)
        public event Action<int, string>? AddressCreated;
        public event Action<int, string>? AddressUpdated;
        public event Action<int>? AddressDeleted;
        public event Action<int, int>? DefaultAddressChanged;

        // Events cho thông báo
        public event Action<string>? PrivateNotificationReceived;
        public event Action<string, string>? MessageReceived;
        public event Action<string, string>? CategoryNotificationReceived;

        // Events cho kết nối
        public event Action<string>? UserConnected;
        public event Action<string>? UserDisconnected;

        // Seller Profile events (MainEcommerceService)
        public event Action<string>? SellerProfileCreated;
        public event Action<int, string>? SellerProfileUpdated;
        public event Action<int>? SellerProfileDeleted;
        public event Action<int, string>? SellerProfileVerified;
        public event Action<int, string>? SellerProfileUnverified;

        public bool IsMainHubConnected => _mainHubConnection?.State == HubConnectionState.Connected;
        public bool IsProductHubConnected => _productHubConnection?.State == HubConnectionState.Connected;

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
                // Khởi tạo kết nối đến MainEcommerceService
                await StartMainHubConnectionAsync();
                
                // Khởi tạo kết nối đến ProductService
                await StartProductHubConnectionAsync();

#if DEBUG
                _snackbar.Add("Connected to notification systems", Severity.Success);
#endif

                // Đăng ký user connection
                string userId = await GetCurrentUserIdAsync();
                if (!string.IsNullOrEmpty(userId))
                {
                    await RegisterUserConnectionAsync(userId);
                }
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Error connecting to notification hubs: {ex.Message}", Severity.Error);
                Console.WriteLine($"Error connecting to notification hubs: {ex.Message}");
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task StartMainHubConnectionAsync()
        {
            if (_mainHubConnection != null && IsMainHubConnected)
                return;

            string mainHubUrl = "https://localhost:7260/notificationHub";

            _mainHubConnection = new HubConnectionBuilder()
                .WithUrl(mainHubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await _localStorage.GetItemAsStringAsync("token");
                        return token;
                    };
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                })
                .WithAutomaticReconnect(new[] {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5)
                })
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Warning);
                })
                .Build();

            RegisterMainHubEventHandlers();
            await _mainHubConnection.StartAsync();
        }

        private async Task StartProductHubConnectionAsync()
        {
            if (_productHubConnection != null && IsProductHubConnected)
                return;

            string productHubUrl = "https://localhost:7252/notificationHub"; // Sửa từ 7262 thành 7252

            _productHubConnection = new HubConnectionBuilder()
                .WithUrl(productHubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await _localStorage.GetItemAsStringAsync("token");
                        return token;
                    };
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                })
                .WithAutomaticReconnect(new[] {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5)
                })
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Warning);
                })
                .Build();

            RegisterProductHubEventHandlers();
            await _productHubConnection.StartAsync();
        }

        private void RegisterMainHubEventHandlers()
        {
            if (_mainHubConnection == null) return;

            // User management events
            _mainHubConnection.On<string>("UserCreated", async (name) =>
            {
                await Task.Run(() => UserCreated?.Invoke(name));
            });

            _mainHubConnection.On<int, string>("UserUpdated", async (userId, name) =>
            {
                await Task.Run(() => UserUpdated?.Invoke(userId, name));
            });

            _mainHubConnection.On<int>("UserDeleted", async (userId) =>
            {
                await Task.Run(() => UserDeleted?.Invoke(userId));
            });

            _mainHubConnection.On<int, string>("UserStatusChanged", async (userId, status) =>
            {
                await Task.Run(() => UserStatusChanged?.Invoke(userId, status));
            });

            // Notification events
            _mainHubConnection.On<string>("PrivateNotification", async (message) =>
            {
                await Task.Run(() => PrivateNotificationReceived?.Invoke(message));
            });

            _mainHubConnection.On<string, string>("ReceiveMessage", async (user, message) =>
            {
                await Task.Run(() => MessageReceived?.Invoke(user, message));
            });

            // Connection events
            _mainHubConnection.On<string>("UserConnected", async (connectionId) =>
            {
                await Task.Run(() => UserConnected?.Invoke(connectionId));
            });

            _mainHubConnection.On<string>("UserDisconnected", async (connectionId) =>
            {
                await Task.Run(() => UserDisconnected?.Invoke(connectionId));
            });

            // Coupon management events
            _mainHubConnection.On<string>("CouponCreated", async (couponCode) =>
            {
                await Task.Run(() => CouponCreated?.Invoke(couponCode));
            });

            _mainHubConnection.On<int, string>("CouponUpdated", async (couponId, couponCode) =>
            {
                await Task.Run(() => CouponUpdated?.Invoke(couponId, couponCode));
            });

            _mainHubConnection.On<int>("CouponDeleted", async (couponId) =>
            {
                await Task.Run(() => CouponDeleted?.Invoke(couponId));
            });

            _mainHubConnection.On<int, string>("CouponStatusChanged", async (couponId, status) =>
            {
                await Task.Run(() => CouponStatusChanged?.Invoke(couponId, status));
            });

            // Address management events
            _mainHubConnection.On<int, string>("AddressCreated", async (userId, addressInfo) =>
            {
                await Task.Run(() => AddressCreated?.Invoke(userId, addressInfo));
            });

            _mainHubConnection.On<int, string>("AddressUpdated", async (userId, addressInfo) =>
            {
                await Task.Run(() => AddressUpdated?.Invoke(userId, addressInfo));
            });

            _mainHubConnection.On<int>("AddressDeleted", async (addressId) =>
            {
                await Task.Run(() => AddressDeleted?.Invoke(addressId));
            });

            _mainHubConnection.On<int, int>("DefaultAddressChanged", async (userId, addressId) =>
            {
                await Task.Run(() => DefaultAddressChanged?.Invoke(userId, addressId));
            });

            // Seller Profile management events
            _mainHubConnection.On<string>("SellerProfileCreated", async (storeName) =>
            {
                await Task.Run(() => SellerProfileCreated?.Invoke(storeName));
            });

            _mainHubConnection.On<int, string>("SellerProfileUpdated", async (sellerId, storeName) =>
            {
                await Task.Run(() => SellerProfileUpdated?.Invoke(sellerId, storeName));
            });

            _mainHubConnection.On<int>("SellerProfileDeleted", async (sellerId) =>
            {
                await Task.Run(() => SellerProfileDeleted?.Invoke(sellerId));
            });

            _mainHubConnection.On<int, string>("SellerProfileVerified", async (sellerId, storeName) =>
            {
                await Task.Run(() => SellerProfileVerified?.Invoke(sellerId, storeName));
            });

            _mainHubConnection.On<int, string>("SellerProfileUnverified", async (sellerId, storeName) =>
            {
                await Task.Run(() => SellerProfileUnverified?.Invoke(sellerId, storeName));
            });
        }

        private void RegisterProductHubEventHandlers()
        {
            if (_productHubConnection == null) return;

            // Product management events
            _productHubConnection.On<int, string, string>("ProductCreated", async (productId, productName, categoryName) =>
            {
                await Task.Run(() => ProductCreated?.Invoke(productId, productName, categoryName));
            });

            _productHubConnection.On<int, string, decimal>("ProductUpdated", async (productId, productName, price) =>
            {
                await Task.Run(() => ProductUpdated?.Invoke(productId, productName, price));
            });

            _productHubConnection.On<int, string>("ProductDeleted", async (productId, productName) =>
            {
                await Task.Run(() => ProductDeleted?.Invoke(productId, productName));
            });

            _productHubConnection.On<int, string, int>("ProductStockChanged", async (productId, productName, newStock) =>
            {
                await Task.Run(() => ProductStockChanged?.Invoke(productId, productName, newStock));
            });

            _productHubConnection.On<int, string, decimal, decimal>("ProductPriceChanged", async (productId, productName, oldPrice, newPrice) =>
            {
                await Task.Run(() => ProductPriceChanged?.Invoke(productId, productName, oldPrice, newPrice));
            });

            _productHubConnection.On<int, string, int, int>("LowStockAlert", async (productId, productName, currentStock, minStock) =>
            {
                await Task.Run(() => LowStockAlert?.Invoke(productId, productName, currentStock, minStock));
            });

            // Category management events
            _productHubConnection.On<string>("CategoryCreated", async (categoryName) =>
            {
                await Task.Run(() => CategoryCreated?.Invoke(categoryName));
            });

            _productHubConnection.On<int, string>("CategoryUpdated", async (categoryId, categoryName) =>
            {
                await Task.Run(() => CategoryUpdated?.Invoke(categoryId, categoryName));
            });

            _productHubConnection.On<int, string>("CategoryDeleted", async (categoryId, categoryName) =>
            {
                await Task.Run(() => CategoryDeleted?.Invoke(categoryId, categoryName));
            });

            // Category notification events
            _productHubConnection.On<string, string>("CategoryNotification", async (categoryId, message) =>
            {
                await Task.Run(() => CategoryNotificationReceived?.Invoke(categoryId, message));
            });
        }

        private void RegisterEventHandlers()
        {
            // Phương thức này giờ không dùng nữa, đã tách thành 2 phương thức riêng
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

        // User connection methods (sử dụng cả 2 hub)
        public async Task RegisterUserConnectionAsync(string userId)
        {
            var tasks = new List<Task>();

            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                tasks.Add(_mainHubConnection.SendAsync("RegisterUserConnection", userId));
            }

            if (_productHubConnection is not null && IsProductHubConnected)
            {
                tasks.Add(_productHubConnection.SendAsync("RegisterUserConnection", userId));
            }

            if (tasks.Any())
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error registering user connection: {ex.Message}");
                }
            }
        }

        // Main Hub methods (MainEcommerceService)
        public async Task SendMessageAsync(string user, string message)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("SendMessage", user, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message: {ex.Message}");
                }
            }
        }

        // User notification methods (MainEcommerceService)
        public async Task NotifyUserCreatedAsync(string username)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyUserCreated", username);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying user created: {ex.Message}");
                }
            }
        }

        public async Task NotifyUserUpdatedAsync(int userId, string name)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyUserUpdated", userId, name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying user updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyUserDeletedAsync(int userId)
        {
            if (_mainHubConnection != null && _mainHubConnection.State == HubConnectionState.Connected)
            {
                await _mainHubConnection.SendAsync("NotifyUserDeleted", userId);
            }
        }

        public async Task NotifyUserStatusChangedAsync(int userId, string status)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyUserStatusChanged", userId, status);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying user status changed: {ex.Message}");
                }
            }
        }

        // Product notification methods (ProductService)
        public async Task NotifyProductCreatedAsync(int productId, string productName, string categoryName)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyProductCreated", productId, productName, categoryName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying product created: {ex.Message}");
                }
            }
        }

        public async Task NotifyProductUpdatedAsync(int productId, string productName, decimal price)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyProductUpdated", productId, productName, price);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying product updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyProductDeletedAsync(int productId, string productName)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyProductDeleted", productId, productName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying product deleted: {ex.Message}");
                }
            }
        }

        public async Task NotifyProductStockChangedAsync(int productId, string productName, int newStock)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyProductStockChanged", productId, productName, newStock);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying product stock changed: {ex.Message}");
                }
            }
        }

        public async Task NotifyProductPriceChangedAsync(int productId, string productName, decimal oldPrice, decimal newPrice)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyProductPriceChanged", productId, productName, oldPrice, newPrice);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying product price changed: {ex.Message}");
                }
            }
        }

        public async Task NotifyLowStockAsync(int productId, string productName, int currentStock, int minStock)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyLowStock", productId, productName, currentStock, minStock);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying low stock: {ex.Message}");
                }
            }
        }

        // Category notification methods (ProductService)
        public async Task NotifyCategoryCreatedAsync(string categoryName)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyCategoryCreated", categoryName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying category created: {ex.Message}");
                }
            }
        }

        public async Task NotifyCategoryUpdatedAsync(int categoryId, string categoryName)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyCategoryUpdated", categoryId, categoryName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying category updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyCategoryDeletedAsync(int categoryId, string categoryName)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("NotifyCategoryDeleted", categoryId, categoryName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying category deleted: {ex.Message}");
                }
            }
        }

        // Category group methods (ProductService)
        public async Task JoinCategoryGroupAsync(string categoryId)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("JoinCategoryGroup", categoryId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error joining category group: {ex.Message}");
                }
            }
        }

        public async Task LeaveCategoryGroupAsync(string categoryId)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("LeaveCategoryGroup", categoryId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error leaving category group: {ex.Message}");
                }
            }
        }

        public async Task SendCategoryNotificationAsync(string categoryId, string message)
        {
            if (_productHubConnection is not null && IsProductHubConnected)
            {
                try
                {
                    await _productHubConnection.SendAsync("SendCategoryNotification", categoryId, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending category notification: {ex.Message}");
                }
            }
        }

        // Coupon notification methods (MainEcommerceService)
        public async Task NotifyCouponCreatedAsync(string couponCode)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyCouponCreated", couponCode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying coupon created: {ex.Message}");
                }
            }
        }

        public async Task NotifyCouponUpdatedAsync(int couponId, string couponCode)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyCouponUpdated", couponId, couponCode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying coupon updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyCouponDeletedAsync(int couponId)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyCouponDeleted", couponId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying coupon deleted: {ex.Message}");
                }
            }
        }

        public async Task NotifyCouponStatusChangedAsync(int couponId, string status)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyCouponStatusChanged", couponId, status);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying coupon status changed: {ex.Message}");
                }
            }
        }

        // Address notification methods (MainEcommerceService)
        public async Task NotifyAddressCreatedAsync(int userId, string addressInfo)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyAddressCreated", userId, addressInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying address created: {ex.Message}");
                }
            }
        }

        public async Task NotifyAddressUpdatedAsync(int userId, string addressInfo)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyAddressUpdated", userId, addressInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying address updated: {ex.Message}");
                }
            }
        }

        public async Task NotifyAddressDeletedAsync(int addressId)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyAddressDeleted", addressId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying address deleted: {ex.Message}");
                }
            }
        }

        public async Task NotifyDefaultAddressChangedAsync(int userId, int addressId)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyDefaultAddressChanged", userId, addressId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying default address changed: {ex.Message}");
                }
            }
        }

        // Seller Profile notification methods (MainEcommerceService)
        public async Task NotifySellerProfileCreatedAsync(string storeName)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifySellerProfileCreated", storeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile created: {ex.Message}");
                }
            }
        }

        public async Task NotifySellerProfileUpdatedAsync(int sellerId, string storeName)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifySellerProfileUpdated", sellerId, storeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile updated: {ex.Message}");
                }
            }
        }

        public async Task NotifySellerProfileDeletedAsync(int sellerId)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifySellerProfileDeleted", sellerId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile deleted: {ex.Message}");
                }
            }
        }

        public async Task NotifySellerProfileVerifiedAsync(int sellerId, string storeName)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifySellerProfileVerified", sellerId, storeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile verified: {ex.Message}");
                }
            }
        }

        public async Task NotifySellerProfileUnverifiedAsync(int sellerId, string storeName)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifySellerProfileUnverified", sellerId, storeName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying seller profile unverified: {ex.Message}");
                }
            }
        }

        public async Task NotifyUserRoleUpdatedAsync(int userId, string username, string newRole)
        {
            if (_mainHubConnection is not null && IsMainHubConnected)
            {
                try
                {
                    await _mainHubConnection.SendAsync("NotifyUserRoleUpdated", userId, username, newRole);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying user role updated: {ex.Message}");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _connectionSemaphore?.Dispose();

            var disposeTasks = new List<Task>();

            if (_mainHubConnection is not null)
            {
                disposeTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _mainHubConnection.StopAsync();
                        await _mainHubConnection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing main hub connection: {ex.Message}");
                    }
                }));
            }

            if (_productHubConnection is not null)
            {
                disposeTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _productHubConnection.StopAsync();
                        await _productHubConnection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing product hub connection: {ex.Message}");
                    }
                }));
            }

            if (disposeTasks.Any())
            {
                await Task.WhenAll(disposeTasks);
            }
        }
    }
}