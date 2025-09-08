using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Collections.Generic;

namespace TrackLite
{
    public partial class ConfigPage : ContentPage
    {
        public ConfigPage()
        {
            InitializeComponent();
        }

        // Abre o repositorio do github
        private async void OnGitHubTapped(object sender, EventArgs e)
        {
            var url = "https://github.com/mariaeduardaeva/TrackLite/tree/master";
            await Launcher.Default.OpenAsync(url);
        }

        //  Abre email para contatar suporte
        private async void OnFeedbackClicked(object sender, EventArgs e)
        {
            var email = new EmailMessage
            {
                Subject = "Feedback - TrackLite",
                Body = "",
                To = new List<string> { "suportetracklite@gmail.com" }
            };
            await Email.Default.ComposeAsync(email);
        }

        // Navega para a página legal
        private async void OnTermsTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new LegalPage());
        }
    }
}
