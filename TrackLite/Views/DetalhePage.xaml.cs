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
                    await ExportarCSVComoPDF();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Falha ao gerar arquivo: {ex.Message}", "OK");
            }
        }

        private async Task ExportarPDF()
        {
            if (CorridaSelecionada == null) return;

            try
            {
                var fileName = $"Corrida_{CorridaSelecionada.Data:yyyyMMdd_HHmm}.pdf";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                using var stream = new SKFileWStream(filePath);
                using var document = SKDocument.CreatePdf(stream);

                var pageInfo = new SKSize(595, 842);
                var canvas = document.BeginPage(pageInfo.Width, pageInfo.Height);

                var corPrimaria = SKColor.Parse("#214F4B");
                var corFundo = SKColor.Parse("#FCFCFC");
                var corPreto = SKColors.Black;
                var corCinza = SKColor.Parse("#666666");

                canvas.Clear(corFundo);
                canvas.DrawRect(0, 0, pageInfo.Width, 120, new SKPaint { Color = corPrimaria, IsAntialias = true });

                var paintTitulo = new SKPaint
                {
                    TextSize = 32,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                    Color = SKColors.White,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };
                canvas.DrawText("Relatório da Corrida", pageInfo.Width / 2, 70, paintTitulo);

                var paintData = new SKPaint
                {
                    TextSize = 14,
                    Typeface = SKTypeface.FromFamilyName("Arial"),
                    Color = SKColors.White,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };
                canvas.DrawText($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm}", pageInfo.Width / 2, 95, paintData);

                float y = 160;
                var paintRotulo = new SKPaint { TextSize = 16, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), Color = corPrimaria, IsAntialias = true };
                var paintValor = new SKPaint { TextSize = 16, Typeface = SKTypeface.FromFamilyName("Arial"), Color = corPreto, IsAntialias = true };
                var paintSeparador = new SKPaint { Color = SKColor.Parse("#E0E0E0"), StrokeWidth = 1, IsAntialias = true };

                void DesenharLinha(string rotulo, string valor)
                {
                    canvas.DrawText(rotulo, 60, y, paintRotulo);
                    canvas.DrawText(valor, 180, y, paintValor);
                    canvas.DrawLine(60, y + 5, pageInfo.Width - 60, y + 5, paintSeparador);
                    y += 40;
                }

                DesenharLinha("Data", $"{CorridaSelecionada.Data:dd/MM/yyyy HH:mm}");
                DesenharLinha("Distância", CorridaSelecionada.Distancia);
                DesenharLinha("Tempo", CorridaSelecionada.TempoDecorrido);
                DesenharLinha("Ritmo", CorridaSelecionada.Ritmo);
                DesenharLinha("Passos", PassosEstimados);

                canvas.DrawText("© 2025 TrackLite - Todos os direitos reservados", pageInfo.Width / 2, pageInfo.Height - 30, new SKPaint { TextSize = 12, Typeface = SKTypeface.FromFamilyName("Arial"), Color = corCinza, IsAntialias = true, TextAlign = SKTextAlign.Center });

                document.EndPage();
                document.Close();

                await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Falha ao gerar PDF: {ex.Message}", "OK");
            }
        }

        private async Task ExportarCSVComoPDF()
        {
            if (CorridaSelecionada == null)
            {
                await DisplayAlert("Erro", "Nenhuma corrida selecionada para exportar.", "OK");
                return;
            }

            try
            {
                var fileName = $"Corrida_CSV_{CorridaSelecionada.Data:yyyyMMdd_HHmm}.pdf";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

                using (var stream = new SKFileWStream(filePath))
                using (var document = SKDocument.CreatePdf(stream))
                {
                    var pageInfo = new SKSize(595, 842);
                    var corPrimaria = SKColor.Parse("#214F4B");
                    var corFundo = SKColor.Parse("#FCFCFC");
                    var corPreto = SKColors.Black;
                    var corCinza = SKColor.Parse("#666666");

                    var paintTexto = new SKPaint
                    {
                        TextSize = 12,
                        Typeface = SKTypeface.FromFamilyName("Arial"),
                        Color = corPreto,
                        IsAntialias = true
                    };

                    var paintRodape = new SKPaint
                    {
                        TextSize = 12,
                        Typeface = SKTypeface.FromFamilyName("Arial"),
                        Color = corCinza,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    };

                    float margemEsquerda = 60;
                    float larguraMaxima = pageInfo.Width - 2 * margemEsquerda;
                    float margemInferior = 60;
                    float y = 160;

                    SKCanvas canvas = null;

                    void NovaPagina(bool primeiraPagina = false)
                    {
                        if (!primeiraPagina)
                            document.EndPage();

                        canvas = document.BeginPage(pageInfo.Width, pageInfo.Height);
                        canvas.Clear(corFundo);

                        canvas.DrawRect(0, 0, pageInfo.Width, 120, new SKPaint { Color = corPrimaria, IsAntialias = true });
                        canvas.DrawText("Dados CSV da Corrida", pageInfo.Width / 2, 70, new SKPaint
                        {
                            TextSize = 28,
                            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                            Color = SKColors.White,
                            IsAntialias = true,
                            TextAlign = SKTextAlign.Center
                        });

                        canvas.DrawText($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm}", pageInfo.Width / 2, 95, new SKPaint
                        {
                            TextSize = 14,
                            Typeface = SKTypeface.FromFamilyName("Arial"),
                            Color = SKColors.White,
                            IsAntialias = true,
                            TextAlign = SKTextAlign.Center
                        });

                        y = 160;
                    }

                    NovaPagina(true);

                    void DrawTextWrapped(string text, float x, ref float y, float maxWidth, SKPaint paint)
                    {
                        var partes = text.Split(',');
                        string linha = "";

                        foreach (var parte in partes)
                        {
                            string palavra = parte.Trim();
                            if (palavra.Length > 0)
                                palavra += ",";

                            var testLine = string.IsNullOrEmpty(linha) ? palavra : $"{linha}{palavra}";
                            var width = paint.MeasureText(testLine);

                            if (width > maxWidth)
                            {
                                if (y > pageInfo.Height - margemInferior)
                                {
                                    NovaPagina();
                                }

                                canvas.DrawText(linha, x, y, paint);
                                y += paint.TextSize + 2;
                                linha = palavra;
                            }
                            else
                            {
                                linha = testLine;
                            }
                        }

                        if (!string.IsNullOrEmpty(linha))
                        {
                            if (y > pageInfo.Height - margemInferior)
                            {
                                NovaPagina();
                            }

                            canvas.DrawText(linha, x, y, paint);
                            y += paint.TextSize + 2;
                        }
                    }

                    var pontos = CorridaSelecionada.Rota;

                    string latitudes = string.Join(",", pontos.Select(p => p.lat.ToString("F6", CultureInfo.InvariantCulture)));
                    string longitudes = string.Join(",", pontos.Select(p => p.lng.ToString("F6", CultureInfo.InvariantCulture)));
                    string accuracies = string.Join(",", pontos.Select(p =>
                        p.accuracy > 0 ? p.accuracy.ToString("F1", CultureInfo.InvariantCulture) : "-"));
                    string timestamps = string.Join(",", pontos.Select(p =>
                        p.timestamp != default ? p.timestamp.ToString("yyyy-MM-dd HH:mm:ss") : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

                    DrawTextWrapped($"Latitudes: {latitudes}", margemEsquerda, ref y, larguraMaxima, paintTexto);
                    DrawTextWrapped($"Longitudes: {longitudes}", margemEsquerda, ref y, larguraMaxima, paintTexto);
                    DrawTextWrapped($"Accuracy: {accuracies}", margemEsquerda, ref y, larguraMaxima, paintTexto);
                    DrawTextWrapped($"Timestamp: {timestamps}", margemEsquerda, ref y, larguraMaxima, paintTexto);

                    y += 20;
                    if (y > pageInfo.Height - margemInferior) NovaPagina();

                    canvas.DrawText($"Data: {CorridaSelecionada.Data:dd/MM/yyyy HH:mm}", margemEsquerda, y, paintTexto); y += 18;
                    canvas.DrawText($"Distância: {CorridaSelecionada.Distancia}", margemEsquerda, y, paintTexto); y += 18;
                    canvas.DrawText($"Tempo: {CorridaSelecionada.TempoDecorrido}", margemEsquerda, y, paintTexto); y += 18;
                    canvas.DrawText($"Ritmo Médio: {CorridaSelecionada.Ritmo}", margemEsquerda, y, paintTexto); y += 18;
                    canvas.DrawText($"Passos Estimados: {PassosEstimados}", margemEsquerda, y, paintTexto);

                    canvas.DrawText("© 2025 TrackLite - Formato CSV", pageInfo.Width / 2, pageInfo.Height - 30, paintRodape);

                    document.EndPage();
                    document.Close();
                }

                await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Falha ao gerar PDF do CSV: {ex.Message}", "OK");
            }
        }


        private void CarregarMapa()
        {
            if (CorridaSelecionada == null || CorridaSelecionada.Rota.Count == 0) return;

            var pontosJson = System.Text.Json.JsonSerializer.Serialize(CorridaSelecionada.Rota.Select(p => new { p.lat, p.lng }));
            double lat = CorridaSelecionada.Rota[0].lat;
            double lng = CorridaSelecionada.Rota[0].lng;

            string html = $@"
<!DOCTYPE html>
<html>
<head>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>html, body, #map {{ height:100%; margin:0; padding:0; }}</style>
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
                CarregarGraficoComTemposReais();
            else
                MostrarGraficoVazio();
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
            float minPace = (float)Math.Floor(Math.Max(0, todosPaces.Min() * 0.85));

            for (int i = 0; i < temposPorKm.Count; i++)
            {
                double paceMin = temposPorKm[i].TotalMinutes;
                SKColor cor = paceMin <= paceMedio * 0.95
                    ? SKColor.Parse("#00C853")
                    : paceMin <= paceMedio * 1.05
                        ? SKColor.Parse("#FF9800")
                        : SKColor.Parse("#FF3D00");

                entries.Add(new ChartEntry((float)paceMin)
                {
                    Label = $"Km {i + 1}",
                    ValueLabel = "",
                    Color = cor,
                    TextColor = cor
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
                    Color = SKColor.Parse("#B0BEC5"),
                    TextColor = SKColor.Parse("#B0BEC5")
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
                    var chart = new LineChart
                    {
                        Entries = entries,
                        LineMode = LineMode.Spline,
                        LineSize = 6,
                        PointMode = PointMode.Circle,
                        PointSize = 14,
                        LabelOrientation = Orientation.Vertical,
                        LabelTextSize = 22,
                        ValueLabelTextSize = 0,
                        ValueLabelOption = ValueLabelOption.None,
                        Margin = 50,
                        BackgroundColor = SKColors.Transparent,
                        MaxValue = maxValue ?? entries.Max(e => e.Value).GetValueOrDefault() * 1.15f,
                        MinValue = minValue ?? entries.Min(e => e.Value).GetValueOrDefault() * 0.85f,
                        AnimationProgress = 1f
                    };

                    graficoFrame.Content = new ChartView
                    {
                        Chart = chart,
                        HorizontalOptions = LayoutOptions.FillAndExpand,
                        VerticalOptions = LayoutOptions.FillAndExpand
                    };
                    graficoFrame.ForceLayout();
                });
            }
        }
    }
}