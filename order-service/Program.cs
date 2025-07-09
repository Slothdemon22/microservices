using Npgsql;
using RabbitMQ.Client;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OrderService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// DB connection check
try
{
    var connString = builder.Configuration.GetConnectionString("DefaultConnection");
    using var conn = new NpgsqlConnection(connString);
    conn.Open();
    app.Logger.LogInformation("Successfully connected to PostgreSQL database (order-service)");
    conn.Close();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to connect to PostgreSQL database (order-service)");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet("/", () => "Order Service - RabbitMQ Producer");

app.MapPost("/send-test", (IConfiguration config, ILogger<Program> logger) =>
{
    try
    {
        var rabbitConfig = config.GetSection("RabbitMQ");
        var factory = new ConnectionFactory()
        {
            HostName = rabbitConfig["Host"] ?? "localhost",
            Port = int.Parse(rabbitConfig["Port"] ?? "5672"),
            UserName = rabbitConfig["Username"] ?? "guest",
            Password = rabbitConfig["Password"] ?? "guest"
        };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        var queue = rabbitConfig["Queue"] ?? "order_notifications";
        channel.QueueDeclare(queue: queue, durable: false, exclusive: false, autoDelete: false, arguments: null);
        string message = $"Test message from order-service at {DateTime.Now}";
        var body = Encoding.UTF8.GetBytes(message);
        channel.BasicPublish(exchange: "", routingKey: queue, basicProperties: null, body: body);
        logger.LogInformation($"Published message to RabbitMQ: {message}");
        return Results.Ok(new { status = "sent", message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to publish message to RabbitMQ");
        return Results.BadRequest(new { status = "error", message = ex.Message });
    }
});

app.Run();
