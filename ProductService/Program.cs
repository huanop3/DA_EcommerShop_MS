using Microsoft.EntityFrameworkCore;
using ProductService.Models.dbProduct;
using ProductService.Models.AutoMapperProfiles;

var builder = WebApplication.CreateBuilder(args);

// Swagger + AutoMapper + Controllers
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(ProductProfile)); // Đăng ký AutoMapper

// DbContext
var connectionString = builder.Configuration.GetConnectionString("ProductDbService");
builder.Services.AddDbContext<ProductDBContext>(options =>
    options
        .UseLazyLoadingProxies(false)
        .UseSqlServer(connectionString));

// Controller
builder.Services.AddControllers();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("allow_all", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("allow_all");
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
