using Mapsui.Tiling;
using Mapsui.UI.Maui;

namespace TrackLite
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            mapView.Map ??= new Mapsui.Map();
            mapView.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
        }
    }
}