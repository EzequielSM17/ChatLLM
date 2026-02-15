using ViewModels;

namespace Views{

    public partial class SettingsPage : ContentPage
    {
        public SettingsPage(ChatViewModel vm) // No olvides pasarle el VM por inyección
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}