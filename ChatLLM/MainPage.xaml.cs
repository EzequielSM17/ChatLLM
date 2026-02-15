using Models;
using ViewModels;

namespace Views
{
    public partial class MainPage : ContentPage
    {


        public MainPage(ChatViewModel vm)
        {
            InitializeComponent();

            BindingContext =  vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var vm = (ChatViewModel)BindingContext;
            await vm.InitializeOnceAsync();
            vm.Messages.CollectionChanged += (s, e) =>
            {
                if (vm.Messages.Count > 0)
                {
                    // Hace scroll automático al último elemento añadido
                    MessagesListView.ScrollTo(vm.Messages.Count - 1);
                }
            };
            
            
            
        }

    }
}
