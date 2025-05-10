using System;
using System.Collections.Generic;

namespace MainEcommerceService.Models.dbMainEcommer;

public partial class ProductServiceEvent
{
    public int EventId { get; set; }

    public string EventType { get; set; } = null!;

    public int ProductId { get; set; }

    public string EventData { get; set; } = null!;

    public DateTime ReceivedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsProcessed { get; set; }
}
