using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TrackLite.Models;
using TrackLite.Services;

namespace TrackLite
{
    public partial class MainPage : ContentPage
    {
        private readonly DatabaseService _databaseService = new DatabaseService();
        private List<Ponto> rota = new();
        private bool isTracking = false;
        private static bool _trackingGlobalAtivo = false;
        private System.Timers.Timer tempoTimer;
        private TimeSpan tempoDecorrido = TimeSpan.Zero;
        private double ultimaDistanciaVibrada = 0.0;
        private CancellationTokenSource trackingCts;
        private bool mapaPronto = false;
        private TaskCompletionSource<bool> mapaInicializadoTcs = new();
        private const int ZoomInicial = 18;
        private const int MinZoom = 10;
        private const int MaxZoom = 18;
        private bool _estaRestaurandoEstado = false;
        private bool _deveContinuarTracking = false;
        private int _intervaloColeta = 1000;
        private double _limiarAccuracy = 20.0;
        private bool _vibracaoKmAtivada = true;

        private List<TimeSpan> temposPorKm = new List<TimeSpan>();
        private double kmAtual = 1.0;
        private TimeSpan tempoUltimoKm = TimeSpan.Zero;

        public MainPage()
        {
            InitializeComponent();
            CarregarConfiguracoes();
            CarregarMapa();
            InicializarTimer();
            ConfigurarEventos();
        }

        #region Configurações
        private void CarregarConfiguracoes()
        {
            try
            {
                _intervaloColeta = AppSettings.FrequenciaColeta * 1000;
                _limiarAccuracy = AppSettings.GetAccuracyInMeters();
                _vibracaoKmAtivada = AppSettings.VibracaoKm;

                Console.WriteLine("=== CONFIGURAÇÕES CARREGADAS ===");
                Console.WriteLine($"Frequencia: {AppSettings.FrequenciaColeta}s -> {_intervaloColeta}ms");
                Console.WriteLine($"Accuracy: {AppSettings.LimiarAccuracy} -> {_limiarAccuracy}m");
                Console.WriteLine($"Vibração: {_vibracaoKmAtivada}");
                Console.WriteLine("================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar configurações: {ex.Message}");
                _intervaloColeta = 1000;
                _limiarAccuracy = 20.0;
                _vibracaoKmAtivada = true;
            }
        }
        #endregion

