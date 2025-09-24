using Microsoft.Maui.Controls;
using System;
using System.Globalization;

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
                OnPropertyChanged(nameof(TempoFormatado));
                OnPropertyChanged(nameof(DistanciaFormatada));
                OnPropertyChanged(nameof(RitmoFormatado));
                OnPropertyChanged(nameof(DataFormatada));
                OnPropertyChanged(nameof(PassosEstimados));
            }
        }

        // Propriedades formatadas para bind no XAML
        public string TempoFormatado => CorridaSelecionada?.TempoDecorrido ?? "--:--";
        public string DistanciaFormatada => CorridaSelecionada?.Distancia ?? "-";
        public string RitmoFormatado => CorridaSelecionada?.Ritmo ?? "-";

        // Mostra apenas a data da corrida
        public string DataFormatada =>
            CorridaSelecionada?.Data.ToString("dd MMMM 'de' yyyy", new CultureInfo("pt-BR")) ?? "-";

        // Propriedade para mostrar os passos estimados
        public string PassosEstimados
        {
            get
            {
                if (CorridaSelecionada == null || string.IsNullOrWhiteSpace(CorridaSelecionada.Distancia))
                    return "-";

                // Extrai número da distância
                if (double.TryParse(
                        CorridaSelecionada.Distancia.Replace("km", "").Trim().Replace(",", "."),
                        out double km))
                {
                    double passos = km * 1400;
                    return $"{passos:F0} passos";
                }

                return "-";
            }
        }

        public DetalhePage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        // Volta para a página anterior
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // Simula o download do conteúdo
        private async void OnDownloadClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Download", "Aqui você faria o download do conteúdo.", "OK");
        }
    }
}