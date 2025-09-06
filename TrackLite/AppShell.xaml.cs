using Microsoft.Maui.Controls;

namespace TrackLite
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(DetalhePage), typeof(DetalhePage));
        }
    }
}
