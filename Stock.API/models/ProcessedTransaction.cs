using System.ComponentModel.DataAnnotations;

namespace Stock.API.Models;

public class ProcessedTransaction
{
    [Key]
    public string TransactionId { get; set; } = string.Empty;
    
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}