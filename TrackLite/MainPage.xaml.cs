// BruTile
using BruTile.Cache;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using NetTopologySuite.Geometries;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace TrackLite
{
    public partial class MainPage : ContentPage
    {
        // ZOOM como índices (níveis OSM). Ex.: 10..18
        private const int MinZoomLevel = 10;
        private const int MaxZoomLevel = 18;

        private bool isTracking = false;
        private readonly List<MPoint> pontosRota = new();
        private MemoryLayer rotaLayer;
        private MemoryLayer usuarioLayer;

        private readonly System.Timers.Timer tempoTimer;
        private TimeSpan tempoDecorrido = TimeSpan.Zero;

        private CancellationTokenSource trackingCts;
        private double ultimaDistanciaVibrada = 0.0;

        public MainPage()
        {
            InitializeComponent();

            // Inicializa mapa
            mapView.Map = new Mapsui.Map();

            // Configura limites de zoom/resolutions compatíveis com OSM/GlobalSphericalMercator
            SetupZoomLimits();

            // Tile Layer híbrido online/offline
            AddHybridTileLayer();

            // Camada da rota
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

            // Camada do usuário
            usuarioLayer = new MemoryLayer
            {
                Name = "Usuário",
                Style = null,
                Features = new List<IFeature>()
            };
            mapView.Map.Layers.Add(usuarioLayer);

            Loaded += async (_, __) =>
            {
                RestaurarEstado();
                await CenterOnUserLocation();
            };

            // Inicializa timer
            tempoTimer = new System.Timers.Timer(1000);
            tempoTimer.Elapsed += (s, e) =>
            {
                tempoDecorrido = tempoDecorrido.Add(TimeSpan.FromSeconds(1));
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TempoLabel.Text = tempoDecorrido.ToString(@"hh\:mm\:ss");
                    AtualizarDistanciaEPace();
                    SalvarEstado();
                });
            };
        }

        // ------------------- Setup Zoom Limits (Mapsui v4) -------------------
        private void SetupZoomLimits()
        {
            try
            {
                var navigator = mapView.Map.Navigator;

                // usa as resolutions do esquema GlobalSphericalMercator (OSM)
                var mercator = new GlobalSphericalMercator();
                var mercResolutions = mercator.Resolutions.Select(r => r.Value.UnitsPerPixel).ToList();

                // override das resolutions para garantir consistência com tiles OSM
                navigator.OverrideResolutions = mercResolutions;

                // garante índices válidos
                int minIdx = Math.Max(0, MinZoomLevel);
                int maxIdx = Math.Min(mercResolutions.Count - 1, MaxZoomLevel);
                if (minIdx > maxIdx) (minIdx, maxIdx) = (maxIdx, minIdx);

                // para MMinMax: Min é a menor resolução (mais "zoom in"), Max é a maior (mais "zoom out")
                double minResolution = mercResolutions[maxIdx]; // resolução do nível mais "alto" (ex.: 18)
                double maxResolution = mercResolutions[minIdx]; // resolução do nível mais "baixo" (ex.: 10)

                navigator.OverrideZoomBounds = new MMinMax(minResolution, maxResolution);

                // Opcional: força o limiter para manter dentro do extent (evita "white borders")
                // navigator.Limiter = new ViewportLimiterKeepWithinExtent();
            }
            catch
            {
                // se algo falhar, apenas não aplicamos limites — evitar crash em runtime
            }
        }

        // ------------------- Helpers Zoom level/resolution -------------------
        private int GetClosestZoomLevelIndex()
        {
            var navigator = mapView.Map.Navigator;
            var resolutions = navigator.Resolutions;
            if (resolutions == null || resolutions.Count == 0)
                return -1;

            double currentRes = navigator.Viewport.Resolution;
            int bestIndex = 0;
            double bestDiff = double.MaxValue;
            for (int i = 0; i < resolutions.Count; i++)
            {
                var diff = Math.Abs(resolutions[i] - currentRes);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private void EnsureZoomWithinBounds()
        {
            try
            {
                var navigator = mapView.Map.Navigator;
                int current = GetClosestZoomLevelIndex();
                if (current < 0) return;

                if (current < MinZoomLevel)
                    navigator.ZoomToLevel(MinZoomLevel);
                else if (current > MaxZoomLevel)
                    navigator.ZoomToLevel(MaxZoomLevel);
            }
            catch { }
        }

        // ------------------- Persistência -------------------
        private void SalvarEstado()
        {
            try
            {
                var rotaJson = JsonSerializer.Serialize(pontosRota);
                Preferences.Set("Rota", rotaJson);
                Preferences.Set("Tempo", tempoDecorrido.Ticks);
                Preferences.Set("IsTracking", isTracking);
                Preferences.Set("UltimaDistanciaVibrada", ultimaDistanciaVibrada);
            }
            catch { }
        }

        private void RestaurarEstado()
        {
            try
            {
                if (Preferences.ContainsKey("Rota"))
                {
                    var rotaJson = Preferences.Get("Rota", "");
                    var pontos = JsonSerializer.Deserialize<List<MPoint>>(rotaJson);
                    if (pontos != null && pontos.Count > 0)
                    {
                        pontosRota.Clear();
                        pontosRota.AddRange(pontos);

                        if (pontosRota.Count >= 2)
                        {
                            var coords = pontosRota.Select(p => new Coordinate(p.X, p.Y)).ToArray();
                            var line = new LineString(coords);
                            rotaLayer.Features = new List<IFeature> { new GeometryFeature { Geometry = line } };
                        }

                        var ultimo = pontosRota.Last();
                        usuarioLayer.Features = new List<IFeature>
                        {
                            new GeometryFeature
                            {
                                Geometry = new NetTopologySuite.Geometries.Point(ultimo.X, ultimo.Y),
                                Styles = new List<IStyle>
                                {
                                    new SymbolStyle
                                    {
                                        Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromArgb(255, 0x16, 0xC0, 0x72)),
                                        Outline = new Pen(Mapsui.Styles.Color.FromArgb(255, 0xF8, 0xF4, 0xF4), 2),
                                        SymbolScale = 0.6
                                    }
                                }
                            }
                        };
                        mapView.RefreshGraphics();
                        mapView.Map.Navigator.CenterOn(ultimo);

                        // garante que o zoom automático respeite limites
                        EnsureZoomWithinBounds();
                    }
                }

                if (Preferences.ContainsKey("Tempo"))
                {
                    long ticks = Preferences.Get("Tempo", 0L);
                    tempoDecorrido = TimeSpan.FromTicks(ticks);
                    TempoLabel.Text = tempoDecorrido.ToString(@"hh\:mm\:ss");
                }

                if (Preferences.ContainsKey("UltimaDistanciaVibrada"))
                    ultimaDistanciaVibrada = Preferences.Get("UltimaDistanciaVibrada", 0.0);

                AtualizarDistanciaEPace();

                if (Preferences.Get("IsTracking", false))
                {
                    _ = StartTracking();
                }
            }
            catch { }
        }

        // ------------------- Tile Layer -------------------
        private void AddHybridTileLayer()
        {
            var cachePath = Path.Combine(FileSystem.AppDataDirectory, "tilecache");
            if (!Directory.Exists(cachePath))
                Directory.CreateDirectory(cachePath);

            var fileCache = new FileCache(cachePath, "png");

            var tileSchema = new GlobalSphericalMercator(0, 18);
            var url = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

            var httpTileSource = new HttpTileSource(
                tileSchema,
                url,
                name: "OSM",
                persistentCache: fileCache);

            var tileLayer = new TileLayer(httpTileSource)
            {
                Name = "OSM Online/Offline"
            };

            var toRemove = mapView.Map.Layers.OfType<TileLayer>().ToList();
            foreach (var l in toRemove) mapView.Map.Layers.Remove(l);

            mapView.Map.Layers.Insert(0, tileLayer);
        }

        // ------------------- Animação Botões -------------------
        private async Task TrocarIconeComBounceAsync(Button botao, string novoArquivo)
        {
            try
            {
                await botao.FadeTo(0, 150);
                botao.ImageSource = ImageSource.FromFile(novoArquivo);
                await botao.FadeTo(1, 150);
                await botao.ScaleTo(1.08, 180, Easing.CubicOut);
                await botao.ScaleTo(1.0, 180, Easing.CubicOut);
            }
            catch { }
        }

        private async Task BotaoAnimadoAsync(Button botao)
        {
            try
            {
                await botao.FadeTo(0.6, 100);
                await botao.FadeTo(1, 100);
                await botao.ScaleTo(1.08, 180, Easing.CubicOut);
                await botao.ScaleTo(1.0, 180, Easing.CubicOut);
            }
            catch { }
        }

        // ------------------- Localização -------------------
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

                    // aplica zoom apenas se fora do limite atual
                    EnsureZoomWithinBounds();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Localização", $"Não foi possível obter a localização: {ex.Message}", "OK");
            }
        }

        // ------------------- Tracking -------------------
        private async Task StartTracking()
        {
            isTracking = true;
            await TrocarIconeComBounceAsync(PlayPauseButton, "pausar.png");

            if (pontosRota.Count == 0) // só zera se não tiver rota
            {
                rotaLayer.Features = new List<IFeature>();
                usuarioLayer.Features = new List<IFeature>();
                tempoDecorrido = TimeSpan.Zero;
                ultimaDistanciaVibrada = 0.0;
            }

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

                            if (pontosRota.Count >= 2)
                            {
                                var coords = pontosRota.Select(p => new Coordinate(p.X, p.Y)).ToArray();
                                var line = new LineString(coords);
                                rotaLayer.Features = new List<IFeature> { new GeometryFeature { Geometry = line } };
                            }

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

                            // garante que o zoom esteja dentro dos limites definidos
                            EnsureZoomWithinBounds();

                            AtualizarDistanciaEPace();
                            SalvarEstado();
                        }
                    }
                    catch { }

                    await Task.Delay(2000, token);
                }
            }
            catch (TaskCanceledException) { }
        }

        private async void StopTracking()
        {
            if (!isTracking) return;

            trackingCts?.Cancel();
            isTracking = false;
            await TrocarIconeComBounceAsync(PlayPauseButton, "playy.png");
            tempoTimer.Stop();
            SalvarEstado();
        }

        // ------------------- Botões -------------------
        private async void PlayPauseButton_Clicked(object sender, EventArgs e)
        {
            if (!isTracking)
                await StartTracking();
            else
                StopTracking();
        }

        private async void SaveButton_Clicked(object sender, EventArgs e)
        {
            if (isTracking)
                StopTracking();

            DisplayAlert("Salvar Rota", "Rota salva! Iria para a página de histórico.", "OK");

            await BotaoAnimadoAsync(SaveButton);
        }

        private async void DeletarRota_Clicked(object sender, EventArgs e)
        {
            StopTracking();
            pontosRota.Clear();
            rotaLayer.Features = new List<IFeature>();
            usuarioLayer.Features = new List<IFeature>();
            mapView.RefreshGraphics();
            tempoDecorrido = TimeSpan.Zero;
            TempoLabel.Text = "00:00:00";
            DistanciaLabel.Text = "0 km";
            PaceLabel.Text = "0:00";
            ultimaDistanciaVibrada = 0.0;
            Preferences.Clear();

            await BotaoAnimadoAsync(DeleteButton);
        }

        private async void LocalButton_Clicked(object sender, EventArgs e)
        {
            await CenterOnUserLocation();
            await BotaoAnimadoAsync(LocalButton);
        }

        // ------------------- Distância e Pace -------------------
        private void AtualizarDistanciaEPace()
        {
            double distancia = 0;
            for (int i = 1; i < pontosRota.Count; i++)
                distancia += CalcularDistancia(pontosRota[i - 1], pontosRota[i]);

            double distanciaKm = distancia / 1000.0;
            DistanciaLabel.Text = $"{distanciaKm:F2} km";

            // Vibração a cada km
            if (distanciaKm - ultimaDistanciaVibrada >= 1.0)
            {
                ultimaDistanciaVibrada = Math.Floor(distanciaKm);
                Vibrar();
            }

            // Cálculo do pace com proteção para distâncias pequenas
            const double minDistanceKm = 0.2; // só calcula pace a partir de 200m

            if (distanciaKm >= minDistanceKm && tempoDecorrido.TotalSeconds > 0)
            {
                double paceSegundosPorKm = tempoDecorrido.TotalSeconds / distanciaKm;
                int paceMin = (int)Math.Floor(paceSegundosPorKm / 60);
                int paceSec = (int)Math.Round(paceSegundosPorKm % 60);

                if (paceSec == 60)
                {
                    paceSec = 0;
                    paceMin++;
                }

                PaceLabel.Text = $"{paceMin}:{paceSec:D2}";
            }
            else
            {
                PaceLabel.Text = "0:00";
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

        private void Vibrar()
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500));
            }
            catch { }
        }
    }
}