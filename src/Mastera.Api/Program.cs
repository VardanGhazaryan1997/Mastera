var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

/// <summary>Exposed so functional tests can host the API with WebApplicationFactory.</summary>
public partial class Program
{
}
