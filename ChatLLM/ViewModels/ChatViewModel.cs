using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Models;
using Services;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Input;

namespace ViewModels;

public partial class ChatViewModel : ObservableObject
{
    public readonly ChatService _chatService;
    public readonly HttpClient _http;

    [ObservableProperty] public partial ObservableCollection<Message> Messages { get; set; } = new();
    [ObservableProperty] public partial float Temperature { get; set; } = 0.7f;
    [ObservableProperty] public partial bool IsLoading { get; set; } = false;
    [ObservableProperty] public partial string StatusMessage { get; set; } = "Esperando inicio...";
    [ObservableProperty] public partial string systemPrompt { get; set; } = "Eres un asistente servicial.";
    [ObservableProperty] public partial int topK { get; set; } = 40;
    [ObservableProperty] public partial string modelName { get; set; } = "meta-llama-3.1-8b-instruct";

    [ObservableProperty]
    public partial ObservableCollection<string> AvailableModels { get; set; } = new() { "meta-llama-3.1-8b-instruct", "gemma-3-270m-it" };

    public ICommand ManualStartCommand { get; }

    public ChatViewModel(ChatService chatService)
    {
        _chatService = chatService;
        _http = new HttpClient { BaseAddress = new Uri("http://localhost:1234") };

        // El ViewModel se suscribe al evento del Servicio
        _chatService.MessageReceived += OnMessageReceived;

        // Definimos el comando correctamente
        ManualStartCommand = new Command(async () => await StartChatAsync());
    }

    private async Task StartChatAsync()
    {
        if (IsLoading) return;

        var initialMessage = "¡Hola! ¿De qué quieres hablar hoy?";
        // No lo añadimos aquí a Messages, porque al enviarlo a RabbitMQ 
        // lo recibiremos de vuelta en OnMessageReceived y se añadirá ahí.
        await _chatService.SendMessageAsync(initialMessage);
    }

    private async void OnMessageReceived(string text)
    {
        // 1. Mostrar en UI
       
        MainThread.BeginInvokeOnMainThread(() =>
            Messages.Add(new Message { Text = text, IsBot = true }));
        if (text.StartsWith("[BOT_1]"))
        {
            return;
        }
        // 2. Esperar para que no sea infinito instantáneo
        await Task.Delay(2000);

        // 3. Obtener respuesta del LLM
        var response = await GetLlmResponse(text);

        // 4. Enviar respuesta de vuelta a RabbitMQ
        await _chatService.SendMessageAsync($"[BOT_1]: {response}");
    }

    private async Task<string> GetLlmResponse(string prompt)
    {
        try
        {
            var requestBody = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = prompt } // <--- USAR EL PROMPT REAL
                },
                temperature = Temperature,
                top_k = topK
            };

            var response = await _http.PostAsJsonAsync("/v1/chat/completions", requestBody);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>();
                return content.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "...";
            }
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
        return "Error en LLM";
    }

    public async Task WarmupLlmAsync()
    {
        IsLoading = true;
        StatusMessage = "Cargando modelo...";
        try
        {
            var response = await _http.PostAsJsonAsync("/v1/chat/completions", new
            {
                model = modelName,
                messages = new[] { new { role = "user", content = "ping" } },
                max_tokens = 1
            });
            StatusMessage = response.IsSuccessStatusCode ? "Modelo listo" : "Error en LM Studio";
        }
        finally { IsLoading = false; }
    }
}