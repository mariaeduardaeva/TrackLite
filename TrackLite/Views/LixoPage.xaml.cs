using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using System.Windows.Input;
using TrackLite.Models;
using TrackLite.Services;

namespace TrackLite;

public partial class LixoPage : ContentPage
{
    private readonly DatabaseService _databaseService;

    // Corridas na lixeira
    public ObservableCollection<Corrida> CorridasLixo { get; set; } = new ObservableCollection<Corrida>();

    // Corridas agrupadas por data
    public ObservableCollection<CorridaGroup> CorridasLixoAgrupadas { get; set; } = new();

    public LixoPage()
    {
        InitializeComponent();
        _databaseService = new DatabaseService();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarLixeiraAsync();
    }

    private async Task CarregarLixeiraAsync()
    {
        var lixeira = await _databaseService.GetLixeiraAsync();
        CorridasLixo.Clear();
        foreach (var corrida in lixeira)
            CorridasLixo.Add(corrida);

        OrdenarLixo();
    }

    // Ordena as corridas na lixeira por data e agrupa-as
    public void OrdenarLixo()
    {
        var culturaPT = new CultureInfo("pt-BR");

        var ordenadas = CorridasLixo.OrderByDescending(c => c.Data).ToList();
        CorridasLixo.Clear();
        foreach (var c in ordenadas)
            CorridasLixo.Add(c);

        var grupos = CorridasLixo
            .OrderByDescending(c => c.Data)
            .GroupBy(c => c.Data.ToString("dd 'de' MMMM", culturaPT))
            .Select(g => new CorridaGroup(g.Key, g));

        CorridasLixoAgrupadas.Clear();
        foreach (var grupo in grupos)
            CorridasLixoAgrupadas.Add(grupo);
    }

    // Restaura a corrida da lixeira
    private async void OnRestaurarInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItemView swipeItemView && swipeItemView.BindingContext is Corrida corrida)
        {
            bool resposta = await DisplayAlert(
                "Confirmação",
                $"Deseja realmente restaurar a corrida de {corrida.Data:dd/MM/yyyy HH:mm}?",
                "Sim",
                "Não"
            );

            if (!resposta) return;

            await _databaseService.RestaurarCorridaAsync(corrida);

            CorridasLixo.Remove(corrida);
            OrdenarLixo();
        }
    }

    // Exclui permanentemente a corrida da lixeira
    private async void OnExcluirInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItemView swipeItemView && swipeItemView.BindingContext is Corrida corrida)
        {
            bool resposta = await DisplayAlert(
                "Confirmação",
                $"Deseja excluir permanentemente a corrida de {corrida.Data:dd/MM/yyyy HH:mm}?",
                "Sim",
                "Não"
            );

            if (!resposta) return;

            await _databaseService.DeleteCorridaAsync(corrida);

            CorridasLixo.Remove(corrida);
            OrdenarLixo();
        }
    }

    // Comando para quando um item é tocado 
    public ICommand ItemTappedCommand => new Command<Corrida>(async (corrida) =>
    {
        if (corrida == null) return;

        await Shell.Current.GoToAsync(nameof(DetalhePage), true,
            new Dictionary<string, object>
            {
                { "CorridaSelecionada", corrida }
            });
    });
}