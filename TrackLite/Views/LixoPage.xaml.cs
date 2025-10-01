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

    public ObservableCollection<Corrida> CorridasLixo { get; set; } = new ObservableCollection<Corrida>();

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

        await _databaseService.RemoverLixeiraExpiradaAsync();

        var lixeira = await _databaseService.GetLixeiraAsync();
        CorridasLixo.Clear();
        foreach (var corrida in lixeira)
            CorridasLixo.Add(corrida);

        OrdenarLixo();
    }

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

    private async void OnRestaurarInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItemView swipeItemView && swipeItemView.BindingContext is Corrida corrida)
        {
            bool resposta = await DisplayAlert(
                "Confirma��o",
                $"Deseja realmente restaurar a corrida de {corrida.Data:dd/MM/yyyy HH:mm}?",
                "Sim",
                "N�o"
            );

            if (!resposta) return;

            await _databaseService.RestaurarCorridaAsync(corrida);

            CorridasLixo.Remove(corrida);
            OrdenarLixo();
        }
    }

    private async void OnExcluirInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItemView swipeItemView && swipeItemView.BindingContext is Corrida corrida)
        {
            bool resposta = await DisplayAlert(
                "Confirma��o",
                $"Deseja excluir permanentemente a corrida de {corrida.Data:dd/MM/yyyy HH:mm}?",
                "Sim",
                "N�o"
            );

            if (!resposta) return;

            await _databaseService.DeleteCorridaAsync(corrida);

            CorridasLixo.Remove(corrida);
            OrdenarLixo();
        }
    }

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