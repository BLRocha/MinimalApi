using Microsoft.EntityFrameworkCore;
using MinimalApi.Data;
using MinimalApi.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("database") ?? throw new InvalidOperationException("Connection string 'database' not found.");
builder.Services.AddDbContext<AppDbContext>(oprions => {
    oprions.UseNpgsql(connectionString);
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/fornecedor", async (AppDbContext context) =>
{
    var fornecedor = context.Fornecedor.Where( f => f.Ativo == true);
    return Results.Ok(fornecedor);
}).Produces<List<Fornecedor>>(200);

app.MapGet("/fornecedor{id}", async (AppDbContext context, Guid? id) =>
{
    if (id == null) return Results.NotFound();
    var fornecedor = await context.Fornecedor.FindAsync(id);
    if (fornecedor == null) return Results.NotFound();
    return Results.Ok(fornecedor);
}).Produces<Fornecedor>(200, "application/json")
  .Produces(404);

app.MapPost("/fornecedor", async (AppDbContext context, Fornecedor f) =>
{
    if (f.Validate()) {
        var data = await context.Fornecedor.AddAsync(f);
        context.SaveChanges();
        return Results.Created($"/fornecedor/{data.Entity.Id}", data.Entity);
    }
    return Results.UnprocessableEntity();
}).Produces(201)
  .Produces(422);

app.MapDelete("/fornecedor/{id}", async (AppDbContext context, Guid id) =>
{
    var data = await context.Fornecedor.FindAsync(id);
    context.Remove(data);
    context.SaveChanges();
    return Results.NoContent();

}).Produces(204);

app.MapPut("/fornecedor/{id}", async (AppDbContext context,Fornecedor f, Guid id) =>
{
    var data = await context.Fornecedor.SingleOrDefaultAsync( p => p.Id == id);
    if (data == null) {
        return Results.BadRequest();
    }
    data.Name = f.Name;
    data.Documento = f.Documento;
    data.Ativo = f.Ativo;
    context.SaveChanges();
    return Results.Ok(data);

}).Produces(200)
  .Produces(400);

app.MapGet("/fornecedor/{term}", async (AppDbContext context, string? term) =>
{
    if (String.IsNullOrWhiteSpace(term)) return Results.NotFound();
    var fornecedor = await context.Fornecedor.Where(p => 
        (p.Name.ToLower().Contains(term.ToLower()) == true ||
        p.Documento.ToLower().Contains(term) == true)
    ).ToArrayAsync();

    if (fornecedor == null) return Results.NotFound();
    return Results.Ok(fornecedor);
}).Produces<List<Fornecedor>>(200)
  .Produces(404);

app.Run();
