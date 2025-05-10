using System;
using System.Collections.Generic;

namespace MainEcommerceService.Models.dbMainEcommer;

public partial class ProductReference
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public int SellerId { get; set; }

    public int CategoryId { get; set; }

    public string? ProductTags { get; set; }

    public decimal Price { get; set; }

    public decimal? DiscountPrice { get; set; }

    public DateTime LastSyncedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual SellerProfile Seller { get; set; } = null!;
}
