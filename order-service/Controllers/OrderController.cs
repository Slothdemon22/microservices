using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Text;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly OrderDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<OrderController> _logger;

        public OrderController(OrderDbContext db, IConfiguration config, ILogger<OrderController> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Order order)
        {
            order.CreatedAt = DateTime.UtcNow;
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // Publish event to RabbitMQ
            var rabbitConfig = _config.GetSection("RabbitMQ");
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
            var message = System.Text.Json.JsonSerializer.Serialize(new { order.Id, order.Description, order.CreatedAt });
            var body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: "", routingKey: queue, basicProperties: null, body: body);
            _logger.LogInformation($"Order created and event published: {message}");

            return Ok(order);
        }
    }
} 