using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const int MaxZoom = 22;
        private bool _estaRestaurandoEstado = false;
        private bool _deveContinuarTracking = false;
        private int _intervaloColeta = 1000;
        private double _limiarAccuracy = 20.0;
        private bool _vibracaoKmAtivada = true;
        private Ponto ultimoPontoAdicionado = null;

        private List<TimeSpan> temposPorKm = new List<TimeSpan>();
        private double kmAtual = 1.0;
        private TimeSpan tempoUltimoKm = TimeSpan.Zero;

        private bool _monitorandoGPS = false;
        private CancellationTokenSource _gpsMonitorCts;
        private bool _gpsAtivo = true;
        private bool _primeiraVerificacaoPermissao = true;
        private bool _jaMostrouAlertaPermissao = false;
        private Location _ultimaLocalizacaoConhecida = null;

        public MainPage()
        {
            InitializeComponent();
            CarregarConfiguracoes();
            CarregarMapa();
            InicializarTimer();
            ConfigurarEventos();

            _ = Task.Run(() => IniciarMonitoramentoGPS());
        }

        private void CarregarConfiguracoes()
        {
            try
            {
                _intervaloColeta = AppSettings.FrequenciaColeta * 1000;
                _limiarAccuracy = AppSettings.GetAccuracyInMeters();
                _vibracaoKmAtivada = AppSettings.VibracaoKm;

                if (_limiarAccuracy > 25.0) _limiarAccuracy = 25.0;
                if (_intervaloColeta < 1000) _intervaloColeta = 1000;
            }
            catch (Exception ex)
            {
                _intervaloColeta = 1000;
                _limiarAccuracy = 20.0;
                _vibracaoKmAtivada = true;
            }
        }

        private void InicializarTimer()
        {
            tempoTimer = new System.Timers.Timer(1000);
            tempoTimer.Elapsed += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    tempoDecorrido = tempoDecorrido.Add(TimeSpan.FromSeconds(1));
                    TempoLabel.Text = tempoDecorrido.ToString(@"hh\:mm\:ss");
                    AtualizarDistanciaEPace();
                    SalvarEstado();
                });
            };
        }

        private void ConfigurarEventos()
        {
            App.Current.PageAppearing += OnPageAppearing;
            App.Current.PageDisappearing += OnPageDisappearing;
            Loaded += async (_, __) =>
            {
                if (!_trackingGlobalAtivo)
                    RestaurarEstado();
                await mapaInicializadoTcs.Task;
                await CentralizarNaLocalizacaoAtual();
            };
        }

        private void CarregarMapa()
        {
            try
            {
                var html = @"
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
        .user-icon {
            border: 2px solid #FCFCFC;
            border-radius: 50%;
            width: 18px;
            height: 18px;
            background-color: #16C072;
            animation: pulse 2s ease-in-out infinite;
        }
        @keyframes pulse {
            0% { transform: scale(1); box-shadow: 0 0 0 0 rgba(22, 192, 114, 0.7); }
            50% { transform: scale(1.2); box-shadow: 0 0 10px 5px rgba(22, 192, 114, 0.7); }
            100% { transform: scale(1); box-shadow: 0 0 0 0 rgba(22, 192, 114, 0); }
        }
    </style>
</head>
<body>
    <div id='map'>
        <div class='fallback' id='fallback'>
            <div>
                <h3>TrackLite</h3>
                <p>Rastreamento ativo</p>
                <p><small>Mapa em carregamento...</small></p>
            </div>
        </div>
    </div>

    <script>
        var mapLoaded = false;
        var map, usuarioMarker, rotaLine;
        var zoomInicial = 18;
        var minZoom = 10;
        var maxZoom = 22;
        var usuarioVisivel = false;
        
        function loadLeaflet() {
            if (mapLoaded) return;
            
            var link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
            document.head.appendChild(link);

            var script = document.createElement('script');
            script.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
            script.onload = initializeMap;
            script.onerror = function() {
                showFallback('Mapa offline - Rastreamento ativo');
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
                    minZoom: minZoom,
                    maxZoom: maxZoom,
                    zoomControl: true
                }).setView([0, 0], 2);

                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: maxZoom,
                    minZoom: minZoom
                }).addTo(map);

                rotaLine = L.polyline([], {
                    color: '#214F4B',
                    weight: 4
                }).addTo(map);

                var usuarioIcon = L.divIcon({
                    html: '<div class=""user-icon""></div>',
                    className: '',
                    iconSize: [18, 18],
                    iconAnchor: [9, 9]
                });
                
                usuarioMarker = L.marker([0, 0], {icon: usuarioIcon}).addTo(map);
                usuarioVisivel = true;

                window.atualizarUsuario = function(lat, lng) {
                    try {
                        var latlng = L.latLng(lat, lng);
                        if (usuarioMarker) {
                            usuarioMarker.setLatLng(latlng);
                            if (!usuarioVisivel) {
                                usuarioMarker.addTo(map);
                                usuarioVisivel = true;
                            }
                        }
                        if (rotaLine) rotaLine.addLatLng(latlng);
                    } catch(e) {}
                };

                window.zoomParaUsuario = function(lat, lng) {
                    try {
                        if (map) map.setView(L.latLng(lat, lng), zoomInicial);
                    } catch(e) {}
                };

                window.centralizarComZoom = function(lat, lng, zoom) {
                    try {
                        if (map) map.setView(L.latLng(lat, lng), zoom);
                    } catch(e) {}
                };

                window.limparRota = function() {
                    try {
                        if (rotaLine) rotaLine.setLatLngs([]);
                    } catch(e) {}
                };

                window.manterZoomAtual = function() {
                    try {
                        if (map && usuarioMarker) {
                            var currentLatLng = usuarioMarker.getLatLng();
                            if (currentLatLng) map.setView(currentLatLng, map.getZoom());
                        }
                    } catch(e) {}
                };

                window.mostrarUsuario = function() {
                    try {
                        if (usuarioMarker && !usuarioVisivel) {
                            usuarioMarker.addTo(map);
                            usuarioVisivel = true;
                        }
                    } catch(e) {}
                };

                window.ocultarUsuario = function() {
                    try {
                        if (usuarioMarker && usuarioVisivel) {
                            map.removeLayer(usuarioMarker);
                            usuarioVisivel = false;
                        }
                    } catch(e) {}
                };

                window.estaUsuarioVisivel = function() {
                    return usuarioVisivel;
                };

                mapLoaded = true;
                window.location.href = 'app://map-ready';
                
            } catch(error) {
                showFallback('Erro no mapa - Rastreamento ativo');
                setTimeout(function() {
                    window.location.href = 'app://map-ready';
                }, 100);
            }
        }

        function showFallback(message) {
            var fallback = document.getElementById('fallback');
            if (fallback) {
                fallback.innerHTML = '<div><h3>TrackLite</h3><p>' + message + '</p></div>';
                fallback.style.display = 'flex';
            }
        }

        setTimeout(loadLeaflet, 50);

    </script>
