using MainEcommerceService.Models.dbMainEcommer;

public interface IUserRoleRepository : IRepository<UserRole>
{
    // Add custom methods for UserRole here if needed
}

public class UserRoleRepository : Repository<UserRole>, IUserRoleRepository
{
    public UserRoleRepository(MainEcommerDbContext context) : base(context)
    {
    }
}