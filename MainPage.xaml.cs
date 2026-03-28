using MAUICustomControls.MacCatalyst.Controls.CustomObjects;

namespace MAUICustomControls.MacCatalyst;

public partial class MainPage : ContentPage
{

	public MainPage()
	{
		InitializeComponent();
	}

	private void Button_Clicked(object sender, EventArgs e)
	{
		popoverButton.HidePopover();
    }

    private void CheckBox_CheckedChanged(object sender, CheckedChangedEventArgs e)
	{
		DropdownBox2.Options = new List<SelectorOption>
		{
			new SelectorOption{ Text = "What", SystemIconName="star"},
			new SelectorOption{ Text = "The", SystemIconName="star"},
			new SelectorOption{ Text = "Fuck", SystemIconName="star"},
		};
	}
}
