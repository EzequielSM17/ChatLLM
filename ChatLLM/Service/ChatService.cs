using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Services;

public class ChatService : IDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;
    private string? _queueName;

    // Este evento notificará al ViewModel
    public event Action<string>? MessageReceived;

    private readonly ConnectionFactory _factory = new()
    {
        HostName = "localhost",
        Port = 5673,
        UserName = "guest",
        Password = "guest",
    };

    public async Task InitializeAsync()
    {
        try
        {
            _connection = await _factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(exchange: "groupChat", type: ExchangeType.Fanout, durable: false);

            var queueDeclareResult = await _channel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true);
            _queueName = queueDeclareResult.QueueName;

            await _channel.QueueBindAsync(queue: _queueName, exchange: "groupChat", routingKey: string.Empty);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            // Suscribimos el OnReceived aquí mismo
            consumer.ReceivedAsync += OnMessageReceivedInternal;

            await _channel.BasicConsumeAsync(queue: _queueName, autoAck: true, consumer: consumer);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error RabbitMQ: {ex.Message}");
        }
    }

    // El método OnReceive interno del servicio
    private async Task OnMessageReceivedInternal(object sender, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        // Notificamos a cualquier suscriptor (nuestro ViewModel)
        MessageReceived?.Invoke(message);

        await Task.CompletedTask;
    }

    public async Task SendMessageAsync(string message)
    {
        if (_channel is null) return;
        var body = Encoding.UTF8.GetBytes(message);
        await _channel.BasicPublishAsync(exchange: "groupChat", routingKey: string.Empty, body: body);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}