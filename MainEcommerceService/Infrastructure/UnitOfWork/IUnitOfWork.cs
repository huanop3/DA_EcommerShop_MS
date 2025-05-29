
using MainEcommerceService.Models.dbMainEcommer;

public interface IUnitOfWork : IAsyncDisposable
{
    public IUserRepository _userRepository { get; }
    public IUserRoleRepository _userRoleRepository { get; }
    public IRoleRepository _roleRepository { get; }
    public IClientRepository _clientRepository { get; }
    public ILoginLogRepository _loginLogRepository { get; }
    public ICouponRepository _couponRepository { get; }
    public IAddressRepository _addressRepository { get; }
    public ISellerProfileRepository _sellerProfileRepository { get; }
    public IRefreshTokenRepository _refreshTokenRepository { get; }
    Task BeginTransaction();
    Task CommitTransaction();
    Task RollbackTransaction();
    
    Task<int> SaveChangesAsync();
}

public class UnitOfWork : IUnitOfWork
{
    public IUserRepository _userRepository { get;}
    public IUserRoleRepository _userRoleRepository { get; }
    public IRoleRepository _roleRepository { get; }
    public IClientRepository _clientRepository { get; }
    public ILoginLogRepository _loginLogRepository { get; }
    public ICouponRepository _couponRepository { get; }
    // public ICategoryRepository _categoryRepository { get; }
    public IRefreshTokenRepository _refreshTokenRepository { get; }
    public IAddressRepository _addressRepository { get; }
    public ISellerProfileRepository _sellerProfileRepository { get; }

    private readonly MainEcommerDBContext  _context;

    public UnitOfWork(MainEcommerDBContext context, IUserRepository userRepository, IUserRoleRepository userRoleRepository, IRoleRepository roleRepository, IClientRepository clientRepository, ILoginLogRepository loginLogRepository, ICouponRepository couponRepository, IRefreshTokenRepository refreshTokenRepository, IAddressRepository addressRepository, ISellerProfileRepository sellerProfileRepository)
    {
        _context = context;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _clientRepository = clientRepository;
        _loginLogRepository = loginLogRepository;
        _couponRepository = couponRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _addressRepository = addressRepository;
        _sellerProfileRepository = sellerProfileRepository;
    }
    public async Task BeginTransaction()
    {
        await _context.Database.BeginTransactionAsync();
    }
    public async Task CommitTransaction()
    {
        await _context.Database.CommitTransactionAsync();
    }
    public async Task RollbackTransaction()
    {
        await _context.Database.RollbackTransactionAsync();
    }
    public Task<int> SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}




