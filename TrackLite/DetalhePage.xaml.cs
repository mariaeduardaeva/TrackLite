using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.IO;
using Microsoft.Maui.Storage;
using SkiaSharp;

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

        public string TempoFormatado => CorridaSelecionada?.TempoDecorrido ?? "--:--";
        public string DistanciaFormatada => CorridaSelecionada?.Distancia ?? "-";
        public string RitmoFormatado => CorridaSelecionada?.Ritmo ?? "-";

        public string DataFormatada =>
            CorridaSelecionada?.Data.ToString("dd MMMM 'de' yyyy", new CultureInfo("pt-BR")) ?? "-";

        public string PassosEstimados
        {
            get
            {
                if (CorridaSelecionada == null || string.IsNullOrWhiteSpace(CorridaSelecionada.Distancia))
                    return "-";

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

        private async void OnExportClicked(object sender, EventArgs e)
        {
            if (CorridaSelecionada == null)
            {
                await DisplayAlert("Erro", "Nenhuma corrida selecionada para exportar.", "OK");
                return;
            }

            try
            {
                var fileName = $"Corrida_{CorridaSelecionada.Data:yyyyMMdd_HHmm}.pdf";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                GerarPdfSkia(filePath, CorridaSelecionada);

                await DisplayAlert("PDF gerado", $"Arquivo salvo em:\n{filePath}", "OK");

                await Launcher.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Falha ao gerar PDF: {ex.Message}", "OK");
            }
        }

        private void GerarPdfSkia(string filePath, Corrida corrida)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var document = SKDocument.CreatePdf(stream))
            {
                const float pageWidth = 595; // A4 width
                const float pageHeight = 842; // A4 height

                // BeginPage retorna um SKCanvas para desenhar
                using (var canvas = document.BeginPage(pageWidth, pageHeight))
                {
                    // Fundo branco
                    canvas.Clear(SKColors.White);

                    var paintTitle = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = 24,
                        IsAntialias = true,
                        Typeface = SKTypeface.Default
                    };

                    var paintBody = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = 16,
                        IsAntialias = true,
                        Typeface = SKTypeface.Default
                    };

                    // Título
                    canvas.DrawText("Detalhe da Corrida", 40, 60, paintTitle);

                    // Conteúdo
                    canvas.DrawText($"Data: {corrida.Data:dd/MM/yyyy HH:mm}", 40, 100, paintBody);
                    canvas.DrawText($"Tempo: {corrida.TempoDecorrido}", 40, 130, paintBody);
                    canvas.DrawText($"Distância: {corrida.Distancia}", 40, 160, paintBody);
                    canvas.DrawText($"Ritmo: {corrida.Ritmo}", 40, 190, paintBody);
                    canvas.DrawText($"Passos estimados: {PassosEstimados}", 40, 220, paintBody);
                }

                document.EndPage();
            }
        }
    }
}