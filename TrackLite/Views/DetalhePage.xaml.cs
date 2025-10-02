using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using SkiaSharp;
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

                if (double.TryParse(CorridaSelecionada.Distancia.Replace("km", "").Trim().Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double km))
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
            GerarPdfSkia(filePath, CorridaSelecionada);
            await DisplayAlert("PDF gerado", $"Arquivo salvo em:\n{filePath}", "OK");
            await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
        }

        private void GerarPdfSkia(string filePath, Corrida corrida)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var document = SKDocument.CreatePdf(stream))
            {
                const float pageWidth = 595;
                const float pageHeight = 842;
                using (var canvas = document.BeginPage(pageWidth, pageHeight))
                {
                    canvas.Clear(SKColors.White);

                    float margin = 50;
                    float y = 50;

                    var headerPaint = new SKPaint { Color = new SKColor(0x21, 0x4F, 0x4B) };
                    canvas.DrawRect(0, 0, pageWidth, 40, headerPaint);

                    var headerTextPaint = new SKPaint
                    {
                        Color = SKColors.White,
                        TextSize = 18,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                    };
                    string headerText = "TrackLite";
                    var headerTextWidth = headerTextPaint.MeasureText(headerText);
                    canvas.DrawText(headerText, (pageWidth - headerTextWidth) / 2, 27, headerTextPaint);

                    var paintTitle = new SKPaint
                    {
                        Color = new SKColor(0x21, 0x4F, 0x4B),
                        TextSize = 28,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                    };
                    string title = "Relatório da Corrida";
                    var titleWidth = paintTitle.MeasureText(title);
                    canvas.DrawText(title, (pageWidth - titleWidth) / 2, 85, paintTitle);

                    y = 130;

                    var paintSubtitle = new SKPaint
                    {
                        Color = new SKColor(0x21, 0x4F, 0x4B),
                        TextSize = 18,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                    };

                    var paintBody = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = 16,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
                    };

                    void DesenharLinha(string label, string valor)
                    {
                        canvas.DrawText(label, margin, y, paintSubtitle);
                        canvas.DrawText(valor, margin + 220, y, paintBody);
                        y += 35;
                    }

                    DesenharLinha("Data:", corrida.Data.ToString("dd/MM/yyyy HH:mm"));
                    DesenharLinha("Tempo:", corrida.TempoDecorrido);
                    DesenharLinha("Distância:", corrida.Distancia);
                    DesenharLinha("Ritmo:", corrida.Ritmo);
                    DesenharLinha("Passos estimados:", PassosEstimados);

                    var rodapePaint = new SKPaint
                    {
                        Color = SKColors.Gray,
                        TextSize = 12,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Italic)
                    };
                    string rodape = "Gerado por TrackLite";
                    var rodapeWidth = rodapePaint.MeasureText(rodape);
                    canvas.DrawText(rodape, (pageWidth - rodapeWidth) / 2, pageHeight - 40, rodapePaint);
                }
                document.EndPage();
            }
        }

        private async Task ExportarCSV()
        {
            if (CorridaSelecionada == null)
                return;

            var fileName = $"Corrida_{CorridaSelecionada!.Data:yyyyMMdd_HHmm}.pdf";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Latitude,Longitude");
            foreach (var ponto in CorridaSelecionada.Rota)
                sb.AppendLine($"{ponto.lat},{ponto.lng}");

            sb.AppendLine($"Data,{CorridaSelecionada.Data:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Tempo,{CorridaSelecionada.TempoDecorrido}");
            sb.AppendLine($"Distância,{CorridaSelecionada.Distancia}");
            sb.AppendLine($"Ritmo,{CorridaSelecionada.Ritmo}");
            sb.AppendLine($"Passos Estimados,{PassosEstimados}");

            GerarPdfCsv(filePath, sb.ToString());

            await DisplayAlert("PDF gerado", $"Arquivo salvo em:\n{filePath}", "OK");
            await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
        }

        private void GerarPdfCsv(string filePath, string conteudoCsv)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var document = SKDocument.CreatePdf(stream))
            {
                const float pageWidth = 595;
                const float pageHeight = 842;
                using (var canvas = document.BeginPage(pageWidth, pageHeight))
                {
                    canvas.Clear(SKColors.White);

                    float margin = 50;
                    float y = 50;

                    var headerPaint = new SKPaint { Color = new SKColor(0x21, 0x4F, 0x4B) };
                    canvas.DrawRect(0, 0, pageWidth, 40, headerPaint);

                    var headerTextPaint = new SKPaint
                    {
                        Color = SKColors.White,
                        TextSize = 18,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                    };
                    string headerText = "TrackLite";
                    var headerTextWidth = headerTextPaint.MeasureText(headerText);
                    canvas.DrawText(headerText, (pageWidth - headerTextWidth) / 2, 27, headerTextPaint);

                    var paintTitle = new SKPaint
                    {
                        Color = new SKColor(0x21, 0x4F, 0x4B),
                        TextSize = 22,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                    };
                    string title = "Relatório CSV";
                    var titleWidth = paintTitle.MeasureText(title);
                    canvas.DrawText(title, (pageWidth - titleWidth) / 2, 85, paintTitle);

                    y = 130;

                    var paintBody = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = 14,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal)
                    };

                    var linhas = conteudoCsv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var linha in linhas)
                    {
                        canvas.DrawText(linha, margin, y, paintBody);
                        y += 20; 
                    }

                    var rodapePaint = new SKPaint
                    {
                        Color = SKColors.Gray,
                        TextSize = 12,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Italic)
                    };
                    string rodape = "Gerado por TrackLite";
                    var rodapeWidth = rodapePaint.MeasureText(rodape);
                    canvas.DrawText(rodape, (pageWidth - rodapeWidth) / 2, pageHeight - 40, rodapePaint);
                }
                document.EndPage();
            }
        }

    }
}