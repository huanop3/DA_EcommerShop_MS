using System;
using System.Collections.Generic;

namespace MainEcommerceService.Models.dbMainEcommer;

public partial class PromotionProduct
{
    public int PromotionProductId { get; set; }

    public int PromotionId { get; set; }

    public int ProductId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual Promotion Promotion { get; set; } = null!;
}
