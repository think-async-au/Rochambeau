using Rochambeau.Api.Common.Http;
using Rochambeau.Api.Common.Persistence;
using Rochambeau.Api.Modules.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSignalR();
builder.Services.AddPersistence(builder.Configuration);

builder.Services.AddIdentityModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseClientHeaderCheck();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    await app.ApplyMigrationsAsync().ConfigureAwait(false);
}

app.MapHealthChecks("/health").AllowAnonymous();

app.MapIdentityModule();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Entry point; public so integration tests can use WebApplicationFactory.</summary>
public partial class Program;
