using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;

namespace MainEcommerceService.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        private static readonly ConcurrentDictionary<string, string> _userConnections = new ConcurrentDictionary<string, string>();

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        // Phương thức chung để gửi thông báo
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        // CRUD User Operations - Các phương thức này sẽ được gọi từ UserService
        public async Task NotifyUserCreated(string name)
        {
            _logger.LogInformation($"Gửi thông báo tạo người dùng mới: {name}");
            await Clients.All.SendAsync("UserCreated",name);
        }

        [Authorize(Roles = "Admin")]
        public async Task NotifyUserUpdated(int userId, string name)
        {
            _logger.LogInformation($"Gửi thông báo cập nhật người dùng: {userId} - {name}");
            await Clients.All.SendAsync("UserUpdated", userId, name);
        }

        [Authorize(Roles = "Admin")]
        public async Task NotifyUserDeleted(int userId)
        {
            _logger.LogInformation($"Gửi thông báo xóa người dùng: {userId}");
            await Clients.All.SendAsync("UserDeleted", userId);
        }

        [Authorize(Roles = "Admin")]
        public async Task NotifyUserStatusChanged(int userId, string status)
        {
            _logger.LogInformation($"Gửi thông báo thay đổi trạng thái người dùng: {userId} - {status}");
            await Clients.All.SendAsync("UserStatusChanged", userId, status);
        }

        // Đăng ký user-connection mapping
        public async Task RegisterUserConnection(string userId)
        {
            _userConnections[Context.ConnectionId] = userId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            _logger.LogInformation($"User {userId} đã đăng ký kết nối với ID: {Context.ConnectionId}");
        }

        // Gửi thông báo riêng cho từng user
        public async Task SendPrivateNotification(string userId, string message)
        {
            _logger.LogInformation($"Gửi thông báo riêng đến user {userId}: {message}");
            await Clients.Group($"User_{userId}").SendAsync("PrivateNotification", message);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client kết nối: {Context.ConnectionId}");
            await Clients.All.SendAsync("UserConnected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation($"Client ngắt kết nối: {Context.ConnectionId}");
            
            // Xóa mapping khi user ngắt kết nối
            if (_userConnections.TryRemove(Context.ConnectionId, out string userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogInformation($"Đã xóa user {userId} khỏi nhóm");
            }
            
            await Clients.All.SendAsync("UserDisconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}