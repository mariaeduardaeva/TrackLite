using System.Collections.ObjectModel;
using System.Windows.Input;

namespace TrackLite;

public partial class HistoricoPage : ContentPage
{
    public ObservableCollection<Corrida> Corridas { get; set; }

    public HistoricoPage()
    {
        InitializeComponent();

        Corridas = new ObservableCollection<Corrida>
        {
            new Corrida { Data = "26 de agosto", Distancia = "5.5 km", Ritmo = "5:03 / km", CorFundo = "#214F4B" },
            new Corrida { Data = "27 de agosto", Distancia = "7 km", Ritmo = "4:58 / km", CorFundo = "#303681" },
            new Corrida { Data = "28 de agosto", Distancia = "10 km", Ritmo = "5:20 / km", CorFundo = "#214F4B" },
            new Corrida { Data = "27 de agosto", Distancia = "7 km", Ritmo = "4:58 / km", CorFundo = "#303681" },
            new Corrida { Data = "28 de agosto", Distancia = "10 km", Ritmo = "5:20 / km", CorFundo = "#214F4B" },
            new Corrida { Data = "27 de agosto", Distancia = "7 km", Ritmo = "4:58 / km", CorFundo = "#303681" },
            new Corrida { Data = "28 de agosto", Distancia = "10 km", Ritmo = "5:20 / km", CorFundo = "#214F4B" },
            new Corrida { Data = "27 de agosto", Distancia = "7 km", Ritmo = "4:58 / km", CorFundo = "#303681" },
            new Corrida { Data = "28 de agosto", Distancia = "10 km", Ritmo = "5:20 / km", CorFundo = "#214F4B" },
            new Corrida { Data = "27 de agosto", Distancia = "7 km", Ritmo = "4:58 / km", CorFundo = "#303681" },
            new Corrida { Data = "28 de agosto", Distancia = "10 km", Ritmo = "5:20 / km", CorFundo = "#214F4B" },
        };

        BindingContext = this;
    }

    // Comando chamado pelo TapGestureRecognizer
    public ICommand ItemTappedCommand => new Command<Corrida>(async (corrida) =>
    {
        if (corrida == null)
            return;

        // Navegação no Shell passando o objeto como parâmetro
        await Shell.Current.GoToAsync(nameof(DetalhePage), true,
            new Dictionary<string, object>
            {
                { "CorridaSelecionada", corrida }
            });
    });
}

public class Corrida
{
    public string Data { get; set; } = string.Empty;
    public string Distancia { get; set; } = string.Empty;
    public string Ritmo { get; set; } = string.Empty;
    public string CorFundo { get; set; } = string.Empty;
}
