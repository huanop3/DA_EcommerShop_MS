using System.Linq.Expressions;
using MainEcommerceService.Helper;
using MainEcommerceService.Models.dbMainEcommer;
using MainEcommerceService.Models.ViewModel;
using Microsoft.EntityFrameworkCore;

public interface IUserService
{
    Task<HTTPResponseClient<UserLoginResponseVM>> Login(LoginRequestVM loginRequest);
    Task<HTTPResponseClient<String>> Register(RegisterLoginVM registerLoginVM);
    Task<HTTPResponseClient<UserLoginResponseVM>> ForgotPassword(string email);
    Task<HTTPResponseClient<UserLoginResponseVM>> ResetPassword(string token, string newPassword);
    Task<HTTPResponseClient<int>> CountLoginLog(string username);
    Task<HTTPResponseClient<UserLoginResponseVM>> RefreshToken(string tokenVM);
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtAuthService _jwtAuthService;

    public UserService(IUnitOfWork unitOfWork, JwtAuthService jwtAuthService)
    {
        _unitOfWork = unitOfWork;
        _jwtAuthService = jwtAuthService;
    }

    /// <summary>
    /// Executes an operation within a transaction with standardized error handling
    /// </summary>
    private async Task<HTTPResponseClient<T>> ExecuteInTransaction<T>(Func<Task<HTTPResponseClient<T>>> operation,int StatusCode, string errorMessage)
    {
        var response = new HTTPResponseClient<T>();
        try
        {
            await _unitOfWork.BeginTransaction();
            response = await operation();
            await _unitOfWork.CommitTransaction();
            return response;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransaction();
            response.Success = false;
            response.StatusCode = StatusCode; // Internal Server Error
            response.Message = $"{errorMessage}: {ex.Message}";
            response.DateTime = DateTime.Now;
            return response;
        }
    }

    /// <summary>
    /// Finds or creates a client device in the database
    /// </summary>
    private async Task<Client> GetOrCreateClientDevice(LoginRequestVM loginRequest)
    {
        var client = await _unitOfWork._clientRepository.SingleOrDefaultAsync(x => 
            x.DeviceId == loginRequest.DeviceID && x.ClientName == loginRequest.ClientName);
            
        if (client == null)
        {
            client = new Client
            {
                ClientName = loginRequest.ClientName,
                DeviceName = loginRequest.DeviceName,
                Description = "Thiết bị của người dùng",
                DeviceId = loginRequest.DeviceID,
                Ipaddress = loginRequest.IPAddress,
                CreatedAt = loginRequest.CollectedAt == default ? DateTime.Now : loginRequest.CollectedAt,
                UpdatedAt = DateTime.Now
            };
            
            await _unitOfWork._clientRepository.AddAsync(client);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            client.UpdatedAt = DateTime.Now;
            _unitOfWork._clientRepository.Update(client);
            await _unitOfWork.SaveChangesAsync();
        }
        
        return client;
    }

    /// <summary>
    /// Finds a user by username
    /// </summary>
    private async Task<User> GetUserByUsername(string username)
    {
        return await _unitOfWork._userRepository.SingleOrDefaultAsync(x => x.Username == username);
    }

    /// <summary>
    /// Creates a refresh token for a user
    /// </summary>
    private async Task<RefreshToken> CreateRefreshToken(User user, Client client, LoginRequestVM loginRequest)
    {
        var refreshToken = new RefreshToken
        {
            UserId = user.UserId,
            ClientId = client.ClientId,
            Token = _jwtAuthService.GenerateToken(user, 10080), // 7 days
            DeviceName = loginRequest.DeviceName,
            DeviceId = loginRequest.DeviceID,
            DeviceOs = loginRequest.DeviceOS,
            Ipaddress = loginRequest.IPAddress,
            ExpiryDate = DateTime.Now.AddDays(7),
            CreatedAt = DateTime.Now,
            IsRevoked = false,
            IsDeleted = false
        };
        
        await _unitOfWork._refreshTokenRepository.AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();
        
        return refreshToken;
    }

    public Task<HTTPResponseClient<UserLoginResponseVM>> ForgotPassword(string email)
    {
        // Khi triển khai, cần thêm các status code: 200, 404, 500
        throw new NotImplementedException();
    }

