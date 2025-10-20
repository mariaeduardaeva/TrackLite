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
        private bool mapaPronto = false;
        private TaskCompletionSource<bool> mapaInicializadoTcs = new();

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

                if (corridaSelecionada != null)
                {
                    _ = CarregarMapaCompativel();
                    CarregarGraficoPacePorKm();
                }
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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (corridaSelecionada != null)
            {
                CarregarGraficoPacePorKm();
                if (!mapaPronto)
                {
                    await mapaInicializadoTcs.Task;
                }
            }
        }

        private async Task CarregarMapaCompativel()
        {
            if (CorridaSelecionada == null || CorridaSelecionada.Rota.Count == 0)
            {
                mapaPronto = true;
                mapaInicializadoTcs.TrySetResult(true);
                return;
            }

            try
            {
                var pontosJson = System.Text.Json.JsonSerializer.Serialize(
                    CorridaSelecionada.Rota.Select(p => new { p.lat, p.lng }));

                double lat = CorridaSelecionada.Rota[0].lat;
                double lng = CorridaSelecionada.Rota[0].lng;

                string html = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            height: 100%;
            font-family: Arial, sans-serif;
        }
        #map {
            width: 100%;
            height: 100%;
            background: #f0f0f0;
        }
        .fallback {
            width: 100%;
            height: 100%;
            display: flex;
            align-items: center;
            justify-content: center;
            text-align: center;
            color: #666;
            padding: 20px;
            box-sizing: border-box;
        }
        .leaflet-container {
            background: #f8f9fa;
        }
        .rota-style {
            stroke: true;
            color: '#21A179';
            weight: 4;
            opacity: 0.8;
        }
    </style>
</head>
<body>
    <div id='map'>
        <div class='fallback' id='fallback'>
            <div>
                <h3>TrackLite</h3>
                <p>Carregando rota...</p>
                <p><small>Mapa em carregamento...</small></p>
            </div>
        </div>
    </div>

    <script>
        var mapLoaded = false;
        var map, rotaLine;
        var rotaCoords = " + pontosJson + @";
        
        function loadLeaflet() {
            if (mapLoaded) return;
            
            var link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
            link.onerror = function() {
                showFallback('Mapa offline - Rota carregada');
                setTimeout(function() {
                    window.location.href = 'app://map-ready';
                }, 100);
            };
            document.head.appendChild(link);

            var script = document.createElement('script');
            script.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
            script.onload = initializeMap;
            script.onerror = function() {
                showFallback('Mapa offline - Rota carregada');
                setTimeout(function() {
                    window.location.href = 'app://map-ready';
                }, 100);
            };
            document.head.appendChild(script);
        }

        function initializeMap() {
            try {
                var fallback = document.getElementById('fallback');
                if (fallback) fallback.style.display = 'none';
                
                map = L.map('map', {
                    zoomControl: true,
                    dragging: true,
                    tap: false
                }).setView([" + lat.ToString().Replace(',', '.') + @", " + lng.ToString().Replace(',', '.') + @"], 15);

                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: 19,
                    attribution: '© OpenStreetMap'
                }).addTo(map);

                if (rotaCoords && rotaCoords.length > 0) {
                    rotaLine = L.polyline(rotaCoords, {
                        color: '#21A179',
                        weight: 4,
                        opacity: 0.8
                    }).addTo(map);
                    
                    map.fitBounds(rotaLine.getBounds());
                }

                mapLoaded = true;
                window.location.href = 'app://map-ready';
                
            } catch(error) {
                showFallback('Rota carregada - Mapa limitado');
                setTimeout(function() {
                    window.location.href = 'app://map-ready';
                }, 100);
            }
        }

        function showFallback(message) {
            var fallback = document.getElementById('fallback');
            if (fallback) {
                fallback.innerHTML = '<div><h3>TrackLite</h3><p>' + message + '</p><p><small>Rota com ' + rotaCoords.length + ' pontos</small></p></div>';
                fallback.style.display = 'flex';
            }
        }

        setTimeout(loadLeaflet, 50);

    </script>
