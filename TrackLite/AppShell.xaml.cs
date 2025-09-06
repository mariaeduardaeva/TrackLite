using Microsoft.Maui.Controls;

namespace TrackLite
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Registra a rota para navegação até a página de detalhe
            Routing.RegisterRoute(nameof(DetalhePage), typeof(DetalhePage));
        }
    }
}
