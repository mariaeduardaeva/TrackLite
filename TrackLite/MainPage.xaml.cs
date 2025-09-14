using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Microsoft.Maui.ApplicationModel;

namespace TrackLite
{
    public partial class MainPage : ContentPage
    {
        private const double MinZoomLevel = 1;
        private const double MaxZoomLevel = 20;

        public MainPage()
        {
            InitializeComponent();

            mapView.Map ??= new Mapsui.Map();
            mapView.Map.Layers.Add(OpenStreetMap.CreateTileLayer());

            Loaded += async (_, __) => await CenterOnUserLocation();

            mapView.Map.Navigator.ViewportChanged += (_, __) =>
            {
                var zoom = mapView.Map.Navigator.Viewport.Resolution;

                if (zoom < MinZoomLevel)
                    mapView.Map.Navigator.ZoomTo(MinZoomLevel);

                if (zoom > MaxZoomLevel)
                    mapView.Map.Navigator.ZoomTo(MaxZoomLevel);
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
                    var sphericalMercator = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                    var userPoint = new MPoint(sphericalMercator.x, sphericalMercator.y);

                    mapView.Map.Navigator.CenterOn(userPoint);

                    mapView.Map.Navigator.ZoomTo(15);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Localização", $"Não foi possível obter a localização: {ex.Message}", "OK");
            }
        }
    }
}