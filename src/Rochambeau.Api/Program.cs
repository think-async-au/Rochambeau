using Rochambeau.Api.Common.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSignalR();
builder.Services.AddPersistence(builder.Configuration);

// Modules: builder.Services.Add<Name>Module(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.ApplyMigrationsAsync().ConfigureAwait(false);
}

app.MapHealthChecks("/health");

// Modules: app.Map<Name>Module();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Entry point; public so integration tests can use WebApplicationFactory.</summary>
public partial class Program;
