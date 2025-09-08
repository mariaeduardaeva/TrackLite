using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Globalization;

namespace TrackLite;

public partial class HistoricoPage : ContentPage
{
    // Coleção de corridas no histórico
    public ObservableCollection<Corrida> Corridas { get; set; } = Lixeira.CorridasHistorico;

    // Coleção de corridas agrupadas por data
    public ObservableCollection<CorridaGroup> CorridasAgrupadas { get; set; } = new();

    public HistoricoPage()
    {
        InitializeComponent();

        // Adiciona corridas de exemplo se o histórico estiver vazio
        if (Corridas.Count == 0)
        {
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 26, 18, 30, 0), Distancia = "7 km", Ritmo = "4:58" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 26, 20, 15, 0), Distancia = "5.5 km", Ritmo = "5:03" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 27, 19, 0, 0), Distancia = "7 km", Ritmo = "4:58" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 28, 18, 45, 0), Distancia = "10 km", Ritmo = "5:20" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 28, 7, 30, 0), Distancia = "3 km", Ritmo = "5:10" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 29, 12, 0, 0), Distancia = "8 km", Ritmo = "4:50" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 29, 18, 15, 0), Distancia = "5 km", Ritmo = "5:05" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 30, 6, 45, 0), Distancia = "12 km", Ritmo = "5:30" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 30, 20, 0, 0), Distancia = "4 km", Ritmo = "4:45" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 31, 19, 20, 0), Distancia = "6.5 km", Ritmo = "5:00" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 1, 7, 15, 0), Distancia = "5 km", Ritmo = "4:55" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 1, 18, 50, 0), Distancia = "9 km", Ritmo = "5:12" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 2, 6, 30, 0), Distancia = "10 km", Ritmo = "5:25" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 2, 19, 10, 0), Distancia = "7 km", Ritmo = "4:57" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 3, 12, 45, 0), Distancia = "3.5 km", Ritmo = "5:15" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 3, 20, 5, 0), Distancia = "8 km", Ritmo = "5:00" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 4, 7, 0, 0), Distancia = "6 km", Ritmo = "5:05" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 4, 18, 30, 0), Distancia = "11 km", Ritmo = "5:22" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 5, 6, 50, 0), Distancia = "4 km", Ritmo = "4:50" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 9, 5, 19, 15, 0), Distancia = "7.5 km", Ritmo = "5:08" });

        }

        BindingContext = this;
        OrdenarCorridas();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrdenarCorridas();
    }

    // Ordena as corridas por data e agrupa-as
    public void OrdenarCorridas()
    {
        var ordenadas = Corridas.OrderByDescending(c => c.Data).ToList();
        Corridas.Clear();
        foreach (var c in ordenadas)
            Corridas.Add(c);

        AgruparCorridas();
    }

    private void AgruparCorridas()
    {
        var culturaPT = new CultureInfo("pt-BR");

        var grupos = Corridas
            .OrderByDescending(c => c.Data)
            .GroupBy(c => c.Data.ToString("dd 'de' MMMM", culturaPT))
            .Select(g => new CorridaGroup(g.Key, g));

        CorridasAgrupadas.Clear();
        foreach (var grupo in grupos)
            CorridasAgrupadas.Add(grupo);
    }

    // Comando para quando um item é tocado
    public ICommand ItemTappedCommand => new Command<Corrida>(async (corrida) =>
    {
        if (corrida == null)
            return;

        await Shell.Current.GoToAsync(nameof(DetalhePage), true,
            new Dictionary<string, object>
            {
                { "CorridaSelecionada", corrida }
            });
    });

    // Navega para a página da lixeira
    private async void OnLixoClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new LixoPage());
    }

    // Manipula a ação de enviar uma corrida para a lixeira
    private async void OnSwipeItemInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItemView swipeItemView && swipeItemView.BindingContext is Corrida corrida)
        {
            bool resposta = await DisplayAlert(
                "Confirmação",
                $"Deseja realmente enviar a corrida de {corrida.Data:dd/MM/yyyy HH:mm} para a lixeira?",
                "Sim",
                "Não"
            );

            if (!resposta)
                return;

            // Remove a corrida do histórico e adiciona à lixeira   
            Corridas.Remove(corrida);
            Lixeira.CorridasLixo.Add(corrida);
            OrdenarCorridas();

            // Ordena a lixeira
            var ordenadasLixo = Lixeira.CorridasLixo.OrderByDescending(c => c.Data).ToList();
            Lixeira.CorridasLixo.Clear();
            foreach (var c in ordenadasLixo)
                Lixeira.CorridasLixo.Add(c);
        }
    }
}

// Modelo de dados para uma corrida
public class Corrida
{
    public DateTime Data { get; set; } = DateTime.Now;
    public string Distancia { get; set; } = string.Empty;
    public string Ritmo { get; set; } = string.Empty;
    public string CorFundo { get; set; } = string.Empty;

    public string DataFormatada => Data.ToString("dd/MM/yyyy HH:mm");
}

// Grupo de corridas para agrupamento por data
public class CorridaGroup : ObservableCollection<Corrida>
{
    public string DataChave { get; }

    public CorridaGroup(string dataChave, IEnumerable<Corrida> corridas) : base(corridas)
    {
        DataChave = dataChave;
    }
}

// Classe estática para gerenciar o histórico e a lixeira
public static class Lixeira
{
    public static ObservableCollection<Corrida> CorridasLixo { get; } = new();
    public static ObservableCollection<Corrida> CorridasHistorico { get; } = new();
}
