using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Globalization;
using TrackLite.Models;
using TrackLite.Services;

namespace TrackLite;

public partial class HistoricoPage : ContentPage
{
    private readonly DatabaseService _databaseService;

    // Coleção de corridas no histórico
    public ObservableCollection<Corrida> Corridas { get; set; } = new ObservableCollection<Corrida>();

    // Coleção de corridas agrupadas por data
    public ObservableCollection<CorridaGroup> CorridasAgrupadas { get; set; } = new();

    public HistoricoPage()
    {
        InitializeComponent();

        _databaseService = new DatabaseService();

        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarHistoricoAsync();
    }

    private async Task CarregarHistoricoAsync()
    {
        var historico = await _databaseService.GetHistoricoAsync();
        Corridas.Clear();
        foreach (var corrida in historico)
            Corridas.Add(corrida);

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

            await _databaseService.MoverParaLixeiraAsync(corrida);

            Corridas.Remove(corrida);

            OrdenarCorridas();
        }
    }
}
