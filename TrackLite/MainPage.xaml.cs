using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TrackLite
{
    public partial class MainPage : ContentPage
    {
        private List<(double lat, double lng)> rota = new();
        private List<(double lat, double lng)> rotaBuffer = new();
        private bool isTracking = false;
        private System.Timers.Timer tempoTimer;
        private TimeSpan tempoDecorrido = TimeSpan.Zero;
        private double ultimaDistanciaVibrada = 0.0;
        private CancellationTokenSource trackingCts;
        private bool mapaPronto = false;
        private TaskCompletionSource<bool> mapaInicializadoTcs = new();

        private const int ZoomInicial = 18; // zoom inicial ao abrir o mapa
        private const int MinZoom = 10;     // limite mínimo de zoom
        private const int MaxZoom = 18;     // limite máximo de zoom

        public MainPage()
        {
            InitializeComponent();
            CarregarMapa();

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

            Loaded += async (_, __) =>
            {
                RestaurarEstado();
                await mapaInicializadoTcs.Task;
                await CentralizarUsuario();
            };
        }

        #region WebView Map
        private void CarregarMapa()
        {
            var html = $@"
<!DOCTYPE html>
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
    center: [0,0],
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

window.usuarioMarker = L.marker([0,0], {{icon: usuarioIcon}}).addTo(window.map);

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
                    Debug.WriteLine("Mapa sinalizou pronto!");
                }
            };
        }

        private async Task AtualizarMapa(double lat, double lng)
        {
            if (!mapaPronto) return;

            rota.Add((lat, lng));

            string js = $"atualizarUsuario({lat}, {lng});";
            await MapWebView.EvaluateJavaScriptAsync(js);
        }

        private async Task CentralizarUsuario()
        {
            if (!mapaPronto)
                await mapaInicializadoTcs.Task;

            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Permissão", "Localização não permitida.", "OK");
                    return;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);
                if (location != null)
                {
                    string js = $"centralizar({location.Latitude}, {location.Longitude});";
                    await MapWebView.EvaluateJavaScriptAsync(js);
                    await AtualizarMapa(location.Latitude, location.Longitude);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro ao centralizar usuário: " + ex);
            }
        }
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
            }
            catch (Exception ex) { Debug.WriteLine("Erro ao salvar estado: " + ex); }
        }

        private void RestaurarEstado()
        {
            try
            {
                if (Preferences.ContainsKey("Rota"))
                {
                    var json = Preferences.Get("Rota", "");
                    var pontos = JsonSerializer.Deserialize<List<(double lat, double lng)>>(json);
                    if (pontos != null)
                        rota.AddRange(pontos);

                    foreach (var p in rota)
                        _ = AtualizarMapa(p.lat, p.lng);
                }

                tempoDecorrido = TimeSpan.Zero;
                TempoLabel.Text = "00:00:00";
                isTracking = false;
                ultimaDistanciaVibrada = 0;
                AtualizarDistanciaEPace();
            }
            catch (Exception ex) { Debug.WriteLine("Erro ao restaurar estado: " + ex); }
        }
        #endregion

        #region Tracking
        private async Task StartTracking()
        {
            rota.Clear();
            rotaBuffer.Clear();
            tempoDecorrido = TimeSpan.Zero;
            DistanciaLabel.Text = "0 km";
            PaceLabel.Text = "0:00";
            ultimaDistanciaVibrada = 0;

            if (mapaPronto)
            {
                try { await MapWebView.EvaluateJavaScriptAsync("limparRota();"); }
                catch (Exception ex) { Debug.WriteLine("Erro ao limpar rota antes do tracking: " + ex); }
            }

            isTracking = true;
            PlayPauseButton.ImageSource = "pausar.png";
            tempoTimer.Start();
            trackingCts = new CancellationTokenSource();
            var token = trackingCts.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(2));
                    var location = await Geolocation.Default.GetLocationAsync(request);
                    if (location != null)
                        await AtualizarMapa(location.Latitude, location.Longitude);

                    await Task.Delay(2000, token);
                }
            }
            catch (TaskCanceledException) { }
        }

        private void StopTracking()
        {
            if (!isTracking) return;
            trackingCts?.Cancel();
            isTracking = false;
            PlayPauseButton.ImageSource = "playy.png";
            tempoTimer.Stop();
            SalvarEstado();
        }

        private async void PlayPauseButton_Clicked(object sender, EventArgs e)
        {
            if (!isTracking) await StartTracking();
            else StopTracking();
        }
        #endregion

        #region Botões
        private async void DeletarRota_Clicked(object sender, EventArgs e)
        {
            StopTracking();
            rota.Clear();
            rotaBuffer.Clear();
            tempoDecorrido = TimeSpan.Zero;
            TempoLabel.Text = "00:00:00";
            DistanciaLabel.Text = "0 km";
            PaceLabel.Text = "0:00";
            ultimaDistanciaVibrada = 0;
            Preferences.Clear();

            if (mapaPronto)
            {
                try
                {
                    await MapWebView.EvaluateJavaScriptAsync("limparRota();");
                    await MapWebView.EvaluateJavaScriptAsync("usuarioMarker.setLatLng([0,0]);");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Erro ao limpar rota no mapa: " + ex);
                }
            }

            await CentralizarUsuario();
        }

        private async void SaveButton_Clicked(object sender, EventArgs e)
        {
            StopTracking();
            SalvarRotaNoHistorico();
            await Shell.Current.GoToAsync("//HistoricoPage");
        }

        private void SalvarRotaNoHistorico()
        {
            double distanciaKm = rota.Count >= 2 ? CalcularDistanciaTotal() / 1000.0 : 0;
            var corrida = new Corrida
            {
                Data = DateTime.Now,
                Distancia = $"{distanciaKm:F2} km",
                Ritmo = PaceLabel.Text,
                TempoDecorrido = tempoDecorrido.ToString(@"hh\:mm\:ss")
            };
            Lixeira.CorridasHistorico.Add(corrida);
        }

        private double CalcularDistanciaTotal()
        {
            double distancia = 0;
            for (int i = 1; i < rota.Count; i++)
                distancia += Haversine(rota[i - 1], rota[i]);
            return distancia;
        }

        private async void LocalButton_Clicked(object sender, EventArgs e)
        {
            if (!mapaPronto) return;
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);
                if (location != null)
                {
                    string js = $"window.map.setView([{location.Latitude}, {location.Longitude}], {ZoomInicial}, {{animate: true, duration: 1}});";
                    await MapWebView.EvaluateJavaScriptAsync(js);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro no LocalButton: " + ex);
            }
        }
        #endregion

        #region Pace e Distância
        private void AtualizarDistanciaEPace()
        {
            double distancia = 0;
            for (int i = 1; i < rota.Count; i++)
                distancia += Haversine(rota[i - 1], rota[i]);

            double distanciaKm = distancia / 1000.0;
            DistanciaLabel.Text = $"{distanciaKm:F2} km";

            if (distanciaKm - ultimaDistanciaVibrada >= 1.0)
            {
                ultimaDistanciaVibrada = Math.Floor(distanciaKm);
                Vibrar();
            }

            if (distanciaKm >= 0.2 && tempoDecorrido.TotalSeconds > 0)
            {
                double paceSegundosPorKm = tempoDecorrido.TotalSeconds / distanciaKm;
                int paceMin = (int)Math.Floor(paceSegundosPorKm / 60);
                int paceSec = (int)Math.Round(paceSegundosPorKm % 60);
                if (paceSec == 60) { paceSec = 0; paceMin++; }
                PaceLabel.Text = $"{paceMin}:{paceSec:D2}";
            }
            else
                PaceLabel.Text = "0:00";
        }

        private void Vibrar()
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500)); }
            catch (Exception ex) { Debug.WriteLine("Erro ao vibrar: " + ex); }
        }

        private double Haversine((double lat, double lng) p1, (double lat, double lng) p2)
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
    }
}