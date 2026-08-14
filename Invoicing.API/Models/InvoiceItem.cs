using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Invoicing.API.Models;

public class InvoiceItem
{
    [Key]
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    [JsonIgnore]
    public Invoice? Invoice { get; set; }

    [Required]
    public string ProductCode { get; set; } = string.Empty; // Código do produto

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } // Quantidade utilizada
}