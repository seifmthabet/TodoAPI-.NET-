using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TodoAPI.Data;
using TodoAPI.Models;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using TodoAPI.DTOs;
using TodoAPI.Extensions;
using TodoAPI.Mapping;
using TodoAPI.Helpers;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();
builder.Services.AddDbContext<TodoContext>(options => options.UseSqlite("Data Source=todos.db"));
builder.Services.AddAutoMapper(typeof(MappingProfile));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Todo API",
        Version = "v1"
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000") // Add your frontend URLs
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Auth-Config
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    
    // User settings
    options.User.RequireUniqueEmail = false;
}).AddEntityFrameworkStores<TodoContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
        )
    };
});
builder.Services.AddAuthorization();

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Global rate limiter for all requests
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Specific policy for auth endpoints
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));
});

var app = builder.Build();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API V1");
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseCors("AllowFrontend");

app.MapGet("/api/todos", async (
    string? search,
    bool? isCompleted,
    string? sortBy,
    TodoContext db,
    IMapper mapper,
    ClaimsPrincipal user
) =>
{
    var userId = user.GetUserId();
    var query = db.Todos.Where(t => t.UserId == userId);
    
    if (!string.IsNullOrEmpty(search))
        query = query.Where(t => t.Title.Contains(search));

    if (isCompleted.HasValue)
        query = query.Where(t => t.IsCompleted == isCompleted);

    query = sortBy switch
    {
        "title" => query.OrderBy(t => t.Title),
        "created" => query.OrderByDescending(t => t.CreatedAt),
        _ => query.OrderBy(t => t.Id)
    };

    var results = await query.ToListAsync();
    return Results.Ok(mapper.Map<IEnumerable<TodoReadDTO>>(results));
}).RequireAuthorization();

app.MapGet("/api/todos/{id}", async (int id, TodoContext db, IMapper mapper, ClaimsPrincipal user) =>
{
    var userId = user.GetUserId();
    var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    return todo is null ? Results.NotFound() : Results.Ok(mapper.Map<TodoReadDTO>(todo));
}).RequireAuthorization();

app.MapPost("/api/todos", async (
    TodoDTO dto,
    TodoContext db,
    IMapper mapper,
    ClaimsPrincipal user
) =>
{
    try
    {
        var todo = mapper.Map<Todo>(dto);
        todo.UserId = user.GetUserId();
        todo.CreatedAt = DateTime.UtcNow;

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        var result = mapper.Map<TodoReadDTO>(todo);
        return Results.Created($"/api/todos/{todo.Id}", result);
    }
    catch (Exception)
    {
        return Results.Problem("An error occurred while creating the todo");
    }
}).RequireAuthorization();

app.MapPut("/api/todos/{id}", async (int id, TodoDTO dto, TodoContext db, IMapper mapper, ClaimsPrincipal user) =>
{
    var userId = user.GetUserId();
    var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    if (todo is null) return Results.NotFound();

    mapper.Map(dto, todo);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapDelete("/api/todos/{id}", async (int id, TodoContext db, ClaimsPrincipal user) =>
{
    var userId = user.GetUserId();
    var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    if (todo is null) return Results.NotFound();

    db.Todos.Remove(todo);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/api/register", async (UserManager<AppUser> userManager, RegisterLoginDTO dto) =>
{
    if (string.IsNullOrEmpty(dto.Username) || string.IsNullOrEmpty(dto.Password))
        return Results.BadRequest("Username and password are required");

    if (dto.Password.Length < 6)
        return Results.BadRequest("Password must be at least 6 characters long");

    var existingUser = await userManager.FindByNameAsync(dto.Username);
    if (existingUser != null)
        return Results.BadRequest("Username already exists");

    var user = new AppUser { UserName = dto.Username };
    var result = await userManager.CreateAsync(user, dto.Password);
    
    if (!result.Succeeded)
    {
        var errors = result.Errors.Select(e => e.Description);
        return Results.BadRequest(new { message = "Registration failed", errors });
    }
    
    return Results.Ok(new { message = "User registered successfully" });
}).RequireRateLimiting("auth");

app.MapPost("/api/login", async (UserManager<AppUser> userManager, IConfiguration config, RegisterLoginDTO dto) =>
{
    var user = await userManager.FindByNameAsync(dto.Username);
    if (user == null || !await userManager.CheckPasswordAsync(user, dto.Password))
        return Results.Unauthorized();

    var token = TokenService.CreateToken(user, config);
    return Results.Ok(new { token });
}).RequireRateLimiting("auth");



app.Run();