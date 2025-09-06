using Microsoft.Maui.Controls;
using System;

namespace TrackLite
{
    [QueryProperty(nameof(CorridaSelecionada), "CorridaSelecionada")]
    public partial class DetalhePage : ContentPage
    {
        private Corrida? corridaSelecionada;
        public Corrida? CorridaSelecionada
        {
            get => corridaSelecionada;
            set
            {
                corridaSelecionada = value;
                BindingContext = corridaSelecionada; // seta o binding para o XAML
            }
        }

        public DetalhePage()
        {
            InitializeComponent();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(".."); // volta no Shell
        }

        private async void OnDownloadClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Download", "Aqui você faria o download do conteúdo.", "OK");
        }
    }
}
