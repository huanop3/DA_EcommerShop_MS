using System;
using System.Collections.Generic;

namespace ProductService.Models.dbProduct;

public partial class ProductEvent
{
    public int EventId { get; set; }

    public string EventType { get; set; } = null!;

    public int ProductId { get; set; }

    public string EventData { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public bool? IsProcessed { get; set; }

    public virtual Product Product { get; set; } = null!;
}
