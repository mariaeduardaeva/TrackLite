using Microcharts;
using Microcharts.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TrackLite.Models;
using TrackLite.Services;

namespace TrackLite
{
    [QueryProperty(nameof(CorridaSelecionada), "CorridaSelecionada")]
    public partial class DetalhePage : ContentPage
    {
        private Corrida? corridaSelecionada;
        private readonly DatabaseService _databaseService = new DatabaseService();

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

                if (corridaSelecionada != null) CarregarMapa();
                if (corridaSelecionada != null) CarregarGraficoPacePorKm();
            }
        }

        public string TempoFormatado => CorridaSelecionada?.TempoDecorrido ?? "--:--";
        public string DistanciaFormatada => CorridaSelecionada?.Distancia ?? "-";
        public string RitmoFormatado => CorridaSelecionada?.Ritmo ?? "-";
        public string DataFormatada => CorridaSelecionada?.Data.ToString("dd MMMM 'de' yyyy", new CultureInfo("pt-BR")) ?? "-";

        public string PassosEstimados
        {
            get
            {
                if (CorridaSelecionada == null || string.IsNullOrWhiteSpace(CorridaSelecionada.Distancia))
                    return "-";

                if (double.TryParse(
                    CorridaSelecionada.Distancia.Replace("km", "").Trim().Replace(",", "."),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
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

            string escolha = await DisplayActionSheet("Escolha o formato do arquivo:", "Cancelar", null, "PDF", "CSV");
            if (escolha == "Cancelar") return;

            try
            {
                if (escolha == "PDF")
                    await ExportarPDF();
                else if (escolha == "CSV")
                    await ExportarCSV();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Falha ao gerar arquivo: {ex.Message}", "OK");
            }
        }

        private async Task ExportarPDF()
        {
            var fileName = $"Corrida_{CorridaSelecionada!.Data:yyyyMMdd_HHmm}.pdf";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            await Launcher.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(filePath)
            });
        }

        private async Task ExportarCSV()
        {
            if (CorridaSelecionada == null) return;

            var fileName = $"Corrida_{CorridaSelecionada!.Data:yyyyMMdd_HHmm}.csv";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Latitude,Longitude");

            foreach (var ponto in CorridaSelecionada.Rota)
                sb.AppendLine($"{ponto.lat},{ponto.lng}");

            sb.AppendLine($"Data,{CorridaSelecionada.Data:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Tempo,{CorridaSelecionada.TempoDecorrido}");
            sb.AppendLine($"Distancia,{CorridaSelecionada.Distancia}");
            sb.AppendLine($"Ritmo,{CorridaSelecionada.Ritmo}");
            sb.AppendLine($"Passos Estimados,{PassosEstimados}");

            File.WriteAllText(filePath, sb.ToString());

            await Launcher.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(filePath)
            });
        }

        private void CarregarMapa()
        {
            if (CorridaSelecionada == null || CorridaSelecionada.Rota.Count == 0) return;

            var pontos = CorridaSelecionada.Rota;
            double lat = pontos[0].lat;
            double lng = pontos[0].lng;

            string pontosJson = System.Text.Json.JsonSerializer.Serialize(pontos);

            string html = $@"
<!DOCTYPE html>
<html>
<head>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
html, body, #map {{ height:100%; margin:0; padding:0; }}
</style>
</head>
<body>
<div id='map' style='width:100%; height:100%;'></div>
<script>
var map = L.map('map').setView([{lat}, {lng}], 15);
L.tileLayer('https://tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png').addTo(map);
var rotaCoords = {pontosJson};
var polyline = L.polyline(rotaCoords, {{color: '#21A179'}}).addTo(map);
map.fitBounds(polyline.getBounds());
</script>
</body>
</html>";

            MapaCorridaWebView.Source = new HtmlWebViewSource { Html = html };
        }

        private void CarregarGraficoPacePorKm()
        {
            if (CorridaSelecionada == null) return;

            if (CorridaSelecionada.TemposPorKm != null && CorridaSelecionada.TemposPorKm.Count > 0)
            {
                CarregarGraficoComTemposReais();
            }
            else
            {
                MostrarGraficoVazio();
            }
        }

        private void CarregarGraficoComTemposReais()
        {
            if (CorridaSelecionada?.TemposPorKm == null) return;

            var entries = new List<ChartEntry>();
            var temposPorKm = CorridaSelecionada.TemposPorKm;

            double tempoTotalMin = temposPorKm.Sum(t => t.TotalMinutes);
            double paceMedio = tempoTotalMin / temposPorKm.Count;

            for (int i = 0; i < temposPorKm.Count; i++)
            {
                double paceMin = temposPorKm[i].TotalMinutes;

                SKColor cor = paceMin <= paceMedio * 0.95
                    ? SKColor.Parse("#21A179")
                    : paceMin <= paceMedio * 1.05
                        ? SKColor.Parse("#FF9800") 
                        : SKColor.Parse("#F44336");

                string labelPace = $"{(int)Math.Floor(paceMin)}:{((int)Math.Round((paceMin % 1) * 60)):00}";

                entries.Add(new ChartEntry((float)paceMin)
                {
                    Label = $"Km {i + 1}",
                    ValueLabel = labelPace + " min/km",
                    Color = cor
                });
            }

            AtualizarGrafico(entries);
        }

        private void MostrarGraficoVazio()
        {
            var entries = new List<ChartEntry>
            {
                new ChartEntry(0)
                {
                    Label = "Sem dados",
                    ValueLabel = "Nenhum km completo",
                    Color = SKColor.Parse("#848484")
                }
            };

            AtualizarGrafico(entries);
        }

        private void AtualizarGrafico(List<ChartEntry> entries)
        {
            if (entries.Count == 0) return;

            var graficoFrame = this.FindByName<Frame>("GraficoPaceContainer");
            if (graficoFrame != null)
            {
                graficoFrame.Content = new ChartView
                {
                    Chart = new LineChart
                    {
                        Entries = entries,
                        LineMode = LineMode.Spline,
                        LineSize = 4,
                        PointMode = PointMode.Circle,
                        PointSize = 8,
                        LabelTextSize = 12,
                        ValueLabelOption = ValueLabelOption.TopOfElement,
                        BackgroundColor = SKColors.Transparent,
                        MaxValue = entries.Max(e => e.Value).GetValueOrDefault() * 1.1f,
                        MinValue = entries.Min(e => e.Value).GetValueOrDefault() * 0.9f
                    },
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.FillAndExpand
                };
            }
        }
    }
}