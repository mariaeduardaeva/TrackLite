using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Microsoft.Maui.ApplicationModel;
using NetTopologySuite.Geometries;
using System.Text.Json;
using System.Timers;
using System.Threading;

namespace TrackLite
{
    public partial class MainPage : ContentPage
    {
        private const double MinZoomLevel = 1;
        private const double MaxZoomLevel = 20;

        private bool isTracking = false;
        private List<MPoint> pontosRota = new List<MPoint>();
        private MemoryLayer rotaLayer;
        private MemoryLayer usuarioLayer;

        private System.Timers.Timer tempoTimer;
        private TimeSpan tempoDecorrido = TimeSpan.Zero;

        private CancellationTokenSource trackingCts;

        public MainPage()
        {
            InitializeComponent();

            // Inicializa mapa
            mapView.Map ??= new Mapsui.Map();
            mapView.Map.Layers.Add(OpenStreetMap.CreateTileLayer());

            // Camada da rota (linha #214F4B)
            rotaLayer = new MemoryLayer
            {
                Name = "Rota",
                Style = new VectorStyle
                {
                    Line = new Pen(Mapsui.Styles.Color.FromArgb(255, 33, 79, 75), 4)
                },
                Features = new List<IFeature>()
            };
            mapView.Map.Layers.Add(rotaLayer);

            // Camada do usuário (cada ponto terá SymbolStyle)
            usuarioLayer = new MemoryLayer
            {
                Name = "Usuário",
                Style = null, // cada ponto terá seu próprio SymbolStyle
                Features = new List<IFeature>()
            };
            mapView.Map.Layers.Add(usuarioLayer);

            // Centraliza no usuário ao carregar
            Loaded += async (_, __) => await CenterOnUserLocation();

            // Zoom mínimo/máximo
            mapView.Map.Navigator.ViewportChanged += (_, __) =>
            {
                var zoom = mapView.Map.Navigator.Viewport.Resolution;
                if (zoom < MinZoomLevel)
                    mapView.Map.Navigator.ZoomTo(MinZoomLevel);
                if (zoom > MaxZoomLevel)
                    mapView.Map.Navigator.ZoomTo(MaxZoomLevel);
            };

            // Inicializa timer do tempo
            tempoTimer = new System.Timers.Timer(1000); // 1 segundo
            tempoTimer.Elapsed += (s, e) =>
            {
                tempoDecorrido = tempoDecorrido.Add(TimeSpan.FromSeconds(1));
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TempoLabel.Text = tempoDecorrido.ToString(@"hh\:mm\:ss");
                    AtualizarDistanciaEPace();
                });
            };
        }

        private async Task CenterOnUserLocation()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location != null)
                {
                    var merc = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                    var userPoint = new MPoint(merc.x, merc.y);

                    // Cores para o ponto do usuário
                    var corUsuario = Mapsui.Styles.Color.FromArgb(255, 0x16, 0xC0, 0x72);
                    var corContorno = Mapsui.Styles.Color.FromArgb(255, 0xF8, 0xF4, 0xF4);

                    var pontoFeature = new GeometryFeature
                    {
                        Geometry = new NetTopologySuite.Geometries.Point(userPoint.X, userPoint.Y),
                        Styles = new List<IStyle>
                        {
                            new SymbolStyle
                            {
                                Fill = new Mapsui.Styles.Brush(corUsuario),
                                Outline = new Pen(corContorno, 2),
                                SymbolScale = 0.6
                            }
                        }
                    };

                    usuarioLayer.Features = new List<IFeature> { pontoFeature };

                    mapView.RefreshGraphics();
                    mapView.Map.Navigator.CenterOn(userPoint);
                    mapView.Map.Navigator.ZoomTo(15);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Localização", $"Não foi possível obter a localização: {ex.Message}", "OK");
            }
        }

        private async Task StartTracking()
        {
            isTracking = true;
            pontosRota.Clear();
            rotaLayer.Features = new List<IFeature>();
            usuarioLayer.Features = new List<IFeature>();
            tempoDecorrido = TimeSpan.Zero;
            tempoTimer.Start();

            trackingCts = new CancellationTokenSource();
            var token = trackingCts.Token;

            var corUsuario = Mapsui.Styles.Color.FromArgb(255, 0x16, 0xC0, 0x72);
            var corContorno = Mapsui.Styles.Color.FromArgb(255, 0xF8, 0xF4, 0xF4);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(2));
                        var location = await Geolocation.Default.GetLocationAsync(request);

                        if (location != null)
                        {
                            var merc = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                            var ponto = new MPoint(merc.x, merc.y);

                            pontosRota.Add(ponto);

                            // Atualiza a rota
                            if (pontosRota.Count >= 2)
                            {
                                var coords = pontosRota.Select(p => new Coordinate(p.X, p.Y)).ToArray();
                                var line = new LineString(coords);
                                rotaLayer.Features = new List<IFeature> { new GeometryFeature { Geometry = line } };
                            }

                            // Atualiza ponto do usuário
                            var pontoFeature = new GeometryFeature
                            {
                                Geometry = new NetTopologySuite.Geometries.Point(ponto.X, ponto.Y),
                                Styles = new List<IStyle>
                                {
                                    new SymbolStyle
                                    {
                                        Fill = new Mapsui.Styles.Brush(corUsuario),
                                        Outline = new Pen(corContorno, 2),
                                        SymbolScale = 0.6
                                    }
                                }
                            };
                            usuarioLayer.Features = new List<IFeature> { pontoFeature };

                            mapView.RefreshGraphics();
                            mapView.Map.Navigator.CenterOn(ponto);

                            AtualizarDistanciaEPace();
                        }
                    }
                    catch { }

                    await Task.Delay(2000, token);
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                tempoTimer.Stop();
                isTracking = false;
            }
        }

        private void StopTracking()
        {
            trackingCts?.Cancel();
        }

        private void PlayPauseButton_Clicked(object sender, EventArgs e)
        {
            if (!isTracking)
            {
                _ = StartTracking();
                PlayPauseButton.ImageSource = ImageSource.FromFile("pausar.png");
            }
            else
            {
                StopTracking();
                PlayPauseButton.ImageSource = ImageSource.FromFile("playy.png");
            }
        }

        private void SaveButton_Clicked(object sender, EventArgs e)
        {
            if (isTracking)
                StopTracking();

            DisplayAlert("Salvar Rota", "Rota salva! Iria para a página de histórico.", "OK");

            PlayPauseButton.ImageSource = ImageSource.FromFile("playy.png");
        }

        private void DeletarRota_Clicked(object sender, EventArgs e)
        {
            pontosRota.Clear();
            rotaLayer.Features = new List<IFeature>();
            usuarioLayer.Features = new List<IFeature>();
            mapView.RefreshGraphics();
            tempoDecorrido = TimeSpan.Zero;
            TempoLabel.Text = "00:00:00";
            DistanciaLabel.Text = "0 km";
            PaceLabel.Text = "0:00";
        }

        private void AtualizarDistanciaEPace()
        {
            double distancia = 0;
            for (int i = 1; i < pontosRota.Count; i++)
                distancia += CalcularDistancia(pontosRota[i - 1], pontosRota[i]);

            double distanciaKm = distancia / 1000.0;
            DistanciaLabel.Text = $"{distanciaKm:F2} km";

            if (distanciaKm > 0)
            {
                double paceSegundos = tempoDecorrido.TotalSeconds / distanciaKm;
                int paceMin = (int)(paceSegundos / 60);
                int paceSec = (int)(paceSegundos % 60);
                PaceLabel.Text = $"{paceMin}:{paceSec:D2}";
            }
        }

        private double CalcularDistancia(MPoint p1, MPoint p2)
        {
            var lonLat1 = SphericalMercator.ToLonLat(p1.X, p1.Y);
            var lonLat2 = SphericalMercator.ToLonLat(p2.X, p2.Y);
            return Haversine(lonLat1.lat, lonLat1.lon, lonLat2.lat, lonLat2.lon);
        }

        private double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371000;
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}