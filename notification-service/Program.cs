using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NotificationService;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// DB connection check
try
{
    var connString = builder.Configuration.GetConnectionString("DefaultConnection");
    using var conn = new NpgsqlConnection(connString);
    conn.Open();
    app.Logger.LogInformation("Successfully connected to PostgreSQL database (notification-service)");
    conn.Close();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to connect to PostgreSQL database (notification-service)");
}

// Start RabbitMQ consumer in background
Task.Run(async () =>
{
    try
    {
        var rabbitConfig = app.Configuration.GetSection("RabbitMQ");
        var factory = new ConnectionFactory()
        {
            HostName = rabbitConfig["Host"] ?? "localhost",
            Port = int.Parse(rabbitConfig["Port"] ?? "5672"),
            UserName = rabbitConfig["Username"] ?? "guest",
            Password = rabbitConfig["Password"] ?? "guest"
        };
        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();
        var queue = rabbitConfig["Queue"] ?? "order_notifications";
        channel.QueueDeclare(queue: queue, durable: false, exclusive: false, autoDelete: false, arguments: null);
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.Span.ToArray();
            var message = Encoding.UTF8.GetString(body);
            app.Logger.LogInformation($"Received message from RabbitMQ: {message}");
            
            // Save notification to DB
            try
            {
                using var scope = app.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                var orderEvent = JsonSerializer.Deserialize<OrderEvent>(message);
                if (orderEvent != null)
                {
                    var notification = new Notification
                    {
                        OrderId = orderEvent.Id,
                        Description = orderEvent.Description,
                        ReceivedAt = DateTime.UtcNow
                    };
                    dbContext.Notifications.Add(notification);
                    await dbContext.SaveChangesAsync();
                    app.Logger.LogInformation($"Notification saved to DB for Order {orderEvent.Id}");
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Failed to save notification to DB");
            }
        };
        channel.BasicConsume(queue: queue, autoAck: true, consumer: consumer);
        app.Logger.LogInformation("RabbitMQ consumer started successfully");
        
        // Keep the connection alive
        while (true)
        {
            Thread.Sleep(1000);
            if (!connection.IsOpen)
            {
                app.Logger.LogError("RabbitMQ connection lost");
                break;
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to start RabbitMQ consumer");
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "Notification Service - RabbitMQ Consumer");

app.Run();

// DTO for deserializing order events
public class OrderEvent
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
