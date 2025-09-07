using Microsoft.Maui.Controls;

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
    }
}
