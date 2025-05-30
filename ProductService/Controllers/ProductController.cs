using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using ProductService.Models.dbProduct;
using ProductService.Models.ViewModel;
using AutoMapper;

namespace ProductService.Controllers
{
    [Route("api/product")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly ProductDBContext _context;
        private readonly IMapper _mapper;

        public ProductController(ProductDBContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetAll(int pageIndex = 0, int pageSize = 10)
        {
            if (pageIndex < 0 || pageSize <= 0)
                return BadRequest("Tham số phân trang không hợp lệ.");

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsDeleted == null || p.IsDeleted == false)
                .OrderBy(p => p.ProductId)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var productVMs = _mapper.Map<List<ProductViewModel>>(products);
            return Ok(productVMs);
        }

        [HttpGet("getbyid/{id}")]
        [OutputCache(Duration = 60, VaryByRouteValueNames = new[] { "id" })]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id && (p.IsDeleted ?? false) == false);

            if (product == null)
                return NotFound("Mã sản phẩm không tồn tại.");

            var productVM = _mapper.Map<ProductViewModel>(product);
            return Ok(productVM);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] ProductViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var product = _mapper.Map<Product>(model);
            product.CreatedAt = DateTime.Now;
            product.IsDeleted = false;

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return Ok("Thêm sản phẩm thành công.");
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id && (p.IsDeleted ?? false) == false);
            if (existing == null) return NotFound("Không tìm thấy sản phẩm.");

            _mapper.Map(model, existing);
            existing.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok("Cập nhật sản phẩm thành công.");
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id && (p.IsDeleted ?? false) == false);
            if (product == null) return NotFound("Không tìm thấy sản phẩm.");

            product.IsDeleted = true;
            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok("Xoá sản phẩm thành công.");
        }

        [HttpGet("filter")]
        public async Task<IActionResult> Filter(
        [FromQuery] string? name,
        [FromQuery] int? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10)
        {
            if (pageIndex < 0 || pageSize <= 0)
                return BadRequest("Tham số phân trang không hợp lệ.");

            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsDeleted == null || p.IsDeleted == false)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(p => p.ProductName.Contains(name));

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            var total = await query.CountAsync();

            var products = await query
                .OrderBy(p => p.ProductId)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var productVMs = _mapper.Map<List<ProductViewModel>>(products);

            return Ok(new
            {
                Total = total,
                Items = productVMs
            });
        }


    }
}
