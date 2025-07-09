using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

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
Task.Run(() =>
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
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.Span.ToArray();
            var message = Encoding.UTF8.GetString(body);
            app.Logger.LogInformation($"Received message from RabbitMQ: {message}");
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
