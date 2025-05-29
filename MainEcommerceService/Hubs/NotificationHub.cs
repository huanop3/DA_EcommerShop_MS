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
            _logger.LogInformation($"Gửi tin nhắn từ {user}: {message}");
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        #region User Management Operations

        public async Task NotifyUserCreated(string name)
        {
            _logger.LogInformation($"Gửi thông báo tạo người dùng mới: {name}");
            await Clients.All.SendAsync("UserCreated", name);
        }

        public async Task NotifyUserUpdated(int userId, string name)
        {
            _logger.LogInformation($"Gửi thông báo cập nhật người dùng: {userId} - {name}");
            await Clients.All.SendAsync("UserUpdated", userId, name);
        }

        public async Task NotifyUserDeleted(int userId)
        {
            _logger.LogInformation($"Gửi thông báo xóa người dùng: {userId}");
            await Clients.All.SendAsync("UserDeleted", userId);
        }

        // 🔥 THÊM methods này vào NotificationHub

        /// <summary>
        /// Thông báo khi user role được cập nhật
        /// </summary>
        public async Task NotifyUserRoleUpdated(int userId, string username, string newRole)
        {
            _logger.LogInformation($"Gửi thông báo user role updated: User {userId} ({username}) -> {newRole}");

            // Gửi tới tất cả clients
            await Clients.All.SendAsync("UserUpdated", userId, username);

            // Gửi thông báo chi tiết cho admin
            await Clients.Groups("AdminGroup").SendAsync("UserRoleChanged", userId, username, newRole);

            // Gửi thông báo riêng cho user đó
            await Clients.Group($"User_{userId}").SendAsync("YourRoleUpdated", newRole, $"Your role has been updated to {newRole}!");
        }

        /// <summary>
        /// Thông báo khi user status thay đổi
        /// </summary>
        public async Task NotifyUserStatusChanged(int userId, string status)
        {
            _logger.LogInformation($"Gửi thông báo user status changed: User {userId} -> {status}");
            await Clients.All.SendAsync("UserStatusChanged", userId, status);

            // Gửi tới tất cả admin clients
            await Clients.Groups("AdminGroup").SendAsync("UserStatusChanged", userId, status);

            // Gửi thông báo cho user đó
            var message = status == "Active" ? "Your account has been activated" : "Your account has been deactivated";
            await Clients.Group($"User_{userId}").SendAsync("YourStatusChanged", status, message);
        }
        #endregion

        #region Category Management Operations

        public async Task NotifyCategoryCreated(string categoryName)
        {
            _logger.LogInformation($"Gửi thông báo tạo category mới: {categoryName}");
            await Clients.All.SendAsync("CategoryCreated", categoryName);
        }

        public async Task NotifyCategoryUpdated(int categoryId, string categoryName)
        {
            _logger.LogInformation($"Gửi thông báo cập nhật category: {categoryId} - {categoryName}");
            await Clients.All.SendAsync("CategoryUpdated", categoryId, categoryName);
        }

        public async Task NotifyCategoryDeleted(int categoryId)
        {
            _logger.LogInformation($"Gửi thông báo xóa category: {categoryId}");
            await Clients.All.SendAsync("CategoryDeleted", categoryId);
        }

        #endregion

        #region Coupon Management Operations

        public async Task NotifyCouponCreated(string couponCode)
        {
            _logger.LogInformation($"Gửi thông báo tạo coupon mới: {couponCode}");
            await Clients.All.SendAsync("CouponCreated", couponCode);
        }

        public async Task NotifyCouponUpdated(int couponId, string couponCode)
        {
            _logger.LogInformation($"Gửi thông báo cập nhật coupon: {couponId} - {couponCode}");
            await Clients.All.SendAsync("CouponUpdated", couponId, couponCode);
        }

        public async Task NotifyCouponDeleted(int couponId)
        {
            _logger.LogInformation($"Gửi thông báo xóa coupon: {couponId}");
            await Clients.All.SendAsync("CouponDeleted", couponId);
        }

        public async Task NotifyCouponStatusChanged(int couponId, string status)
        {
            _logger.LogInformation($"Gửi thông báo thay đổi trạng thái coupon: {couponId} - {status}");
            await Clients.All.SendAsync("CouponStatusChanged", couponId, status);
        }

        #endregion

        #region Address Management Operations

        public async Task NotifyAddressCreated(int userId, string addressInfo)
        {
            _logger.LogInformation($"Gửi thông báo tạo address mới cho user {userId}: {addressInfo}");

            // Gửi tới tất cả clients
            await Clients.All.SendAsync("AddressCreated", userId, addressInfo);

            // Gửi thông báo riêng cho user đó
            await Clients.Group($"User_{userId}").SendAsync("UserAddressCreated", addressInfo);
        }

        public async Task NotifyAddressUpdated(int userId, string addressInfo)
        {
            _logger.LogInformation($"Gửi thông báo cập nhật address cho user {userId}: {addressInfo}");

            // Gửi tới tất cả clients
            await Clients.All.SendAsync("AddressUpdated", userId, addressInfo);

            // Gửi thông báo riêng cho user đó
            await Clients.Group($"User_{userId}").SendAsync("UserAddressUpdated", addressInfo);
        }

        public async Task NotifyAddressDeleted(int addressId)
        {
            _logger.LogInformation($"Gửi thông báo xóa address: {addressId}");
            await Clients.All.SendAsync("AddressDeleted", addressId);
        }

        public async Task NotifyDefaultAddressChanged(int userId, int addressId)
        {
            _logger.LogInformation($"Gửi thông báo thay đổi default address cho user {userId}: address {addressId}");

            // Gửi tới tất cả clients
            await Clients.All.SendAsync("DefaultAddressChanged", userId, addressId);

            // Gửi thông báo riêng cho user đó
            await Clients.Group($"User_{userId}").SendAsync("UserDefaultAddressChanged", addressId);
        }

        #endregion

        #region Seller Profile Management Operations

        /// <summary>
        /// Thông báo khi có seller profile mới được tạo
        /// </summary>
        public async Task NotifySellerProfileCreated(string storeName)
        {
            _logger.LogInformation($"Gửi thông báo tạo seller profile mới: {storeName}");

            // Gửi tới tất cả clients (đồng nhất với frontend)
            await Clients.All.SendAsync("SellerProfileCreated", storeName);

            // Gửi tới admin group
            await Clients.Groups("AdminGroup").SendAsync("NewSellerApplication", storeName);
        }

        /// <summary>
        /// Thông báo khi seller profile được cập nhật
        /// </summary>
        public async Task NotifySellerProfileUpdated(int sellerId, string storeName)
        {
            _logger.LogInformation($"Gửi thông báo cập nhật seller profile: {sellerId} - {storeName}");

            // Gửi tới tất cả clients (đồng nhất với frontend)
            await Clients.All.SendAsync("SellerProfileUpdated", sellerId, storeName);

            // Gửi thông báo riêng cho seller đó (nếu đang online)
            await Clients.Group($"Seller_{sellerId}").SendAsync("YourSellerProfileUpdated", storeName);
        }

        /// <summary>
        /// Thông báo khi seller profile bị xóa
        /// </summary>
        public async Task NotifySellerProfileDeleted(int sellerId)
        {
            _logger.LogInformation($"Gửi thông báo xóa seller profile: {sellerId}");

            // Gửi tới tất cả clients (đồng nhất với frontend)
            await Clients.All.SendAsync("SellerProfileDeleted", sellerId);

            // Gửi thông báo riêng cho seller đó (nếu đang online)
            await Clients.Group($"Seller_{sellerId}").SendAsync("YourSellerProfileDeleted", "Your seller profile has been deleted by admin");
        }

        /// <summary>
        /// Thông báo khi seller profile được xác minh
        /// </summary>
        public async Task NotifySellerProfileVerified(int sellerId, string storeName)
        {
            _logger.LogInformation($"Gửi thông báo xác minh seller profile: {sellerId} - {storeName}");

            // Gửi tới tất cả clients (đồng nhất với frontend)
            await Clients.All.SendAsync("SellerProfileVerified", sellerId, storeName);

            // Gửi thông báo riêng cho seller đó (nếu đang online)
            await Clients.Group($"Seller_{sellerId}").SendAsync("YourSellerProfileVerified", storeName, "Congratulations! Your seller profile has been verified.");

            // Gửi thông báo cho user owner của seller profile
            var userId = GetUserIdFromSellerId(sellerId);
            await Clients.Group($"User_{userId}").SendAsync("SellerVerificationApproved", storeName);
        }

        /// <summary>
        /// Thông báo khi seller profile bị hủy xác minh
        /// </summary>
        public async Task NotifySellerProfileUnverified(int sellerId, string storeName)
        {
            _logger.LogInformation($"Gửi thông báo hủy xác minh seller profile: {sellerId} - {storeName}");

            // Gửi tới tất cả clients (đồng nhất với frontend)
            await Clients.All.SendAsync("SellerProfileUnverified", sellerId, storeName);

            // Gửi thông báo riêng cho seller đó (nếu đang online)
            await Clients.Group($"Seller_{sellerId}").SendAsync("YourSellerProfileUnverified", storeName, "Your seller verification has been revoked.");

            // Gửi thông báo cho user owner của seller profile
            var userId = GetUserIdFromSellerId(sellerId);
            await Clients.Group($"User_{userId}").SendAsync("SellerVerificationRevoked", storeName);
        }

        #endregion

        #region User Groups Management

        // Đăng ký user-connection mapping
        public async Task RegisterUserConnection(string userId)
        {
            _userConnections[Context.ConnectionId] = userId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            _logger.LogInformation($"User {userId} đã đăng ký kết nối với ID: {Context.ConnectionId}");
        }

        // Thêm user vào admin group
        [Authorize(Roles = "Admin")]
        public async Task JoinAdminGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "AdminGroup");
            _logger.LogInformation($"Admin {Context.ConnectionId} đã join AdminGroup");
        }

        // Xóa user khỏi admin group
        [Authorize(Roles = "Admin")]
        public async Task LeaveAdminGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AdminGroup");
            _logger.LogInformation($"Admin {Context.ConnectionId} đã leave AdminGroup");
        }

        // Gửi thông báo riêng cho từng user
        public async Task SendPrivateNotification(string userId, string message)
        {
            _logger.LogInformation($"Gửi thông báo riêng đến user {userId}: {message}");
            await Clients.Group($"User_{userId}").SendAsync("PrivateNotification", message);
        }

        // Gửi thông báo tới tất cả admin
        [Authorize(Roles = "Admin")]
        public async Task SendAdminBroadcast(string message, string type = "info")
        {
            _logger.LogInformation($"Gửi thông báo broadcast tới admin: {message}");
            await Clients.Groups("AdminGroup").SendAsync("AdminBroadcast", message, type);
        }

        #endregion

        #region Seller Groups Management

        /// <summary>
        /// Đăng ký seller vào group để nhận thông báo riêng
        /// </summary>
        public async Task RegisterSellerConnection(int sellerId, int userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Seller_{sellerId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            _logger.LogInformation($"Seller {sellerId} (User {userId}) đã đăng ký kết nối với ID: {Context.ConnectionId}");
        }

        /// <summary>
        /// Xóa seller khỏi group
        /// </summary>
        public async Task UnregisterSellerConnection(int sellerId, int userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Seller_{sellerId}");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            _logger.LogInformation($"Seller {sellerId} (User {userId}) đã hủy đăng ký kết nối");
        }

        /// <summary>
        /// Gửi thông báo riêng cho một seller
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task SendPrivateSellerNotification(int sellerId, string message, string type = "info")
        {
            _logger.LogInformation($"Gửi thông báo riêng đến seller {sellerId}: {message}");
            await Clients.Group($"Seller_{sellerId}").SendAsync("PrivateSellerNotification", message, type);
        }

        /// <summary>
        /// Gửi thông báo broadcast cho tất cả sellers
        /// </summary>
        [Authorize(Roles = "Admin")]
        public async Task SendSellerBroadcast(string message, string type = "info")
        {
            _logger.LogInformation($"Gửi thông báo broadcast tới tất cả sellers: {message}");

            // Gửi tới tất cả verified sellers
            await Clients.Groups("VerifiedSellers").SendAsync("SellerBroadcast", message, type);
        }

        #endregion

        #region Connection Management

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client kết nối: {Context.ConnectionId}");

            // Kiểm tra nếu là admin thì tự động join AdminGroup
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "AdminGroup");
                _logger.LogInformation($"Admin {Context.ConnectionId} đã tự động join AdminGroup");
            }

            // Kiểm tra nếu là seller thì join VerifiedSellers group
            if (Context.User?.IsInRole("Seller") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "VerifiedSellers");
                _logger.LogInformation($"Seller {Context.ConnectionId} đã tự động join VerifiedSellers");
            }

            // Gửi thông báo user connected (đồng nhất với frontend)
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
                _logger.LogInformation($"Đã xóa user {userId} khỏi nhóm User_{userId}");
            }

            // Xóa khỏi AdminGroup nếu là admin
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AdminGroup");
                _logger.LogInformation($"Admin {Context.ConnectionId} đã rời AdminGroup");
            }

            // Xóa khỏi VerifiedSellers nếu là seller
            if (Context.User?.IsInRole("Seller") == true)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "VerifiedSellers");
                _logger.LogInformation($"Seller {Context.ConnectionId} đã rời VerifiedSellers");
            }

            // Gửi thông báo user disconnected (đồng nhất với frontend)
            await Clients.All.SendAsync("UserDisconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        #endregion

        #region Health Check & Utilities

        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
        }

        [Authorize(Roles = "Admin")]
        public async Task GetConnectedUsersCount()
        {
            var count = _userConnections.Count;
            await Clients.Caller.SendAsync("ConnectedUsersCount", count);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper method để lấy UserId từ SellerId
        /// </summary>
        private string GetUserIdFromSellerId(int sellerId)
        {
            // TODO: Implement logic to get UserId from SellerId
            // Có thể inject SellerProfileService hoặc dùng database query
            return sellerId.ToString(); // Placeholder for now
        }

        #endregion
    }
}