using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace TrackLite;

public partial class LixoPage : ContentPage
{
    public ObservableCollection<Corrida> CorridasLixo { get; set; } = Lixeira.CorridasLixo;

    public LixoPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OrdenarLixo();
    }

    public void OrdenarLixo()
    {
        var ordenadas = CorridasLixo.OrderByDescending(c => c.Data).ToList();
        CorridasLixo.Clear();
        foreach (var c in ordenadas)
            CorridasLixo.Add(c);
    }

    private async void OnRestaurarInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItemView swipeItemView && swipeItemView.BindingContext is Corrida corrida)
        {
            bool resposta = await DisplayAlert(
                "Confirmação",
                $"Deseja realmente restaurar a corrida de {corrida.Data:dd/MM/yyyy HH:mm} para o histórico?",
                "Sim",
                "Não"
            );

            if (!resposta)
                return;

            CorridasLixo.Remove(corrida);
            Lixeira.CorridasHistorico.Add(corrida);

            await Navigation.PopAsync();
        }
    }

    private void OnExcluirInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItemView swipeItemView && swipeItemView.BindingContext is Corrida corrida)
        {
            CorridasLixo.Remove(corrida);
        }
    }
}
