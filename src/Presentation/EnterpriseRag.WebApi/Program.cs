using EnterpriseRag.IoC;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


builder.Services.AddApplicationDependencies();
builder.Services.AddInfrastructurePersistenceDependencies(builder.Configuration);
builder.Services.AddInfrastructureIdentityDependencies(builder.Configuration);
builder.Services.AddVectorStoreDependencies(builder.Configuration);
builder.Services.AddExternalServicesDependencies(builder.Configuration);


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


await app.Services.SeedIdentityDatabaseAsync();

app.Run();


record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