</body>
</html>";

                MapaCorridaWebView.Source = new HtmlWebViewSource { Html = html };

                MapaCorridaWebView.Navigating += (s, e) =>
                {
                    if (e.Url?.Contains("map-ready") == true)
                    {
                        e.Cancel = true;
                        mapaPronto = true;
                        mapaInicializadoTcs.TrySetResult(true);
                    }
                };

            }
            catch (Exception ex)
            {
                mapaPronto = true;
                mapaInicializadoTcs.TrySetResult(true);
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
                    var corHeaderTabela = SKColor.Parse("#E5E5E5");
                    var corLinhasTabela = SKColor.Parse("#DDDDDD");

                    var paintTexto = new SKPaint
                    {
                        TextSize = 11,
                        Typeface = SKTypeface.FromFamilyName("Arial"),
                        Color = corPreto,
                        IsAntialias = true
                    };

                    var paintHeader = new SKPaint
                    {
                        TextSize = 11,
                        Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                        Color = corPreto,
                        IsAntialias = true
                    };

                    var paintLinha = new SKPaint
                    {
                        Color = corLinhasTabela,
                        StrokeWidth = 1,
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

                    float margemEsquerda = 40;
                    float larguraMaxima = pageInfo.Width - 2 * margemEsquerda;
                    float margemInferior = 60;
                    float y = 160;
                    float alturaLinha = 18;

                    SKCanvas canvas = null;

                    void NovaPagina(bool primeiraPagina = false)
                    {
                        if (!primeiraPagina)
                            document.EndPage();

                        canvas = document.BeginPage(pageInfo.Width, pageInfo.Height);
                        canvas.Clear(corFundo);

                        canvas.DrawRect(0, 0, pageInfo.Width, 120, new SKPaint { Color = corPrimaria });
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

                        y = 150;
                    }

                    NovaPagina(true);

                    var pontos = CorridaSelecionada.Rota;
                    if (pontos == null || pontos.Count == 0)
                    {
                        canvas.DrawText("Nenhum dado de rota disponível.", margemEsquerda, y, paintTexto);
                        document.EndPage();
                        document.Close();
                        return;
                    }

                    float[] colunas = { 50, 110, 190, 270, 370, 540 };

                    void DesenharCabecalho()
                    {
                        canvas.DrawRect(margemEsquerda, y, larguraMaxima, alturaLinha, new SKPaint { Color = corHeaderTabela });

                        canvas.DrawText("Nº", colunas[0], y + 13, paintHeader);
                        canvas.DrawText("Latitude", colunas[1], y + 13, paintHeader);
                        canvas.DrawText("Longitude", colunas[2], y + 13, paintHeader);
                        canvas.DrawText("Accuracy (m)", colunas[3], y + 13, paintHeader);
                        canvas.DrawText("Timestamp", colunas[4], y + 13, paintHeader);

                        for (int i = 0; i < colunas.Length; i++)
                        {
                            canvas.DrawLine(colunas[i] - 10, y, colunas[i] - 10, y + alturaLinha, paintLinha);
                        }
                        canvas.DrawLine(colunas[colunas.Length - 1] + 20, y, colunas[colunas.Length - 1] + 20, y + alturaLinha, paintLinha);

                        y += alturaLinha + 5;
                    }

                    DesenharCabecalho();

                    int contador = 1;
                    foreach (var p in pontos)
                    {
                        if (y > pageInfo.Height - margemInferior)
                        {
                            NovaPagina();
                            DesenharCabecalho();
                        }

                        string lat = p.lat.ToString("F6", CultureInfo.InvariantCulture);
                        string lng = p.lng.ToString("F6", CultureInfo.InvariantCulture);
                        string acc = p.accuracy > 0 ? p.accuracy.ToString("F1", CultureInfo.InvariantCulture) : "-";
                        string time = p.timestamp != default ? p.timestamp.ToString("dd/MM/yyyy HH:mm:ss") : "-";

                        if (paintTexto.MeasureText(time) > (pageInfo.Width - margemEsquerda - colunas[4] - 20))
                        {
                            time = time[..Math.Min(time.Length, 16)] + "...";
                        }

                        canvas.DrawText(contador.ToString(), colunas[0], y + 13, paintTexto);
                        canvas.DrawText(lat, colunas[1], y + 13, paintTexto);
                        canvas.DrawText(lng, colunas[2], y + 13, paintTexto);
                        canvas.DrawText(acc, colunas[3], y + 13, paintTexto);
                        canvas.DrawText(time, colunas[4], y + 13, paintTexto);

                        for (int i = 0; i < colunas.Length; i++)
                        {
                            canvas.DrawLine(colunas[i] - 10, y, colunas[i] - 10, y + alturaLinha, paintLinha);
                        }
                        canvas.DrawLine(colunas[colunas.Length - 1] + 20, y, colunas[colunas.Length - 1] + 20, y + alturaLinha, paintLinha);

                        y += alturaLinha;
                        contador++;
                    }

                    y += 25;
                    if (y > pageInfo.Height - margemInferior)
                        NovaPagina();

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

        private void CarregarGraficoPacePorKm()
        {
            if (CorridaSelecionada == null) return;

            if (CorridaSelecionada.TemposPorKm != null && CorridaSelecionada.TemposPorKm.Count > 0)
                CarregarGraficoComTemposReais();
            else if (TemDistanciaParcial())
                CarregarGraficoComParcial();
            else
                MostrarGraficoVazio();
        }

        private bool TemDistanciaParcial()
        {
            if (CorridaSelecionada == null || string.IsNullOrWhiteSpace(CorridaSelecionada.Distancia))
                return false;

            if (double.TryParse(
                CorridaSelecionada.Distancia.Replace("km", "").Trim().Replace(",", "."),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double km))
            {
                return km > 0 && (km < 1 || km % 1 != 0);
            }

            return false;
        }

        private void CarregarGraficoComParcial()
        {
            if (CorridaSelecionada == null) return;

            var entries = new List<ChartEntry>();

            double distanciaKm = ObterDistanciaEmKm();
            if (distanciaKm <= 0) return;

            double tempoMinutos = ConverterTempoParaMinutos(CorridaSelecionada.TempoDecorrido);
            if (tempoMinutos <= 0) return;

            double pace = tempoMinutos / distanciaKm;

            entries.Add(new ChartEntry((float)pace)
            {
                Label = $"{distanciaKm:F2} km",
                ValueLabel = "",
                Color = SKColor.Parse("#2196F3"),
                TextColor = SKColor.Parse("#2196F3")
            });

            float maxPace = (float)Math.Ceiling(pace * 1.3);
            float minPace = (float)Math.Floor(Math.Max(0, pace * 0.7));

            AtualizarGrafico(entries, maxPace, minPace);
        }

        private double ObterDistanciaEmKm()
        {
            if (CorridaSelecionada == null || string.IsNullOrWhiteSpace(CorridaSelecionada.Distancia))
                return 0;

            if (double.TryParse(
                CorridaSelecionada.Distancia.Replace("km", "").Trim().Replace(",", "."),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double km))
            {
                return km;
            }

            return 0;
        }

        private double ConverterTempoParaMinutos(string tempo)
        {
            if (string.IsNullOrWhiteSpace(tempo))
                return 0;

            try
            {
                var partes = tempo.Split(':');

                if (partes.Length == 2)
                {
                    if (int.TryParse(partes[0], out int minutos) &&
                        int.TryParse(partes[1], out int segundos))
                    {
                        return minutos + (segundos / 60.0);
                    }
                }
                else if (partes.Length == 3)
                {
                    if (int.TryParse(partes[0], out int horas) &&
                        int.TryParse(partes[1], out int minutos) &&
                        int.TryParse(partes[2], out int segundos))
                    {
                        return (horas * 60) + minutos + (segundos / 60.0);
                    }
                }
            }
            catch (Exception)
            {
            }

            return 0;
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

            double distanciaTotal = ObterDistanciaEmKm();
            if (distanciaTotal > temposPorKm.Count)
            {
                double distanciaParcial = distanciaTotal - temposPorKm.Count;
                if (distanciaParcial > 0)
                {
                    double tempoParcialMin = tempoTotalMin / temposPorKm.Count * distanciaParcial;
                    double paceParcial = tempoParcialMin / distanciaParcial;

                    entries.Add(new ChartEntry((float)paceParcial)
                    {
                        Label = $"{distanciaParcial:F2} km",
                        ValueLabel = "",
                        Color = SKColor.Parse("#2196F3"),
                        TextColor = SKColor.Parse("#2196F3")
                    });

                    maxPace = (float)Math.Max(maxPace, Math.Ceiling(paceParcial * 1.15));
                    minPace = (float)Math.Min(minPace, Math.Floor(Math.Max(0, paceParcial * 0.85)));
                }
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
                    ValueLabel = "Complete pelo menos 100m",
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