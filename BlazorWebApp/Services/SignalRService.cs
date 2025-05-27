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

        // Events cho thông báo
        public event Action<string>? PrivateNotificationReceived;
        public event Action<string, string>? MessageReceived;

        // Events cho kết nối
        public event Action<string>? UserConnected;
        public event Action<string>? UserDisconnected;

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