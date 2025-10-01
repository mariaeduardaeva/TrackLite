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

        // Abre o repositório do GitHub
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
                // Cria a URL mailto com assunto e destinatário
                var mailtoUrl = "mailto:suportetracklite@gmail.com?subject=Feedback - TrackLite";

                // Abre a lista de apps disponíveis no Android ou iOS
                await Launcher.Default.OpenAsync(new Uri(mailtoUrl));
            }
            catch (Exception ex)
            {
                // Caso algo dê errado
                await DisplayAlert("Erro", $"Não foi possível abrir o email: {ex.Message}", "OK");
            }
        }

        // Navega para a página legal
        private async void OnTermsTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new LegalPage());
        }
    }
}