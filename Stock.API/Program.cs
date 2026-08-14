using Microsoft.EntityFrameworkCore;
using Stock.API.Data;
using Stock.API.Models;

var builder = WebApplication.CreateBuilder(args);

// DbContext com PostgreSQL
builder.Services.AddDbContext<StockDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS para permitir chamadas do Angular
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

// --- ENDPOINTS DE ESTOQUE ---

// 1. Listar todos os produtos
app.MapGet("/api/products", async (StockDbContext db) =>
{
    var products = await db.Products.AsNoTracking().ToListAsync();
    return Results.Ok(products);
});

// 2. Buscar produto por código
app.MapGet("/api/products/{code}", async (string code, StockDbContext db) =>
{
    var product = await db.Products.FirstOrDefaultAsync(p => p.Code == code);
    return product is not null ? Results.Ok(product) : Results.NotFound("Produto não encontrado.");
});

// 3. Cadastrar Produto
app.MapPost("/api/products", async (Product product, StockDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(product.Code) || string.IsNullOrWhiteSpace(product.Description))
        return Results.BadRequest("Código e Descrição são obrigatórios.");

    var exists = await db.Products.AnyAsync(p => p.Code == product.Code);
    if (exists)
        return Results.Conflict("Já existe um produto cadastrado com este código.");

    db.Products.Add(product);
    await db.SaveChangesAsync();

    return Results.Created($"/api/products/{product.Code}", product);
});

// 4. Dar baixa no saldo (Usado ao emitir/imprimir a Nota Fiscal)
app.MapPost("/api/products/deduct-stock", async (DeductStockRequest request, StockDbContext db) =>
{
    try
    {
        // 1. Checa Idempotência: Se essa transação já foi executada, ignora
        if (!string.IsNullOrEmpty(request.TransactionId))
        {
            var alreadyProcessed = await db.ProcessedTransactions.AnyAsync(t => t.TransactionId == request.TransactionId);
            if (alreadyProcessed)
            {
                return Results.Ok(new { Message = "Transação já processada anteriormente (Idempotente).", Success = true });
            }
        }

        var product = await db.Products.FirstOrDefaultAsync(p => p.Code == request.ProductCode);

        if (product is null)
            return Results.NotFound($"Produto com código '{request.ProductCode}' não encontrado.");

        if (product.QuantityOnHand < request.Quantity)
            return Results.BadRequest($"Saldo insuficiente para o produto '{product.Description}'. Saldo atual: {product.QuantityOnHand}");

        // Abate o estoque
        product.QuantityOnHand -= request.Quantity;

        // Registra a transação processada
        if (!string.IsNullOrEmpty(request.TransactionId))
        {
            db.ProcessedTransactions.Add(new ProcessedTransaction { TransactionId = request.TransactionId });
        }

        await db.SaveChangesAsync();

        return Results.Ok(new { Message = "Estoque atualizado com sucesso.", NewQuantity = product.QuantityOnHand });
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict("O saldo deste produto foi alterado por outra operação simultânea. Tente novamente.");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao atualizar estoque: {ex.Message}");
    }
});

app.Run();

// DTO para requisição de baixa de estoque
public record DeductStockRequest(string ProductCode, int Quantity, string? TransactionId = null);