    public async Task<HTTPResponseClient<UserLoginResponseVM>> Login(LoginRequestVM loginRequest)
    {
        return await ExecuteInTransaction(async () =>
        {
            var response = new HTTPResponseClient<UserLoginResponseVM>();
            
            if (string.IsNullOrEmpty(loginRequest.DeviceID) || string.IsNullOrEmpty(loginRequest.DeviceName))
            {
                response.Success = false;
                response.StatusCode = 400; // Bad Request
                response.Message = "Không có thông tin thiết bị";
                return response;
            }
            
            // Kiểm tra giới hạn đăng nhập trước khi xác thực
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            
            // Tìm client
            var existingClient = await _unitOfWork._clientRepository.SingleOrDefaultAsync(x => 
                x.DeviceId == loginRequest.DeviceID && x.ClientName == loginRequest.ClientName);
                
            if (existingClient != null)
            {
                // Đếm số lần đăng nhập từ thiết bị này trong ngày
                var loginAttempts = await _unitOfWork._loginLogRepository.CountAsync(x => 
                    x.ClientId == existingClient.ClientId && 
                    x.LoginTime >= today && 
                    x.LoginTime < tomorrow);
                    
                if (loginAttempts >= 10) // Giới hạn 10 lần/ngày
                {
                    response.Success = false;
                    response.StatusCode = 429; // Too Many Requests
                    response.Message = "Thiết bị này đã vượt quá giới hạn số lần đăng nhập trong ngày. Vui lòng thử lại vào ngày mai.";
                    return response;
                }
            }
            
            // Verify user credentials
            var user = await GetUserByUsername(loginRequest.Username);
            
            if (user == null || !PasswordHelper.VerifyPassword(loginRequest.Password, user.PasswordHash))
            {
                // Ghi nhận lần đăng nhập thất bại để tính vào giới hạn
                if (existingClient != null)
                {
                    var failedLoginLog = new LoginLog()
                    {
                        Username = loginRequest.Username,
                        UserId = user.UserId,
                        ClientId = existingClient.ClientId,
                        IpAddress = loginRequest.IPAddress,
                        LoginTime = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsSuccessful = false
                    };
                    
                    await _unitOfWork._loginLogRepository.AddAsync(failedLoginLog);
                    await _unitOfWork.SaveChangesAsync();
                }
                
                response.Success = false;
                response.StatusCode = 401; // Unauthorized
                response.Message = "Tên đăng nhập hoặc mật khẩu không đúng";
                return response;
            }

            // Create access token
            var accessToken = _jwtAuthService.GenerateToken(user, 60); // 60 minutes
            
            // Get or create client device
            var client = await GetOrCreateClientDevice(loginRequest);
            
            // Create refresh token
            var refreshToken = await CreateRefreshToken(user, client, loginRequest);
            
            // Save login log
            var loginLog = new LoginLog()
            {
                Username = user.Username,
                UserId = user.UserId,
                ClientId = client.ClientId,
                IpAddress = loginRequest.IPAddress,
                LoginTime = DateTime.Now,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsSuccessful = true
            };
            
            await _unitOfWork._loginLogRepository.AddAsync(loginLog);
            await _unitOfWork.SaveChangesAsync();
            
            // Prepare response
            response.Data = new UserLoginResponseVM
            {
                Username = user.Username,
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
            };
            
            response.Success = true;
            response.StatusCode = 200; // OK
            response.Message = "Đăng nhập thành công";
            response.DateTime = DateTime.Now;
            return response;
        },500, "Lỗi đăng nhập");
    }

    public async Task<HTTPResponseClient<string>> Register(RegisterLoginVM registerLoginVM)
    {
        return await ExecuteInTransaction(async () =>
        {
            var response = new HTTPResponseClient<string>();
            
            // Check if user already exists
            var user = await _unitOfWork._userRepository.SingleOrDefaultAsync(x => 
                x.Username == registerLoginVM.Username || x.Email == registerLoginVM.Email);
                
            if (user == null)
            {
                // Create new user
                var newUser = new User()
                {
                    Username = registerLoginVM.Username,
                    Email = registerLoginVM.Email,
                    FirstName = registerLoginVM.FirstName,
                    LastName = registerLoginVM.LastName,
                    PhoneNumber = registerLoginVM.PhoneNumber,
                    PasswordHash = PasswordHelper.HashPassword(registerLoginVM.Password),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                await _unitOfWork._userRepository.AddAsync(newUser);
                await _unitOfWork.SaveChangesAsync();
                
                response.Success = true;
                response.StatusCode = 201; // Created
                response.Message = "Đăng ký thành công";
            }
            else
            {
                response.Success = false;
                response.StatusCode = 409; // Conflict
                response.Message = "Tên đăng nhập hoặc email đã tồn tại";
            }
            
            response.DateTime = DateTime.Now;
            return response;
        }, 500, "Đăng ký thất bại");
    }

    public Task<HTTPResponseClient<UserLoginResponseVM>> ResetPassword(string token, string newPassword)
    {
        // Khi triển khai, cần thêm các status code: 200, 400, 404
        throw new NotImplementedException();
    }
   
    public async Task<HTTPResponseClient<int>> CountLoginLog(string username)
    {
        var response = new HTTPResponseClient<int>();
        try
        {
            // Find user
            var user = await GetUserByUsername(username);
            if (user == null)
            {
                response.Success = false;
                response.StatusCode = 404; // Not Found
                response.Message = "Người dùng không tồn tại";
                return response;
            }
            
            // Get current date (reset to 00:00:00)
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            
            // Count user login logs for current day
            var count = await _unitOfWork._loginLogRepository.CountAsync(x => 
                x.UserId == user.UserId && 
                x.IsSuccessful == true && 
                x.LoginTime >= today && 
                x.LoginTime < tomorrow);
                
            response.Data = count;
            response.Success = true;
            response.StatusCode = 200; // OK
            response.Message = "Đếm số lần đăng nhập thành công trong ngày";
            response.DateTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.StatusCode = 500; // Internal Server Error
            response.Message = $"Đếm số lần đăng nhập thất bại: {ex.Message}";
            response.DateTime = DateTime.Now;
        }
        return response;
    } 

    public async Task<HTTPResponseClient<UserLoginResponseVM>> RefreshToken(string tokenVM)
    {
        return await ExecuteInTransaction(async () =>
        {
            string? newAccessToken = _jwtAuthService.RefreshToken(tokenVM);
            if (newAccessToken == null)
            {
                return new HTTPResponseClient<UserLoginResponseVM>
                {
                    Success = false,
                    StatusCode = 401, // Unauthorized
                    Message = "Token đã hết hạn hoặc không hợp lệ",
                    DateTime = DateTime.Now
                };
            }
            return new HTTPResponseClient<UserLoginResponseVM>
            {
                Success = true,
                StatusCode = 200, // OK
                Message = "Làm mới token thành công",
                DateTime = DateTime.Now,
                Data = new UserLoginResponseVM
                {
                    RefreshToken = tokenVM,
                    AccessToken = newAccessToken,
                }
            };

        },404, "Refresh token thất bại");
    }
}