        #region Timer
        private void InicializarTimer()
        {
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
        #endregion

        #region Eventos
        private void ConfigurarEventos()
        {
            App.Current.PageAppearing += OnPageAppearing;
            App.Current.PageDisappearing += OnPageDisappearing;
            Loaded += async (_, __) =>
            {
                if (!_trackingGlobalAtivo)
                    RestaurarEstado();
                await mapaInicializadoTcs.Task;
            };
        }
        #endregion

        #region Mapa
        private async void CarregarMapa()
        {
            double lat = 0, lng = 0;
            try
            {
                if (await VerificarPermissaoLocalizacao())
                {
                    var location = await ObterLocalizacaoComAccuracy();
                    if (location != null)
                    {
                        lat = location.Latitude;
                        lng = location.Longitude;
                    }
                }
            }
            catch { }

            var html = $@"<!DOCTYPE html>
<html>
<head>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
html, body, #map {{ height:100%; margin:0; padding:0; }}
.user-icon {{
    border: 2px solid #FCFCFC;
    border-radius: 50%;
    width: 18px;
    height: 18px;
    background-color: #16C072;
    animation: pulse 2s ease-in-out infinite;
}}
@keyframes pulse {{
    0% {{ transform: scale(1); box-shadow: 0 0 0 0 rgba(22, 192, 114, 0.7); }}
    50% {{ transform: scale(1.2); box-shadow: 0 0 10px 5px rgba(22, 192, 114, 0.7); }}
    100% {{ transform: scale(1); box-shadow: 0 0 0 0 rgba(22, 192, 114, 0); }}
}}
</style>
</head>
<body>
<div id='map'></div>
<script>
var minZoom = {MinZoom};
var maxZoom = {MaxZoom};
var initialZoom = {ZoomInicial};
window.map = L.map('map', {{
    center: [{lat}, {lng}],
    zoom: initialZoom,
    minZoom: minZoom,
    maxZoom: maxZoom,
    scrollWheelZoom: true
}});
L.tileLayer('https://tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
    maxZoom: maxZoom,
    minZoom: minZoom,
    noWrap: true
}}).addTo(window.map);
window.rotaLine = L.polyline([], {{color: '#214F4B', weight: 4}}).addTo(window.map);
var usuarioIcon = L.divIcon({{
    html: '<div class=""user-icon""></div>',
    className: '',
    iconSize: [18, 18],
    iconAnchor: [9, 9]
}});
window.usuarioMarker = L.marker([{lat}, {lng}], {{icon: usuarioIcon}}).addTo(window.map);
function atualizarUsuario(lat, lng) {{
    window.usuarioMarker.setLatLng([lat,lng]);
    window.map.panTo([lat,lng], {{animate: true, duration: 1}});
    window.rotaLine.addLatLng([lat,lng]);
}}
function centralizar(lat, lng) {{
    window.map.setView([lat,lng], initialZoom, {{animate: true, duration: 1}});
}}
function limparRota() {{
    if (window.rotaLine) {{
        window.map.removeLayer(window.rotaLine);
    }}
    window.rotaLine = L.polyline([], {{color: '#214F4B', weight: 4}}).addTo(window.map);
}}
window.map.on('zoomend', function() {{
    if (window.map.getZoom() > maxZoom) {{
        window.map.setZoom(maxZoom);
    }}
    if (window.map.getZoom() < minZoom) {{
        window.map.setZoom(minZoom);
    }}
}});
setTimeout(function() {{
    window.location.href = 'app://map-ready';
}}, 100);
</script>
</body>
</html>";
            MapWebView.Source = new HtmlWebViewSource { Html = html };
            MapWebView.Navigating += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Url) && e.Url.StartsWith("app://map-ready"))
                {
                    e.Cancel = true;
                    mapaPronto = true;
                    mapaInicializadoTcs.TrySetResult(true);
                }
            };
        }
        #endregion

        #region Localização
        private async Task<Location> ObterLocalizacaoComAccuracy()
        {
            try
            {
                var accuracy = AppSettings.GetGeolocationAccuracy();
                var request = new GeolocationRequest(accuracy, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location == null)
                {
                    MostrarSemSinal();
                    return null;
                }

                OcultarSemSinal();
                return location;
            }
            catch
            {
                MostrarSemSinal();
                return null;
            }
        }

        private async Task<bool> VerificarPermissaoLocalizacao()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                bool abrirConfig = await DisplayAlert("Permissão necessária", "O aplicativo precisa de acesso à localização para funcionar corretamente.\n\nDeseja abrir as configurações para permitir o GPS?", "Abrir Configurações", "Cancelar");
                if (abrirConfig) AppInfo.Current.ShowSettingsUI();
                return false;
            }
            return true;
        }

        private async Task AtualizarMapa(double lat, double lng)
        {
            if (!mapaPronto) return;

            if (rota.Count > 0)
            {
                var ultimo = rota[^1];
                double distancia = Haversine(ultimo, new Ponto { lat = lat, lng = lng });
                if (distancia < (_limiarAccuracy / 1000.0)) return;
            }

            rota.Add(new Ponto { lat = lat, lng = lng, accuracy = _limiarAccuracy, timestamp = DateTime.Now });

            string jsAtualizarUsuario = $"atualizarUsuario({lat}, {lng});";
            await MapWebView.EvaluateJavaScriptAsync(jsAtualizarUsuario);

            string jsAtualizarRota = "if (window.rotaLine) { window.rotaLine.setLatLngs([";
            for (int i = 0; i < rota.Count; i++)
            {
                jsAtualizarRota += $"[{rota[i].lat},{rota[i].lng}]";
                if (i < rota.Count - 1) jsAtualizarRota += ",";
            }
            jsAtualizarRota += "]); }";
            await MapWebView.EvaluateJavaScriptAsync(jsAtualizarRota);

            AtualizarDistanciaEPace();
        }

        private async Task CentralizarUsuario()
        {
            if (!mapaPronto) await mapaInicializadoTcs.Task;
            if (!await VerificarPermissaoLocalizacao()) return;
            var location = await ObterLocalizacaoComAccuracy();
            if (location != null)
            {
                string js = $"centralizar({location.Latitude}, {location.Longitude});";
                await MapWebView.EvaluateJavaScriptAsync(js);
                await AtualizarMapa(location.Latitude, location.Longitude);
            }
        }

        private async void LocalButton_Clicked(object sender, EventArgs e)
        {
            if (!mapaPronto) return;
            if (!await VerificarPermissaoLocalizacao()) return;
            var location = await ObterLocalizacaoComAccuracy();
            if (location != null)
            {
                string js = $"window.map.setView([{location.Latitude}, {location.Longitude}], {ZoomInicial}, {{animate: true, duration: 1}});";
                await MapWebView.EvaluateJavaScriptAsync(js);
            }
        }
        #endregion

        #region Tracking
        private async Task StartTracking()
        {
            if (_trackingGlobalAtivo)
            {
                isTracking = true;
                PlayPauseButton.ImageSource = "pausar.png";
                tempoTimer.Start();
                return;
            }
            if (!await VerificarPermissaoLocalizacao()) return;
            _trackingGlobalAtivo = true;
            DeviceDisplay.KeepScreenOn = true;
            ultimaDistanciaVibrada = 0;
            if (mapaPronto && rota.Count == 0)
                try { await MapWebView.EvaluateJavaScriptAsync("limparRota();"); } catch { }
            isTracking = true;
            PlayPauseButton.ImageSource = "pausar.png";
            tempoTimer.Start();
            await IniciarTrackingLocalizacao();
        }

        private async Task IniciarTrackingLocalizacao()
        {
            trackingCts = new CancellationTokenSource();
            var token = trackingCts.Token;

            Ponto ultimoPonto = null;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var location = await ObterLocalizacaoComAccuracy();
                    if (location != null)
                    {
                        double accuracy = location.Accuracy ?? 9999;
                        double velocidade = 0.0;

                        if (ultimoPonto != null)
                        {
                            double distancia = Haversine(ultimoPonto, new Ponto { lat = location.Latitude, lng = location.Longitude });
                            double tempoSegundos = (DateTime.Now - ultimoPonto.timestamp).TotalSeconds;
                            if (tempoSegundos > 0)
                                velocidade = distancia / tempoSegundos; 
                        }

                        bool pontoValido =
                            accuracy <= 50 && 
                            (velocidade == 0 || velocidade <= 7);

                        if (pontoValido)
                        {
                            ultimoPonto = new Ponto
                            {
                                lat = location.Latitude,
                                lng = location.Longitude,
                                accuracy = accuracy,
                                timestamp = DateTime.Now
                            };

                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await AtualizarMapa(ultimoPonto.lat, ultimoPonto.lng);
                            });
                        }
                        else
                        {
                            Console.WriteLine($"📍 Ponto descartado: accuracy={accuracy:F1}m, velocidade={velocidade:F2}m/s");
                        }
                    }

                    await Task.Delay(_intervaloColeta, token);
                }
            }
            catch (TaskCanceledException) { }
        }


        private void StopTracking()
        {
            if (!isTracking) return;
            trackingCts?.Cancel();
            isTracking = false;
            _trackingGlobalAtivo = false;
            PlayPauseButton.ImageSource = "playy.png";
            tempoTimer.Stop();
            DeviceDisplay.KeepScreenOn = false;
            SalvarEstado();
        }

        private async void PlayPauseButton_Clicked(object sender, EventArgs e)
        {
            if (!isTracking) await StartTracking();
            else StopTracking();
        }
        #endregion

        #region Rota
        private async void DeletarRota_Clicked(object sender, EventArgs e)
        {
            StopTracking();
            await LimparEstadoRota();
            await CentralizarUsuario();
            VerificarConfiguracoesAposDeletar();
            CarregarConfiguracoes();
        }

        private async void SaveButton_Clicked(object sender, EventArgs e)
        {
            StopTracking();
            SalvarRotaNoHistorico();
            await LimparEstadoRota();
            await Shell.Current.GoToAsync("//HistoricoPage");
        }

        private async Task LimparEstadoRota()
        {
            Ponto ultimoPonto = null;
            if (rota.Count > 0) ultimoPonto = rota[^1];

            rota.Clear();
            tempoDecorrido = TimeSpan.Zero;
            TempoLabel.Text = "00:00:00";
            DistanciaLabel.Text = "0 km";
            PaceLabel.Text = "0:00";
            ultimaDistanciaVibrada = 0;

            temposPorKm.Clear();
            tempoUltimoKm = TimeSpan.Zero;
            kmAtual = 1.0;

            LimparPreferenciasRota();

            if (mapaPronto)
            {
                try
                {
                    await MapWebView.EvaluateJavaScriptAsync("limparRota();");
                    if (ultimoPonto != null)
                        await MapWebView.EvaluateJavaScriptAsync($"atualizarUsuario({ultimoPonto.lat}, {ultimoPonto.lng});");
                }
                catch { }
            }
        }

        private void LimparPreferenciasRota()
        {
            Preferences.Remove("Rota");
            Preferences.Remove("Tempo");
            Preferences.Remove("IsTracking");
            Preferences.Remove("UltimaDistanciaVibrada");
            Preferences.Remove("TemposPorKm");
            Preferences.Remove("KmAtual");
            Preferences.Remove("TempoUltimoKm");
        }

        private async void SalvarRotaNoHistorico()
        {
            try
            {
                double distanciaKm = rota.Count >= 2 ? CalcularDistanciaTotal() / 1000.0 : 0;
                var corrida = new Corrida
                {
                    Data = DateTime.Now,
                    Distancia = $"{distanciaKm:F2} km",
                    Ritmo = PaceLabel.Text,
                    TempoDecorrido = tempoDecorrido.ToString(@"hh\:mm\:ss"),
                    Rota = new List<Ponto>(rota),
                    TemposPorKm = new List<TimeSpan>(temposPorKm)
                };
                await _databaseService.SalvarCorridaAsync(corrida);

                Console.WriteLine("=== APÓS SALVAR ROTA NO HISTÓRICO ===");
                AppSettings.VerificarConfiguracoes();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", $"Não foi possível salvar a rota: {ex.Message}", "OK");
            }
        }

        private double CalcularDistanciaTotal()
        {
            double distancia = 0;
            for (int i = 1; i < rota.Count; i++)
            {
                distancia += Haversine(rota[i - 1], rota[i]);
            }
            return distancia;
        }
        #endregion

        #region Distância e Pace
        private void AtualizarDistanciaEPace()
        {
            double distancia = CalcularDistanciaTotal();
            double distanciaKm = distancia / 1000.0;
            DistanciaLabel.Text = $"{distanciaKm:F2} km";

            if (isTracking && distanciaKm >= kmAtual)
            {
                TimeSpan tempoEsteKm = tempoDecorrido - tempoUltimoKm;
                temposPorKm.Add(tempoEsteKm);
                tempoUltimoKm = tempoDecorrido;
                kmAtual++;

                if (_vibracaoKmAtivada)
                    Vibrar();
            }

            if (distanciaKm >= 0.05 && tempoDecorrido.TotalSeconds > 0)
            {
                double paceSegundosPorKm = tempoDecorrido.TotalSeconds / distanciaKm;
                int paceMin = (int)Math.Floor(paceSegundosPorKm / 60);
                int paceSec = (int)Math.Round(paceSegundosPorKm % 60);
                if (paceSec == 60) { paceSec = 0; paceMin++; }
                PaceLabel.Text = $"{paceMin}:{paceSec:D2}";
            }
            else
            {
                PaceLabel.Text = "0:00";
            }
        }

        private void Vibrar()
        {
            if (!_vibracaoKmAtivada) return;
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500)); }
            catch { }
        }
        #endregion

        #region Utilidades
        private double Haversine(Ponto p1, Ponto p2)
        {
            double R = 6371000;
            double dLat = ToRad(p2.lat - p1.lat);
            double dLon = ToRad(p2.lng - p1.lng);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(p1.lat)) * Math.Cos(ToRad(p2.lat)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRad(double deg) => deg * Math.PI / 180.0;
        #endregion

        #region Estado
        private void SalvarEstado()
        {
            try
            {
                Preferences.Set("Rota", JsonSerializer.Serialize(rota));
                Preferences.Set("Tempo", tempoDecorrido.Ticks);
                Preferences.Set("IsTracking", isTracking);
                Preferences.Set("UltimaDistanciaVibrada", ultimaDistanciaVibrada);

                var temposTicks = new List<long>();
                foreach (var tempo in temposPorKm)
                    temposTicks.Add(tempo.Ticks);
                Preferences.Set("TemposPorKm", JsonSerializer.Serialize(temposTicks));

                Preferences.Set("KmAtual", kmAtual);
                Preferences.Set("TempoUltimoKm", tempoUltimoKm.Ticks);

                _ = SaveStateImmediately();
            }
            catch { }
        }

        private async Task SaveStateImmediately()
        {
            await Task.Run(() => { });
        }

        private void RestaurarEstado()
        {
            if (Preferences.ContainsKey("Rota"))
            {
                var json = Preferences.Get("Rota", "");
                var pontos = JsonSerializer.Deserialize<List<Ponto>>(json);
                if (pontos != null) rota.AddRange(pontos);
            }
            if (Preferences.ContainsKey("Tempo")) tempoDecorrido = TimeSpan.FromTicks(Preferences.Get("Tempo", 0L));
            if (Preferences.ContainsKey("UltimaDistanciaVibrada")) ultimaDistanciaVibrada = Preferences.Get("UltimaDistanciaVibrada", 0.0);

            if (Preferences.ContainsKey("TemposPorKm"))
            {
                var jsonTempos = Preferences.Get("TemposPorKm", "");
                if (!string.IsNullOrEmpty(jsonTempos))
                {
                    var tempos = JsonSerializer.Deserialize<List<long>>(jsonTempos);
                    if (tempos != null)
                    {
                        temposPorKm.Clear();
                        foreach (var ticks in tempos)
                            temposPorKm.Add(TimeSpan.FromTicks(ticks));
                    }
                }
            }

            kmAtual = Preferences.Get("KmAtual", 1.0);
            tempoUltimoKm = TimeSpan.FromTicks(Preferences.Get("TempoUltimoKm", 0L));

            TempoLabel.Text = tempoDecorrido.ToString(@"hh\:mm\:ss");
            AtualizarDistanciaEPace();
            isTracking = false;
            PlayPauseButton.ImageSource = "playy.png";
        }

        private async Task RestaurarEstadoAposRotacao()
        {
            if (Preferences.ContainsKey("Rota"))
            {
                var json = Preferences.Get("Rota", "");
                var pontos = JsonSerializer.Deserialize<List<Ponto>>(json);
                if (pontos != null && pontos.Count > 0)
                {
                    rota.Clear();
                    rota.AddRange(pontos);
                    if (mapaPronto)
                    {
                        try
                        {
                            await MapWebView.EvaluateJavaScriptAsync("limparRota();");
                            string jsAtualizarRota = "if (window.rotaLine) { window.rotaLine.setLatLngs([";
                            for (int i = 0; i < rota.Count; i++)
                            {
                                jsAtualizarRota += $"[{rota[i].lat},{rota[i].lng}]";
                                if (i < rota.Count - 1) jsAtualizarRota += ",";
                            }
                            jsAtualizarRota += "]); }";
                            await MapWebView.EvaluateJavaScriptAsync(jsAtualizarRota);
                            var ultimo = rota[^1];
                            await MapWebView.EvaluateJavaScriptAsync($"atualizarUsuario({ultimo.lat}, {ultimo.lng});");
                        }
                        catch { }
                    }
                }
            }

            if (Preferences.ContainsKey("Tempo")) tempoDecorrido = TimeSpan.FromTicks(Preferences.Get("Tempo", 0L));
            if (Preferences.ContainsKey("UltimaDistanciaVibrada")) ultimaDistanciaVibrada = Preferences.Get("UltimaDistanciaVibrada", 0.0);

            if (Preferences.ContainsKey("TemposPorKm"))
            {
                var jsonTempos = Preferences.Get("TemposPorKm", "");
                if (!string.IsNullOrEmpty(jsonTempos))
                {
                    var tempos = JsonSerializer.Deserialize<List<long>>(jsonTempos);
                    if (tempos != null)
                    {
                        temposPorKm.Clear();
                        foreach (var ticks in tempos)
                            temposPorKm.Add(TimeSpan.FromTicks(ticks));
                    }
                }
            }

            kmAtual = Preferences.Get("KmAtual", 1.0);
            tempoUltimoKm = TimeSpan.FromTicks(Preferences.Get("TempoUltimoKm", 0L));

            bool wasTracking = Preferences.Get("IsTracking", false);
            TempoLabel.Text = tempoDecorrido.ToString(@"hh\:mm\:ss");
            AtualizarDistanciaEPace();

            if (wasTracking && !_trackingGlobalAtivo)
            {
                _deveContinuarTracking = true;
                PlayPauseButton.ImageSource = "pausar.png";
                isTracking = true;
                _trackingGlobalAtivo = true;
                tempoTimer.Start();
                await ReiniciarTrackingAposRotacao();
            }
            else if (!wasTracking)
            {
                isTracking = false;
                PlayPauseButton.ImageSource = "playy.png";
            }
        }

        private async Task ReiniciarTrackingAposRotacao()
        {
            trackingCts = new CancellationTokenSource();
            var token = trackingCts.Token;
            try
            {
                while (isTracking && !token.IsCancellationRequested)
                {
                    var location = await ObterLocalizacaoComAccuracy();
                    if (location != null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await AtualizarMapa(location.Latitude, location.Longitude);
                        });
                    }
                    await Task.Delay(_intervaloColeta, token);
                }
            }
            catch (TaskCanceledException) { }
        }
        #endregion

        #region Eventos de Página
        private void OnPageAppearing(object sender, Page e)
        {
            if (e == this && _estaRestaurandoEstado)
            {
                _estaRestaurandoEstado = false;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await RestaurarEstadoAposRotacao();
                });
            }
        }

        private void OnPageDisappearing(object sender, Page e)
        {
            if (e == this && isTracking)
                SalvarEstado();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            CarregarConfiguracoes();
            if (_deveContinuarTracking)
            {
                _deveContinuarTracking = false;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await RestaurarEstadoAposRotacao();
                });
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (isTracking)
                SalvarEstado();
        }
        #endregion

        #region Estado Sem Sinal
        private void MostrarSemSinal()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!SemSinalView.IsVisible)
                {
                    SemSinalView.Opacity = 0;
                    SemSinalView.IsVisible = true;
                    await SemSinalView.FadeTo(1, 300, Easing.CubicIn);
                }
            });
        }

        private void OcultarSemSinal()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (SemSinalView.IsVisible)
                {
                    await SemSinalView.FadeTo(0, 300, Easing.CubicOut);
                    SemSinalView.IsVisible = false;
                }
            });
        }
        #endregion

        #region Debug
        private void VerificarConfiguracoesAposDeletar()
        {
            Console.WriteLine("=== CONFIGURAÇÕES APÓS DELETAR ROTA ===");
            Console.WriteLine($"FrequenciaColeta: {AppSettings.FrequenciaColeta}");
            Console.WriteLine($"LimiarAccuracy: {AppSettings.LimiarAccuracy}");
            Console.WriteLine($"VibracaoKm: {AppSettings.VibracaoKm}");
            Console.WriteLine($"Intervalo Coleta: {_intervaloColeta}ms");
            Console.WriteLine($"Limiar Accuracy: {_limiarAccuracy}m");
            Console.WriteLine($"Vibração Ativada: {_vibracaoKmAtivada}");
            Console.WriteLine("=======================================");
        }
        #endregion
    }
}