</body>
</html>";

                MapWebView.Source = new HtmlWebViewSource { Html = html };

                MapWebView.Navigating += (s, e) =>
                {
                    if (e.Url?.Contains("map-ready") == true)
                    {
                        e.Cancel = true;
                        mapaPronto = true;
                        mapaInicializadoTcs.TrySetResult(true);
                        _ = GarantirUsuarioVisivel();
                    }
                };

            }
            catch (Exception ex)
            {
                mapaPronto = true;
                mapaInicializadoTcs.TrySetResult(true);
            }
        }

        private async Task GarantirUsuarioVisivel()
        {
            if (!mapaPronto) return;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await Task.Delay(500);
                    string jsMostrarUsuario = $"if (window.mostrarUsuario) window.mostrarUsuario();";
                    await MapWebView.EvaluateJavaScriptAsync(jsMostrarUsuario);
                    break;
                }
                catch (Exception ex)
                {
                }
            }
        }

        private async Task CentralizarNaLocalizacaoAtual()
        {
            if (!mapaPronto) return;

            if (!await VerificarPermissaoLocalizacao()) return;

            var location = await ObterLocalizacaoRapida();
            if (location != null)
            {
                await AtualizarPosicaoUsuarioNoMapa(location);
                await CentralizarNoUsuario(location);
            }
            else
            {
                await GarantirUsuarioVisivel();
            }
        }

        private async Task AtualizarPosicaoUsuarioNoMapa(Location location)
        {
            if (!mapaPronto || location == null) return;

            try
            {
                await GarantirUsuarioVisivel();

                string jsAtualizar = $"if (window.atualizarUsuario) window.atualizarUsuario({location.Latitude.ToString().Replace(',', '.')}, {location.Longitude.ToString().Replace(',', '.')});";
                await MapWebView.EvaluateJavaScriptAsync(jsAtualizar);
            }
            catch (Exception ex)
            {
            }
        }

        private async Task CentralizarNoUsuario(Location location)
        {
            if (!mapaPronto || location == null) return;

            try
            {
                await GarantirUsuarioVisivel();

                string jsZoom = $"if (window.centralizarComZoom) window.centralizarComZoom({location.Latitude.ToString().Replace(',', '.')}, {location.Longitude.ToString().Replace(',', '.')}, {ZoomInicial});";
                await MapWebView.EvaluateJavaScriptAsync(jsZoom);
            }
            catch (Exception ex)
            {
            }
        }

        private async Task AtualizarMapaQuandoGPSVolta()
        {
            if (!_gpsAtivo || !mapaPronto) return;

            try
            {
                var location = await ObterLocalizacaoRapida();
                if (location != null)
                {
                    await AtualizarPosicaoUsuarioNoMapa(location);
                    await CentralizarNoUsuario(location);
                }
                else if (_ultimaLocalizacaoConhecida != null)
                {
                    await AtualizarPosicaoUsuarioNoMapa(_ultimaLocalizacaoConhecida);
                    await CentralizarNoUsuario(_ultimaLocalizacaoConhecida);
                }
            }
            catch (Exception ex)
            {
            }
        }

        private async Task AtualizarMapa(double lat, double lng, double accuracy)
        {
            if (accuracy > _limiarAccuracy)
            {
                return;
            }

            var novoPonto = new Ponto
            {
                lat = lat,
                lng = lng,
                accuracy = accuracy,
                timestamp = DateTime.Now
            };

            if (rota.Count > 0)
            {
                var ultimo = rota[^1];
                double distancia = Haversine(ultimo, novoPonto);
                double tempoSegundos = (novoPonto.timestamp - ultimo.timestamp).TotalSeconds;

                if (tempoSegundos > 0)
                {
                    double velocidade = distancia / tempoSegundos;
                    const double velocidadeMaxima = 5.0;
                    if (velocidade > velocidadeMaxima)
                    {
                        return;
                    }
                }

                if (distancia < 1.0)
                {
                    return;
                }
            }

            rota.Add(novoPonto);
            ultimoPontoAdicionado = novoPonto;

            if (mapaPronto)
            {
                try
                {
                    await GarantirUsuarioVisivel();

                    string js = $@"if (window.atualizarUsuario) window.atualizarUsuario({lat.ToString().Replace(',', '.')}, {lng.ToString().Replace(',', '.')});";
                    await MapWebView.EvaluateJavaScriptAsync(js);
                }
                catch (Exception ex)
                {
                }
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AtualizarDistanciaEPace();
            });
        }

        private async Task<Location> ObterLocalizacaoRapida()
        {
            try
            {
                if (!await VerificarPermissaoLocalizacao())
                {
                    MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                    return null;
                }

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location == null)
                {
                    var requestLow = new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5));
                    location = await Geolocation.Default.GetLocationAsync(requestLow);

                    if (location == null)
                    {
                        MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                        return null;
                    }
                }

                if (Math.Abs(location.Latitude) < 0.0001 || Math.Abs(location.Longitude) < 0.0001)
                {
                    MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                    return null;
                }

                if (Math.Abs(location.Latitude) < 1.0 && Math.Abs(location.Longitude) < 1.0)
                {
                    MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                    return null;
                }

                MainThread.BeginInvokeOnMainThread(OcultarSemSinal);
                _gpsAtivo = true;
                _ultimaLocalizacaoConhecida = location;

                return location;

            }
            catch (PermissionException pex)
            {
                MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                _gpsAtivo = false;

                if (!_jaMostrouAlertaPermissao)
                {
                    _jaMostrouAlertaPermissao = true;
                    MainThread.BeginInvokeOnMainThread(MostrarAlertaPermissaoNegada);
                }
                return null;
            }
            catch (FeatureNotEnabledException fex)
            {
                MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                _gpsAtivo = false;
                return null;
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                _gpsAtivo = false;
                return null;
            }
        }

        private async Task<bool> VerificarPermissaoLocalizacao()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

                if (status != PermissionStatus.Granted)
                {
                    if (_primeiraVerificacaoPermissao)
                    {
                        _primeiraVerificacaoPermissao = false;
                        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                        if (status != PermissionStatus.Granted && !_jaMostrouAlertaPermissao)
                        {
                            _jaMostrouAlertaPermissao = true;
                            MainThread.BeginInvokeOnMainThread(MostrarAlertaPermissaoNegada);
                        }
                    }

                    if (status != PermissionStatus.Granted)
                    {
                        MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                        _gpsAtivo = false;
                        return false;
                    }
                }

                try
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(2));
                    var location = await Geolocation.Default.GetLocationAsync(request);
                    _gpsAtivo = (location != null);

                    if (_gpsAtivo)
                    {
                        _ultimaLocalizacaoConhecida = location;
                    }
                }
                catch
                {
                    _gpsAtivo = false;
                }

                if (!_gpsAtivo)
                {
                    MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                    return false;
                }

                MainThread.BeginInvokeOnMainThread(OcultarSemSinal);
                return true;
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                _gpsAtivo = false;
                return false;
            }
        }

        private async void MostrarAlertaPermissaoNegada()
        {
            var result = await DisplayAlert(
                "Permissão de Localização Necessária",
                "O TrackLite precisa da permissão de localização para funcionar corretamente.\n\n" +
                "Deseja abrir as configurações para ativar a permissão de localização?",
                "Sim",
                "Não"
            );

            if (result)
            {
                try
                {
                    AppInfo.Current.ShowSettingsUI();
                }
                catch (Exception ex)
                {
                    await DisplayAlert(
                        "Abrir Configurações",
                        "Por favor, vá em:\n\n" +
                        "Configurações > Aplicativos > TrackLite > Permissões > Localização\n\n" +
                        "e ative a permissão de localização.",
                        "OK"
                    );
                }
            }
        }

        private async void LocalButton_Clicked(object sender, EventArgs e)
        {
            if (!mapaPronto) return;

            if (!await VerificarPermissaoLocalizacao()) return;

            var location = await ObterLocalizacaoRapida();
            if (location != null)
            {
                await AtualizarPosicaoUsuarioNoMapa(location);
                await CentralizarNoUsuario(location);
            }
        }

        private async Task StartTracking()
        {
            if (isTracking) return;

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                MainThread.BeginInvokeOnMainThread(MostrarSemSinal);
                return;
            }

            isTracking = true;
            PlayPauseButton.ImageSource = "pausar.png";
            tempoTimer.Start();
            _trackingGlobalAtivo = true;
            DeviceDisplay.KeepScreenOn = true;
            ultimaDistanciaVibrada = 0;

            if (mapaPronto && rota.Count == 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await MapWebView.EvaluateJavaScriptAsync("if (window.limparRota) window.limparRota();");
                        await GarantirUsuarioVisivel();
                    }
                    catch { }
                });
            }

            var locationInicial = await ObterLocalizacaoRapida();
            if (locationInicial != null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await AtualizarMapa(locationInicial.Latitude, locationInicial.Longitude, locationInicial.Accuracy ?? _limiarAccuracy);

                    if (mapaPronto)
                    {
                        await CentralizarNoUsuario(locationInicial);
                    }
                });
            }

            _ = Task.Run(() => IniciarTrackingLocalizacao());
        }

        private async Task IniciarTrackingLocalizacao()
        {
            trackingCts = new CancellationTokenSource();
            var token = trackingCts.Token;

            Ponto ultimoPontoValido = ultimoPontoAdicionado;
            int tentativasSemLocalizacao = 0;
            const int maxTentativas = 10;

            try
            {
                while (!token.IsCancellationRequested && isTracking)
                {
                    var location = await ObterLocalizacaoRapida();

                    if (location != null && location.Accuracy.HasValue)
                    {
                        tentativasSemLocalizacao = 0;

                        double accuracy = location.Accuracy.Value;
                        var pontoCandidato = new Ponto
                        {
                            lat = location.Latitude,
                            lng = location.Longitude,
                            accuracy = accuracy,
                            timestamp = DateTime.Now
                        };

                        bool pontoValido = accuracy <= _limiarAccuracy || accuracy <= 50.0;

                        if (pontoValido)
                        {
                            if (ultimoPontoValido != null)
                            {
                                double distancia = Haversine(ultimoPontoValido, pontoCandidato);
                                if (distancia < 1.0)
                                {
                                }
                            }

                            ultimoPontoValido = pontoCandidato;

                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await AtualizarMapa(pontoCandidato.lat, pontoCandidato.lng, pontoCandidato.accuracy);
                            });
                        }
                        else
                        {
                            ultimoPontoValido = pontoCandidato;
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await AtualizarMapa(pontoCandidato.lat, pontoCandidato.lng, pontoCandidato.accuracy);
                            });
                        }
                    }
                    else
                    {
                        tentativasSemLocalizacao++;

                        if (tentativasSemLocalizacao >= maxTentativas)
                        {
                            tentativasSemLocalizacao = maxTentativas - 1;
                        }
                    }

                    await Task.Delay(_intervaloColeta, token);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    DisplayAlert("Erro", "Erro no rastreamento: " + ex.Message, "OK");
                });
                StopTracking();
            }
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
            if (!isTracking)
            {
                PlayPauseButton.ImageSource = "pausar.png";
                await StartTracking();
            }
            else
            {
                StopTracking();
            }
        }

        private async void DeletarRota_Clicked(object sender, EventArgs e)
        {
            StopTracking();
            await LimparEstadoRota();
            CarregarConfiguracoes();
            await ManterZoomAtual();
        }

        private async void SaveButton_Clicked(object sender, EventArgs e)
        {
            StopTracking();
            await SalvarRotaNoHistorico();
            await LimparEstadoRota();
            await Shell.Current.GoToAsync("//HistoricoPage");
        }

        private async Task LimparEstadoRota()
        {
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
                    await MapWebView.EvaluateJavaScriptAsync("if (window.limparRota) window.limparRota();");
                    await GarantirUsuarioVisivel();
                }
                catch { }
            }
        }

        private async Task ManterZoomAtual()
        {
            if (!mapaPronto) return;
            try
            {
                string js = "if (window.manterZoomAtual) window.manterZoomAtual();";
                await MapWebView.EvaluateJavaScriptAsync(js);
            }
            catch { }
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

        private async Task SalvarRotaNoHistorico()
        {
            try
            {
                double distanciaKm = rota.Count >= 2 ? CalcularDistanciaTotal() / 1000.0 : 0;

                var corrida = new Corrida
                {
                    Data = DateTime.Now,
                    StartTime = DateTime.Now,
                    Distancia = $"{distanciaKm:F2} km",
                    Ritmo = PaceLabel.Text,
                    TempoDecorrido = tempoDecorrido.ToString(@"hh\:mm\:ss"),
                    Rota = new List<Ponto>(rota),
                    TemposPorKm = new List<TimeSpan>(temposPorKm),
                    Title = $"Corrida {DateTime.Now:dd/MM/yyyy HH:mm}",
                    Description = $"Distância: {distanciaKm:F2} km"
                };

                var activityPoints = new List<ActivityPoint>();
                for (int i = 0; i < rota.Count; i++)
                {
                    activityPoints.Add(new ActivityPoint
                    {
                        Latitude = rota[i].lat,
                        Longitude = rota[i].lng,
                        Accuracy = rota[i].accuracy,
                        Timestamp = rota[i].timestamp,
                        Sequence = i
                    });
                }

                await _databaseService.SalvarCorridaComPontosAsync(corrida, activityPoints);

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

        private void AtualizarDistanciaEPace()
        {
            try
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

                if (distanciaKm >= 0.01 && tempoDecorrido.TotalSeconds > 0)
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
            catch (Exception ex)
            {
            }
        }

        private void Vibrar()
        {
            if (!_vibracaoKmAtivada) return;
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(500)); }
            catch { }
        }

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
            }
            catch { }
        }

        private void RestaurarEstado()
        {
            try
            {
                if (Preferences.ContainsKey("Rota"))
                {
                    var json = Preferences.Get("Rota", "");
                    var pontos = JsonSerializer.Deserialize<List<Ponto>>(json);
                    if (pontos != null && pontos.Count > 0)
                    {
                        rota.Clear();
                        rota.AddRange(pontos);
                        ultimoPontoAdicionado = rota[^1];

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            AtualizarDistanciaEPace();
                        });
                    }
                }

                if (Preferences.ContainsKey("Tempo"))
                    tempoDecorrido = TimeSpan.FromTicks(Preferences.Get("Tempo", 0L));

                if (Preferences.ContainsKey("UltimaDistanciaVibrada"))
                    ultimaDistanciaVibrada = Preferences.Get("UltimaDistanciaVibrada", 0.0);

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
            }
            catch (Exception ex)
            {
            }

            isTracking = false;
            PlayPauseButton.ImageSource = "playy.png";
        }

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

            if (rota.Count > 0)
            {
                AtualizarDistanciaEPace();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (isTracking)
                SalvarEstado();

            PararMonitoramentoGPS();
        }

        private async Task RestaurarEstadoAposRotacao()
        {
            RestaurarEstado();
        }

        private void MostrarSemSinal()
        {
            if (!SemSinalView.IsVisible)
            {
                SemSinalView.Opacity = 0;
                SemSinalView.IsVisible = true;
                SemSinalView.FadeTo(1, 200);
            }
        }

        private void OcultarSemSinal()
        {
            if (SemSinalView.IsVisible)
            {
                SemSinalView.FadeTo(0, 200);
                SemSinalView.IsVisible = false;
            }
        }

        private async Task IniciarMonitoramentoGPS()
        {
            if (_monitorandoGPS) return;

            _monitorandoGPS = true;
            _gpsMonitorCts = new CancellationTokenSource();

            bool gpsStatusAnterior = _gpsAtivo;

            try
            {
                while (!_gpsMonitorCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(3000);

                    var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

                    bool gpsAtivoAnterior = _gpsAtivo;
                    try
                    {
                        var request = new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(3));
                        var location = await Geolocation.Default.GetLocationAsync(request);
                        _gpsAtivo = (location != null);

                        if (_gpsAtivo && location != null)
                        {
                            _ultimaLocalizacaoConhecida = location;
                        }
                    }
                    catch
                    {
                        _gpsAtivo = false;
                    }

                    bool gpsVoltou = !gpsAtivoAnterior && _gpsAtivo;

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        if (status != PermissionStatus.Granted || !_gpsAtivo)
                        {
                            MostrarSemSinal();
                        }
                        else
                        {
                            OcultarSemSinal();

                            if (gpsVoltou)
                            {
                                await AtualizarMapaQuandoGPSVolta();
                            }
                        }
                    });

                    gpsStatusAnterior = _gpsAtivo;
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
        }

        private void PararMonitoramentoGPS()
        {
            _monitorandoGPS = false;
            _gpsMonitorCts?.Cancel();
        }

        private async void DebugButton_Clicked(object sender, EventArgs e)
        {
            string action = await DisplayActionSheet("Debug", "Cancelar", null,
                "Testar GPS Rápido", "Modo Preciso", "Modo Balanceado", "Modo Sensível");

            switch (action)
            {
                case "Testar GPS Rápido":
                    await TestarGPSRapido();
                    return;
                case "Modo Preciso":
                    _limiarAccuracy = 15.0;
                    _intervaloColeta = 2000;
                    break;
                case "Modo Balanceado":
                    _limiarAccuracy = 20.0;
                    _intervaloColeta = 1500;
                    break;
                case "Modo Sensível":
                    _limiarAccuracy = 25.0;
                    _intervaloColeta = 1000;
                    break;
            }

            await DisplayAlert("Debug", $"Modo: {action}\nAccuracy: {_limiarAccuracy}m\nIntervalo: {_intervaloColeta}ms", "OK");
        }

        private async Task TestarGPSRapido()
        {
            if (!await VerificarPermissaoLocalizacao())
            {
                await DisplayAlert("Teste GPS", "Permissão negada.", "OK");
                return;
            }

            var location = await ObterLocalizacaoRapida();
            if (location != null)
            {
                await DisplayAlert("Teste GPS",
                    $"GPS OK!\n\n" +
                    $"Lat: {location.Latitude:F6}\n" +
                    $"Lng: {location.Longitude:F6}\n" +
                    $"Accuracy: {location.Accuracy:F1}m", "OK");
            }
            else
            {
                await DisplayAlert("Teste GPS", "GPS falhou.", "OK");
            }
        }

        private void VerificarConfiguracoesAposDeletar()
        {
        }
    }
}
