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
    };

    // En ChatService.cs
    public async Task InitializeAsync()
    {
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(exchange: "groupChat", type: ExchangeType.Fanout, durable: false);

        var queueDeclareResult = await _channel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true);
        _queueName = queueDeclareResult.QueueName;

        await _channel.QueueBindAsync(queue: _queueName, exchange: "groupChat", routingKey: string.Empty);

        // --- CAMBIO CLAVE: QoS (Quality of Service) ---
        // PrefetchSize: 0 (sin límite de tamaño)
        // PrefetchCount: 1 (SOLO ENVÍAME 1 MENSAJE)
        // Global: false (aplicado a este canal)
        await _channel.BasicQosAsync(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            // Filtro de ID (tu lógica actual)
            if (!string.IsNullOrEmpty(CurrentAppId) && message.StartsWith($"[{CurrentAppId}]"))
            {
                // Si es mío, le digo a RabbitMQ que ya lo procesé (para que me mande el siguiente)
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }

            // Si es de otro, pasamos la información del mensaje (ea) para poder confirmar después
            if (MessageReceivedAsync != null)
            {
                // Necesitamos un evento que devuelva una Task para esperar al LLM
                await MessageReceivedAsync.Invoke(message, ea.DeliveryTag);
            }
        };

        // --- CAMBIO CLAVE: autoAck: false ---
        await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer);
    }

    // Cambiamos el evento para que sea asíncrono
    

    // Método para confirmar que el mensaje ha sido procesado
    public async Task ConfirmMessageAsync(ulong deliveryTag)
    {
        if (_channel != null)
        {
            await _channel.BasicAckAsync(deliveryTag, false);
        }
    }

    // El método OnReceive interno del servicio
    private async Task OnMessageReceivedInternal(object sender, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        if (!string.IsNullOrEmpty(CurrentAppId) && message.StartsWith($"[{CurrentAppId}]:"))
        {
            await Task.CompletedTask;
            return;
        }
        MessageReceived?.Invoke(message);

        await Task.CompletedTask;
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