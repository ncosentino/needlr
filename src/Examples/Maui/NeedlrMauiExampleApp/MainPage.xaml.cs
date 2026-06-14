namespace NeedlrMauiExampleApp;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage(IGreetingService greetingService)
	{
		InitializeComponent();
		GreetingLabel.Text = greetingService.Greet();
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;

		if (count == 1)
			CounterBtn.Text = $"Clicked {count} time";
		else
			CounterBtn.Text = $"Clicked {count} times";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}
