using System;
using System.Collections.Generic;

namespace ProductService.Models.dbProduct;

public partial class SellerProfile
{
    public int SellerId { get; set; }

    public int UserId { get; set; }

    public string StoreName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsVerified { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
