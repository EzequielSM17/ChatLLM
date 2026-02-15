using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Services;

public class ChatService : IDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;
    private string? _queueName;
    public string? CurrentAppId { get; set; }

    // Este evento notificará al ViewModel
    public event Action<string>? MessageReceived;
    public event Func<string, ulong, Task>? MessageReceivedAsync;

    private readonly ConnectionFactory _factory = new()
    {
        HostName = "192.168.0.108",
        Port = 5673,
        UserName = "guest",
        Password = "guest",
        // Aumentamos el intervalo de latido a 60 segundos
        RequestedHeartbeat = TimeSpan.FromSeconds(180),
        // Aumentamos el tiempo de espera de la conexión
        RequestedConnectionTimeout = TimeSpan.FromSeconds(180),
        // Habilitamos la recuperación automática si se cae
        AutomaticRecoveryEnabled = true
    };

    // En ChatService.cs
    public async Task InitializeAsync()
    {
        CurrentAppId = "Bot_" + Guid.NewGuid().ToString().Substring(0, 4);
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(exchange: "groupChat", type: ExchangeType.Fanout, durable: false);

        var queueDeclareResult = await _channel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: false, autoDelete: true);
        _queueName = queueDeclareResult.QueueName;

        await _channel.QueueBindAsync(queue: _queueName, exchange: "groupChat", routingKey: string.Empty);


        await _channel.BasicQosAsync(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);


            if (!string.IsNullOrEmpty(CurrentAppId) && message.StartsWith($"[{CurrentAppId}]"))
            {

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }

            if (MessageReceivedAsync != null)
            {
                await MessageReceivedAsync.Invoke(message, ea.DeliveryTag);
            }
        };

        // --- CAMBIO CLAVE: autoAck: false ---
        await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer);
    }


    public async Task ConfirmMessageAsync(ulong deliveryTag)
    {
        if (_channel != null)
        {
            await _channel.BasicAckAsync(deliveryTag, false);
        }
    }


    public async Task SendMessageAsync(string message)
    {
        if (_channel is null || string.IsNullOrEmpty(CurrentAppId)) return;

        // Al enviar, siempre le ponemos nuestra "firma"
        var messageWithId = $"[{CurrentAppId}]: {message}";
        var body = Encoding.UTF8.GetBytes(messageWithId);

        await _channel.BasicPublishAsync(
            exchange: "groupChat",
            routingKey: string.Empty,
            body: body);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}