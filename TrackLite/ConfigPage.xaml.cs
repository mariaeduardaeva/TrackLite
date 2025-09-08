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

        private async void OnGitHubTapped(object sender, EventArgs e)
        {
            var url = "https://github.com/mariaeduardaeva/TrackLite/tree/master";
            await Launcher.Default.OpenAsync(url);
        }

        private async void OnFeedbackClicked(object sender, EventArgs e)
        {
            var email = new EmailMessage
            {
                Subject = "Feedback - TrackLite",
                Body = "",
                To = new List<string> { "suporte@tracklite.com" }
            };
            await Email.Default.ComposeAsync(email);
        }
        private async void OnTermsTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new LegalPage());
        }
    }
}
