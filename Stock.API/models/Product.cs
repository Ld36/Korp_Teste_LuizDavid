using System.ComponentModel.DataAnnotations;

namespace Stock.API.Models;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty; // Código do Produto

    [Required]
    [MaxLength(150)]
    public string Description { get; set; } = string.Empty; // Nome/Descrição

    public int QuantityOnHand { get; set; } // Saldo disponivel em estoque

    // Controle de Concorrência Otimista
    [ConcurrencyCheck]
    public uint RowVersion { get; set; }
}