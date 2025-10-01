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

        // Abre o reposit�rio do GitHub
        private async void OnGitHubTapped(object sender, EventArgs e)
        {
            var url = "https://github.com/mariaeduardaeva/TrackLite/tree/master";
            await Launcher.Default.OpenAsync(url);
        }

        // Abre email para contatar suporte
        private async void OnFeedbackClicked(object sender, EventArgs e)
        {
            try
            {
                // Cria a URL mailto com assunto e destinat�rio
                var mailtoUrl = "mailto:suportetracklite@gmail.com?subject=Feedback - TrackLite";

                // Abre a lista de apps dispon�veis no Android ou iOS
                await Launcher.Default.OpenAsync(new Uri(mailtoUrl));
            }
            catch (Exception ex)
            {
                // Caso algo d� errado
                await DisplayAlert("Erro", $"N�o foi poss�vel abrir o email: {ex.Message}", "OK");
            }
        }

        // Navega para a p�gina legal
        private async void OnTermsTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new LegalPage());
        }
    }
}