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
            NavigationPage.SetHasNavigationBar(this, false);
            CarregarConfiguracoes();
        }

        private void CarregarConfiguracoes()
        {
            try
            {
                // Força a leitura das preferências
                var frequencia = AppSettings.FrequenciaColeta;
                var accuracy = AppSettings.LimiarAccuracy;
                var vibracao = AppSettings.VibracaoKm;

                // Atualiza a UI na thread principal
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FrequenciaColetaSlider.Value = frequencia;
                    AccuracySlider.Value = accuracy;
                    VibracaoSwitch.IsToggled = vibracao;

                    AtualizarLabelFrequencia(frequencia);
                    AtualizarLabelAccuracy(accuracy);
                    AtualizarLabelVibracao(vibracao);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar configurações: {ex.Message}");
                // Fallback para valores padrão em caso de erro
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CarregarConfiguracoesPadrao();
                });
            }
        }

        private void CarregarConfiguracoesPadrao()
        {
            FrequenciaColetaSlider.Value = 1;
            AccuracySlider.Value = 50;
            VibracaoSwitch.IsToggled = true;

            AtualizarLabelFrequencia(1);
            AtualizarLabelAccuracy(50);
            AtualizarLabelVibracao(true);
        }

        private void FrequenciaColetaSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            int valor = (int)Math.Round(e.NewValue);
            AtualizarLabelFrequencia(valor);

            FrequenciaColetaSlider.Value = valor;

            AppSettings.FrequenciaColeta = valor;
        }

        private void AtualizarLabelFrequencia(int valor)
        {
            FrequenciaColetaLabel.Text = $"{valor}s";
        }

        private void AccuracySlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            int valor = (int)Math.Round(e.NewValue);
            AtualizarLabelAccuracy(valor);

            AccuracySlider.Value = valor;
            AppSettings.LimiarAccuracy = valor;
        }

        private void AtualizarLabelAccuracy(int valor)
        {
            string textoAccuracy;

            if (valor <= 33)
                textoAccuracy = "Baixa";
            else if (valor <= 66)
                textoAccuracy = "Media";
            else
                textoAccuracy = "Alta";

            AccuracyLabel.Text = textoAccuracy;
        }

        private void VibracaoSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            AtualizarLabelVibracao(e.Value);
            AppSettings.VibracaoKm = e.Value;
        }

        private void AtualizarLabelVibracao(bool ativada)
        {
            VibracaoLabel.Text = ativada ? "Ativada" : "Desativada";
        }

        private async void OnGitHubTapped(object sender, EventArgs e)
        {
            try
            {
                var url = "https://github.com/mariaeduardaeva/TrackLite/tree/master";
                await Launcher.Default.OpenAsync(url);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Não foi possível abrir o GitHub: {ex.Message}", "OK");
            }
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
            try
            {
                await Navigation.PushAsync(new LegalPage());
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Não foi possível abrir os termos: {ex.Message}", "OK");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Console.WriteLine("ConfigPage aparecendo - carregando configurações");
            CarregarConfiguracoes();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Console.WriteLine("ConfigPage desaparecendo - verificando configurações:");
            Console.WriteLine($"Frequencia: {AppSettings.FrequenciaColeta}");
            Console.WriteLine($"Accuracy: {AppSettings.LimiarAccuracy}");
            Console.WriteLine($"Vibracao: {AppSettings.VibracaoKm}");
        }
    }
}