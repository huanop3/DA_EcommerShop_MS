using MainEcommerceService.Models.dbMainEcommer;

public interface ICategoryRepository : IRepository<Category>
{
    // Add custom methods for Category here if needed
}

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(MainEcommerDBContext context) : base(context)
    {
    }
}