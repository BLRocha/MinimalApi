using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MinimalApi.Data;
using MinimalApi.Models;
using MiniValidation;
using NetDevPack.Identity;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;

var builder = WebApplication.CreateBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("database") ?? throw new InvalidOperationException("Connection string 'database' not found.");

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
    options.UseNpgsql(connectionString,
    b => b.MigrationsAssembly("MinimalApi")
));

builder.Services.AddDbContext<AppDbContext>(oprions => {
    oprions.UseNpgsql(connectionString);
});

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DeleteFornecedor",
        policy => policy.RequireClaim("DeleteFornecedor"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swo => {
    swo.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Minimal API AuthJWT",
        Description = "Desenvolvido por Leandro Rocha, com base na implementação Eduador Pires @desenvolvedor.io",
        Contact = new OpenApiContact { Name = "Leandro Rocha", Email = "leandrorocha.bezerra@gmail.com" },
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    swo.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
     {
         Description = "Insira o token JWT desta maneira: Bearer {seu token}",
         Name = "Authorization",
         Scheme = "Bearer",
         BearerFormat = "JWT",
         In = ParameterLocation.Header,
         Type = SecuritySchemeType.ApiKey
     });

    swo.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();

MapActions(app);

app.Run();

void MapActions(WebApplication app)
{
    app.MapGet("/fornecedor", [Authorize] async (AppDbContext context) =>
{
    var fornecedor = await context.Fornecedor.Where(f => f.Ativo == true).ToListAsync();
    return Results.Ok(fornecedor);
}).Produces<List<Fornecedor>>(200)
  .WithName("GetFornecedorAllActive")
  .WithTags("Fornecedor");

    app.MapGet("/fornecedor{id}", [Authorize] async (AppDbContext context, Guid? id) =>
    {
        if (id == null) return Results.NotFound();
        var fornecedor = await context.Fornecedor.FindAsync(id);
        if (fornecedor == null) return Results.NotFound();
        return Results.Ok(fornecedor);
    }).Produces<Fornecedor>(200, "application/json")
      .Produces(404)
      .WithName("GetFornecedorById")
      .WithTags("Fornecedor");

    app.MapPost("/fornecedor", [Authorize] async (AppDbContext context, Fornecedor f) =>
    {
        if (f.Validate())
        {
            var data = await context.Fornecedor.AddAsync(f);
            context.SaveChanges();
            return Results.Created($"/fornecedor/{data.Entity.Id}", data.Entity);
        }
        return Results.UnprocessableEntity();
    }).Produces(201)
      .Produces(422)
      .WithName("CreateFornecedor")
      .WithTags("Fornecedor");

    app.MapDelete("/fornecedor/{id}", [Authorize] async (AppDbContext context, Guid id) =>
    {
        var data = await context.Fornecedor.FindAsync(id);
        if (data == null) return Results.NotFound();
        context.Remove(data);
        context.SaveChanges();
        return Results.NoContent();
    }).Produces(204)
      .WithName("DeleteFornecedor")
      .WithTags("Fornecedor");

    app.MapPut("/fornecedor/{id}", [Authorize] async (AppDbContext context, Fornecedor f, Guid id) =>
    {
        var data = await context.Fornecedor.SingleOrDefaultAsync(p => p.Id == id);
        if (data == null)
        {
            return Results.BadRequest();
        }
        data.Name = f.Name;
        data.Documento = f.Documento;
        data.Ativo = f.Ativo;
        context.SaveChanges();
        return Results.Ok(data);

    }).Produces(200)
      .Produces(400)
      .WithName("UpdateFornecedor")
      .WithTags("Fornecedor");

    app.MapGet("/fornecedor/{term}", [Authorize] async (AppDbContext context, string? term) =>
    {
        if (String.IsNullOrWhiteSpace(term)) return Results.NotFound();
        var fornecedor = await context.Fornecedor.Where(p =>
            ((p.Name ?? "").ToLower().Contains(term.ToLower()) == true ||
            (p.Documento ?? "").ToLower().Contains(term) == true)
        ).ToArrayAsync();

        if (fornecedor == null) return Results.NotFound();
        return Results.Ok(fornecedor);
    }).Produces<List<Fornecedor>>(200)
      .Produces(404)
      .WithName("GetFornecetorByTerm")
      .WithTags("Fornecedor");

    /*********************************************************/

    app.MapPost("/registro", [AllowAnonymous] async (
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings,
        RegisterUser registerUser) =>
    {
        if (registerUser == null)
            return Results.BadRequest("Usuário não informado");

        if (!MiniValidator.TryValidate(registerUser, out var errors))
            return Results.ValidationProblem(errors);

        var user = new IdentityUser
        {
            UserName = registerUser.Email,
            Email = registerUser.Email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, registerUser.Password);

        if (!result.Succeeded)
            return Results.BadRequest(result.Errors);

        var jwt = new JwtBuilder()
            .WithUserManager(userManager)
            .WithJwtSettings(appJwtSettings.Value)
            .WithEmail(user.Email)
            .WithJwtClaims()
            .WithUserClaims()
            .WithUserRoles()
            .BuildUserResponse();
        return Results.Ok(jwt);
    }).ProducesValidationProblem()
      .Produces(StatusCodes.Status200OK)
      .Produces(StatusCodes.Status400BadRequest)
      .WithName("RegitroUsuario")
      .WithTags("Auth");

    app.MapPost("/login", [AllowAnonymous] async (
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings,
        LoginUser loginUser) =>
    {
        if (loginUser == null)
            return Results.BadRequest("Usuário não informado");

        if (!MiniValidator.TryValidate(loginUser, out var errors))
            return Results.ValidationProblem(errors);

        var result = await signInManager
            .PasswordSignInAsync(
                loginUser.Email,
                loginUser.Password,
                false,
                true);

        if (result.IsLockedOut)
            return Results.BadRequest("Usuário Bloaqueado");

        if (!result.Succeeded)
            return Results.BadRequest("Usuário ou senha inválidos");

        var jwt = new JwtBuilder()
            .WithUserManager(userManager)
            .WithJwtSettings(appJwtSettings.Value)
            .WithEmail(loginUser.Email)
            .WithJwtClaims()
            .WithUserClaims()
            .WithUserRoles()
            .BuildUserResponse();
        return Results.Ok(jwt);
    }).ProducesValidationProblem()
      .Produces(200)
      .Produces(400)
      .WithName("LoginUsuario")
      .WithTags("Auth");
}
