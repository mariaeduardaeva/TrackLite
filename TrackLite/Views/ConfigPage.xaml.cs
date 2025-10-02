using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;

namespace TrackLite
{
    public partial class ConfigPage : ContentPage
    {
        public ConfigPage()
        {
            InitializeComponent();
        }

        private async void OnGitHubTapped(object sender, EventArgs e)
        {
            var url = "https://github.com/mariaeduardaeva/TrackLite/tree/master";
            await Launcher.Default.OpenAsync(url);
        }

        private async void OnFeedbackClicked(object sender, EventArgs e)
        {
            try
            {
                var mailtoUrl = "mailto:suportetracklite@gmail.com?subject=Feedback - TrackLite";

                await Launcher.Default.OpenAsync(new Uri(mailtoUrl));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Não foi possível abrir o email: {ex.Message}", "OK");
            }
        }

        private async void OnTermsTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new LegalPage());
        }
    }
}