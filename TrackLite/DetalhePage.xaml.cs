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
            await Navigation.PopAsync(); // Volta pra p�gina anterior
        }

        private async void OnDownloadClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Download", "Aqui voc� faria o download do conte�do.", "OK");
        }
    }
}
