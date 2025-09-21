using Microsoft.EntityFrameworkCore;
using Hubs;
using Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(origin => true); // or specify your frontend origin
    });
});

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlite("Data Source=game.db")); 

var app = builder.Build();

// Middleware order matters
app.UseRouting();
app.UseCors(); // must come after routing, before endpoints

// SignalR endpoint
app.UseRouting();
app.UseCors();

app.UseEndpoints(endpoints =>
{
    _ = endpoints.MapHub<GameHub>("/gamehub");
});

// Swagger setup
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets(); // Add this if missing
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
