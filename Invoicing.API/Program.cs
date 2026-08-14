using Microsoft.EntityFrameworkCore;
using Invoicing.API.Data;
using Invoicing.API.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configura Enums como Strings no JSON
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// DbContext PostgreSQL
builder.Services.AddDbContext<InvoicingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Client HTTP para comunicação com a Stock.API
builder.Services.AddHttpClient("StockService", client =>
{
    var url = builder.Configuration["Services:StockApiUrl"] ?? "http://localhost:5284";
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(5); // Timeout para resiliência
});

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

// --- ENDPOINTS DE FATURAMENTO ---

// 1. Listar todas as Notas Fiscais com LINQ
app.MapGet("/api/invoices", async (InvoicingDbContext db) =>
{
    var invoices = await db.Invoices
        .Include(i => i.Items)
        .OrderByDescending(i => i.SequenceNumber)
        .AsNoTracking()
        .ToListAsync();

    return Results.Ok(invoices);
});

// 2. Buscar Nota por Id
app.MapGet("/api/invoices/{id:int}", async (int id, InvoicingDbContext db) =>
{
    var invoice = await db.Invoices
        .Include(i => i.Items)
        .FirstOrDefaultAsync(i => i.Id == id);

    return invoice is not null ? Results.Ok(invoice) : Results.NotFound("Nota fiscal não encontrada.");
});

// 3. Cadastrar Nova Nota Fiscal
app.MapPost("/api/invoices", async (CreateInvoiceRequest request, InvoicingDbContext db) =>
{
    if (request.Items == null || !request.Items.Any())
        return Results.BadRequest("A nota fiscal precisa ter pelo menos um produto.");

    // Calcula a próxima numeração sequencial com LINQ
    var lastSequence = await db.Invoices.MaxAsync(i => (int?)i.SequenceNumber) ?? 0;
    var nextSequence = lastSequence + 1;

    var invoice = new Invoice
    {
        SequenceNumber = nextSequence, // Numeração sequencial
        Status = InvoiceStatus.Open,     // Status inicial Aberta
        Items = request.Items.Select(item => new InvoiceItem
        {
            ProductCode = item.ProductCode,
            Quantity = item.Quantity
        }).ToList()
    };

    db.Invoices.Add(invoice);
    await db.SaveChangesAsync();

    return Results.Created($"/api/invoices/{invoice.Id}", invoice);
});

// 4. Impressão / Fechamento de Nota Fiscal (Comunicação com Estoque + Tratamento de Falhas)
app.MapPost("/api/invoices/{id:int}/print", async (int id, InvoicingDbContext db, IHttpClientFactory clientFactory) =>
{
    var invoice = await db.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id);

    if (invoice == null)
        return Results.NotFound("Nota fiscal não encontrada.");

    if (invoice.Status != InvoiceStatus.Open)
        return Results.BadRequest("Apenas notas fiscais com status 'Aberta' podem ser impressas.");

    var stockClient = clientFactory.CreateClient("StockService");

    try
    {
        // PASSO 1: Pré-validação de saldo para TODOS os itens antes de abater qualquer valor
        foreach (var item in invoice.Items)
        {
            var stockResponse = await stockClient.GetAsync($"/api/products/{item.ProductCode}");
            if (!stockResponse.IsSuccessStatusCode)
            {
                return Results.BadRequest($"O produto com código '{item.ProductCode}' não existe no Estoque.");
            }

            var product = await stockResponse.Content.ReadFromJsonAsync<ProductStockDto>();
            if (product != null && product.QuantityOnHand < item.Quantity)
            {
                return Results.BadRequest($"Saldo insuficiente para o produto '{item.ProductCode}'. Necessário: {item.Quantity}, Disponível: {product.QuantityOnHand}. A nota PERMANECEU ABERTA.");
            }
        }

        // PASSO 2: Se todos os saldos estão validados, executa as baixas com Idempotência
        foreach (var item in invoice.Items)
        {
            var idempotencyKey = $"INV_{invoice.Id}_ITEM_{item.Id}"; // Chave única por item

            var response = await stockClient.PostAsJsonAsync("/api/products/deduct-stock", new
            {
                ProductCode = item.ProductCode,
                Quantity = item.Quantity,
                TransactionId = idempotencyKey
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                return Results.BadRequest(new
                {
                    Message = $"Falha ao processar item '{item.ProductCode}'. A nota PERMANECEU ABERTA.",
                    Details = errorDetails
                });
            }
        }

        // PASSO 3: Atualiza status para Fechada
        invoice.Status = InvoiceStatus.Closed;
        await db.SaveChangesAsync();

        return Results.Ok(new { Message = "Nota fiscal impressa e fechada com sucesso!", Invoice = invoice });
    }
    catch (HttpRequestException)
    {
        // Tratamento de Falha de Comunicação
        return Results.Json(
            new { Message = "O serviço de Estoque está indisponível ou inacessível no momento. A nota fiscal permaneceu ABERTA." },
            statusCode: 503);
    }
});

app.Run();

// DTOs
public record CreateInvoiceRequest(List<CreateInvoiceItemRequest> Items);
public record CreateInvoiceItemRequest(string ProductCode, int Quantity);
public record ProductStockDto(string Code, string Description, int QuantityOnHand);