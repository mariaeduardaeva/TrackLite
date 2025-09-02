using Microsoft.Maui.Controls;

namespace TrackLite
{
    public partial class DetalhePage : ContentPage
    {
        public DetalhePage()
        {
            InitializeComponent();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync(); // Volta pra página anterior
        }

        private async void OnDownloadClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Download", "Aqui você faria o download do conteúdo.", "OK");
        }
    }
}
