using AutoMapper;
using ProductService.Models.dbProduct;
using ProductService.Models.ViewModel;

namespace ProductService.Models.AutoMapperProfiles
{
    public class ProductProfile : Profile
    {
        public ProductProfile()
        {
            // Mapping từ Product → ProductViewModel
            CreateMap<Product, ProductViewModel>()
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId));

            // Mapping ngược: ProductViewModel → Product
            CreateMap<ProductViewModel, Product>()
                .ForMember(dest => dest.Category, opt => opt.Ignore()); // Vẫn bỏ qua navigation property
        }
    }
}
