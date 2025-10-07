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
                OnPropertyChanged(nameof(MensagemMotivacional));
                OnPropertyChanged(nameof(SubtituloMotivacional));

                if (corridaSelecionada != null) CarregarMapa();
                if (corridaSelecionada != null) CarregarGraficoPacePorKm();
            }
        }

        public string TempoFormatado => CorridaSelecionada?.TempoDecorrido ?? "--:--";
        public string DistanciaFormatada => CorridaSelecionada?.Distancia ?? "-";
        public string RitmoFormatado => CorridaSelecionada?.Ritmo ?? "-";
        public string DataFormatada => CorridaSelecionada?.Data.ToString("dd MMMM 'de' yyyy", new CultureInfo("pt-BR")) ?? "-";

        public string MensagemMotivacional
        {
            get
            {
                var mensagens = new[]
                {
                    new { Titulo = "Missão cumprida! 🎯", Subtitulo = "Mais uma vitória na sua jornada." },
                    new { Titulo = "Excelente performance! ⚡", Subtitulo = "Seu esforço está rendendo frutos." },
                    new { Titulo = "Foco e determinação! 💪", Subtitulo = "Cada km é uma conquista." },
                    new { Titulo = "Você foi incrível! 🌟", Subtitulo = "Sua consistência inspira." },
                    new { Titulo = "Limites desafiados! 🚀", Subtitulo = "Você superou suas expectativas." },
                    new { Titulo = "Energia positiva! ✨", Subtitulo = "Mais um dia se movendo com propósito." },
                    new { Titulo = "Corredor dedicado! 🏆", Subtitulo = "Cada passada conta na sua evolução." },
                    new { Titulo = "Fôlego de campeão! 🥇", Subtitulo = "Sua determinação faz a diferença." },
                    new { Titulo = "Movimento constante! 🔄", Subtitulo = "A evolução acontece a cada treino." },
                };

                if (CorridaSelecionada == null)
                    return mensagens[0].Titulo;

                int seed = CorridaSelecionada.Id.GetHashCode();
                var random = new Random(seed);
                var mensagem = mensagens[random.Next(mensagens.Length)];

                return mensagem.Titulo;
            }
        }

        public string SubtituloMotivacional
        {
            get
            {
                var mensagens = new[]
                {
                    "Mais uma vitória na sua jornada.",
                    "Seu esforço está rendendo frutos.",
                    "Cada km é uma conquista.",
                    "Sua consistência inspira.",
                    "Você superou suas expectativas.",
                    "Mais um dia se movendo com propósito.",
                    "Cada passada conta na sua evolução.",
                    "Sua determinação faz a diferença.",
                    "A evolução acontece a cada treino.",
                    "Seus limites estão cada vez mais distantes."
                };

                if (CorridaSelecionada == null)
                    return mensagens[0];

                int seed = CorridaSelecionada.Id.GetHashCode() + 1;
                var random = new Random(seed);
                return mensagens[random.Next(mensagens.Length)];
            }
        }

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

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (corridaSelecionada != null)
            {
                CarregarGraficoPacePorKm();
            }
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

            var todosPaces = temposPorKm.Select(t => t.TotalMinutes).ToList();
            float maxPace = (float)Math.Ceiling(todosPaces.Max() * 1.15);
            float minPace = (float)Math.Floor(todosPaces.Min() * 0.85);

            for (int i = 0; i < temposPorKm.Count; i++)
            {
                double paceMin = temposPorKm[i].TotalMinutes;

                SKColor cor = paceMin <= paceMedio * 0.95
                    ? SKColor.Parse("#00C853")
                    : paceMin <= paceMedio * 1.05
                        ? SKColor.Parse("#FF9800")
                        : SKColor.Parse("#FF3D00");

                string labelPace = $"{(int)Math.Floor(paceMin)}:{((int)Math.Round((paceMin % 1) * 60)):00}";

                entries.Add(new ChartEntry((float)paceMin)
                {
                    Label = $"Km {i + 1}",
                    ValueLabel = labelPace,
                    Color = cor
                });
            }

            AtualizarGrafico(entries, maxPace, minPace);
        }

        private void MostrarGraficoVazio()
        {
            var entries = new List<ChartEntry>
            {
                new ChartEntry(5)
                {
                    Label = "Sem dados",
                    ValueLabel = "Complete pelo menos 1km",
                    Color = SKColor.Parse("#B0BEC5")
                }
            };

            AtualizarGrafico(entries, 10, 0);
        }

        private void AtualizarGrafico(List<ChartEntry> entries, float? maxValue = null, float? minValue = null)
        {
            if (entries.Count == 0) return;

            var graficoFrame = this.FindByName<Frame>("GraficoPaceContainer");
            if (graficoFrame != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(50); 

                    graficoFrame.Content = new ChartView
                    {
                        Chart = new LineChart
                        {
                            Entries = entries,
                            LineMode = LineMode.Spline,
                            LineSize = 6,
                            PointMode = PointMode.Circle,
                            PointSize = 14,
                            LabelTextSize = 11,
                            ValueLabelTextSize = 12,
                            ValueLabelOption = ValueLabelOption.TopOfElement,
                            BackgroundColor = SKColors.Transparent,
                            LabelOrientation = Orientation.Horizontal,
                            MaxValue = maxValue ?? entries.Max(e => e.Value).GetValueOrDefault() * 1.15f,
                            MinValue = minValue ?? entries.Min(e => e.Value).GetValueOrDefault() * 0.85f,

                            AnimationProgress = 1f 
                        },
                        HorizontalOptions = LayoutOptions.FillAndExpand,
                        VerticalOptions = LayoutOptions.FillAndExpand
                    };

                    graficoFrame.ForceLayout();
                });
            }
        }
    }
}
