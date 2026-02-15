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
    [ObservableProperty] public partial float repeatPenalty { get; set; } = 1.1f;
    [ObservableProperty] public partial float topP { get; set; } = 0.95f;
    [ObservableProperty] public partial float minP { get; set; } = 0.05f;
    [ObservableProperty] public partial int maxTokens { get; set; } = 512;
    [ObservableProperty] public partial string modelName { get; set; } = "meta-llama-3.1-8b-instruct";

    private bool _isInitialized = false;
    [ObservableProperty]
    public partial ObservableCollection<string> AvailableModels { get; set; } = new();

    public ICommand ManualStartCommand { get; }

    public ChatViewModel(ChatService chatService)
    {
        _chatService = chatService;
        _http = new HttpClient { BaseAddress = new Uri("http://localhost:1234"), Timeout = TimeSpan.FromMinutes(3) };

        
        ManualStartCommand = new Command(async () => await StartChatAsync());

        _chatService.MessageReceivedAsync += OnMessageReceivedAsync;
        _ = GetModelsAsync();
    }
    

    public async Task InitializeOnceAsync()
    {
        if (_isInitialized) return;

        // Ejecutamos la carga inicial
        await _chatService.InitializeAsync();
        await WarmupLlmAsync();
        MainThread.BeginInvokeOnMainThread(() =>
                Messages.Add(new Message { Text = "SISTEMA: El modelo está cargado y RabbitMQ conectado.", IsBot = true }));
        
        _isInitialized = true;
    }
    private async Task GetModelsAsync()
    {
        try
        {
            var response = await _http.GetAsync("/v1/models");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>();

                // Extraemos los IDs de los modelos cargados
                var modelList = content.GetProperty("data")
                                       .EnumerateArray()
                                       .Select(m => m.GetProperty("id").GetString() ?? "desconocido")
                                       .ToList();

                
                MainThread.BeginInvokeOnMainThread(() => {
                    AvailableModels = new ObservableCollection<string>(modelList);

                  
                    if (AvailableModels.Any())
                    {
                        modelName = AvailableModels.First();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando modelos: {ex.Message}");
        }
    }
    private async Task StartChatAsync()
    {
        if (IsLoading) return;

        var initialMessage = "¡Hola! ¿De qué quieres hablar hoy?";
        await _chatService.SendMessageAsync(initialMessage);
    }
    private async Task OnMessageReceivedAsync(string text, ulong deliveryTag)
    {
        try
        {
            // 1. Limpiar el ID y mostrar en UI
            var cleanText = text.Contains("]:") ? text.Split("]:")[1].Trim() : text;
            MainThread.BeginInvokeOnMainThread(() =>
                Messages.Add(new Message { Text = cleanText, IsBot = true }));

            // 2. Esperar al LLM (mientras esto ocurre, RabbitMQ no enviará más mensajes a esta app)
            var response = await GetLlmResponse(cleanText);

            // 3. Enviar la respuesta a RabbitMQ
            await _chatService.SendMessageAsync(response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error procesando: {ex.Message}");
        }
        finally
        {

            await _chatService.ConfirmMessageAsync(deliveryTag);
        }
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
                    new { role = "user", content = prompt } 
                },
                temperature = Temperature,
                top_k = topK,
                top_p = topP,
                min_p = minP,
                repeat_penalty = repeatPenalty,
                max_tokens = maxTokens
            };

            var response = await _http.PostAsJsonAsync("/v1/chat/completions", requestBody);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>();
                var newMessage= content.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "...";
                MainThread.BeginInvokeOnMainThread(() =>
                Messages.Add(new Message { Text = newMessage, IsBot = false }));
                return newMessage;
                
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