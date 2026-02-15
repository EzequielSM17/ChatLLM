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


            await vm._chatService.InitializeAsync();


            await vm.WarmupLlmAsync();

            vm.Messages.Add(new Message
            {
                Text = "SISTEMA: El modelo está cargado y RabbitMQ conectado.",
                IsBot = true
            });
        }

    }
}
