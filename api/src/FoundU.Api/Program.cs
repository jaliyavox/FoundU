using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FoundUDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FoundUDatabase")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for the React dev origin. Origins tightened / moved to config in Step 3.
const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    await FoundU.Infrastructure.Persistence.Seed.DevelopmentDataSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<FoundUDbContext>(),
        scope.ServiceProvider.GetRequiredService<IConfiguration>());
}

app.UseHttpsRedirection();
app.UseCors(DevCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
public partial class Program { }
