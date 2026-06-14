namespace NeedlrMauiExampleApp;

public partial class MainPage : ContentPage
{
	// The view model is injected by Needlr — no manual registration. Setting it as the
	// BindingContext is all that is needed to bind the view's XAML to the view model.
	public MainPage(MainPageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
