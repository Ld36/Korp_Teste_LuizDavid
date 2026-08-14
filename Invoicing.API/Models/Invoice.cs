using System.ComponentModel.DataAnnotations;

namespace Invoicing.API.Models;

public enum InvoiceStatus
{
    Open = 0,    
    Closed = 1
}

public class Invoice
{
    [Key]
    public int Id { get; set; }

    public int SequenceNumber { get; set; } // Numeração sequencial

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open; // Inicia Aberta

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<InvoiceItem> Items { get; set; } = new(); // Múltiplos produtos
}