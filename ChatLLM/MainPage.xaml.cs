using Models;
using ViewModels;

namespace Views
{
    public partial class MainPage : ContentPage
    {

        private readonly ChatViewModel _viewModel;

        public MainPage(ChatViewModel vm)
        {
            InitializeComponent();

            // Esta es la línea clave: vincula la UI con la lógica
            BindingContext = _viewModel = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var vm = (ChatViewModel)BindingContext;

            // 1. Inicia RabbitMQ
            await vm._chatService.InitializeAsync();

            // 2. Avisa al LLM (Warmup)
            await vm.WarmupLlmAsync();

            // 3. Escribe en el chat que ya está listo
            vm.Messages.Add(new Message
            {
                Text = "SISTEMA: El modelo está cargado y RabbitMQ conectado.",
                IsBot = true
            });
        }

    }
}
