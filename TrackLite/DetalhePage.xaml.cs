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
            // Define a corrida selecionada
            get => corridaSelecionada;
            set
            {
                corridaSelecionada = value;
                BindingContext = corridaSelecionada;
            }
        }

        public DetalhePage()
        {
            InitializeComponent();
        }

        // Volta para a p�gina anterior
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // Simula o download do conte�do
        private async void OnDownloadClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Download", "Aqui voc� faria o download do conte�do.", "OK");
        }
    }
}
