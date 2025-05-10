using System;
using System.Collections.Generic;

namespace ProductService.Models.dbProduct;

public partial class ProductTag
{
    public int ProductTagId { get; set; }

    public int ProductId { get; set; }

    public int TagId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}
