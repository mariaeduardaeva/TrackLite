using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;

namespace TrackLite;

public partial class HistoricoPage : ContentPage
{
    public ObservableCollection<Corrida> Corridas { get; set; } = Lixeira.CorridasHistorico;

    public HistoricoPage()
    {
        InitializeComponent();

        if (Corridas.Count == 0)
        {
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 26, 18, 30, 0), Distancia = "7 km", Ritmo = "4:58 / km", CorFundo = "#303681" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 26, 20, 15, 0), Distancia = "5.5 km", Ritmo = "5:03 / km", CorFundo = "#214F4B" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 26, 19, 0, 0), Distancia = "7 km", Ritmo = "4:58 / km", CorFundo = "#303681" });
            Corridas.Add(new Corrida { Data = new DateTime(2025, 8, 26, 18, 45, 0), Distancia = "10 km", Ritmo = "5:20 / km", CorFundo = "#214F4B" });
        }

        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrdenarCorridas();
    }

    public void OrdenarCorridas()
    {
        var ordenadas = Corridas.OrderByDescending(c => c.Data).ToList();
        Corridas.Clear();
        foreach (var c in ordenadas)
            Corridas.Add(c);
    }

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

            Corridas.Remove(corrida);
            Lixeira.CorridasLixo.Add(corrida);

            OrdenarCorridas();

            var ordenadasLixo = Lixeira.CorridasLixo.OrderByDescending(c => c.Data).ToList();
            Lixeira.CorridasLixo.Clear();
            foreach (var c in ordenadasLixo)
                Lixeira.CorridasLixo.Add(c);
        }
    }
}

public class Corrida
{
    public DateTime Data { get; set; } = DateTime.Now;
    public string Distancia { get; set; } = string.Empty;
    public string Ritmo { get; set; } = string.Empty;
    public string CorFundo { get; set; } = string.Empty;

    public string DataFormatada => Data.ToString("dd/MM/yyyy HH:mm");
}

public static class Lixeira
{
    public static ObservableCollection<Corrida> CorridasLixo { get; } = new();
    public static ObservableCollection<Corrida> CorridasHistorico { get; } = new();
}
