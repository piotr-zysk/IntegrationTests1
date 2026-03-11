using SampleApp;
using SampleApp.Data;

var builder = WebApplication.CreateBuilder(args);
AppComposition.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();
AppComposition.ConfigureEndpoints(app);

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

await app.RunAsync();

public partial class Program;
