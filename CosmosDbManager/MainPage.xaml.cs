using System.Linq;

using CosmosDbManager.Persistence;
using CosmosDbManager.ViewModels;

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace CosmosDbManager;

public sealed partial class MainPage : Page
{
    private readonly MainViewModel _mainViewModel = new();

    public MainPage()
    {
        InitializeComponent();

        DataContext = _mainViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    private async void DatabasesBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems == null || e.AddedItems.Count < 1)
        {
            return;
        }

        await _mainViewModel.SelectDatabaseAsync((CosmosDatabase)e.AddedItems[0]);
    }

    private async void ContainerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems == null || e.AddedItems.Count < 1)
        {
            return;
        }
            
        await _mainViewModel.SelectContainerAsync((CosmosContainer)e.AddedItems[0]);
    }
        
    private void Limit_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        args.Cancel = args.NewText.Any(c => !char.IsDigit(c));
    }
}