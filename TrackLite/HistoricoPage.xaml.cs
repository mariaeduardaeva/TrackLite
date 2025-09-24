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

    public string TempoDecorrido { get; set; } = "00:00:00";

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