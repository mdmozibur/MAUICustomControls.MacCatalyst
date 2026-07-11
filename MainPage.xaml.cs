using MAUICustomControls.MacCatalyst.Controls.CustomObjects;
using System.Collections.ObjectModel;

namespace MAUICustomControls.MacCatalyst;

public partial class MainPage : ContentPage
{
	public ObservableCollection<AnimalGroup> Animals { get; } = new()
	{
		new AnimalGroup("Bears", new[] { "American Black Bear", "Asian Black Bear", "Brown Bear", "Grizzly Bear", "Panda", "Polar Bear", "Sloth Bear", "Sun Bear" }),
		new AnimalGroup("Cats", new[] { "Cheetah", "Cougar", "Jaguar", "Leopard", "Lion", "Lynx", "Ocelot", "Serval", "Tiger" }),
		new AnimalGroup("Dogs", new[] { "Coyote", "Dingo", "Fox", "Gray Wolf", "Jackal", "Raccoon Dog" }),
		new AnimalGroup("Elephants", new[] { "African Bush Elephant", "African Forest Elephant", "Asian Elephant" }),
		new AnimalGroup("Monkeys", new[] { "Baboon", "Capuchin", "Gibbon", "Howler", "Macaque", "Mandrill", "Marmoset", "Spider Monkey", "Tamarin" }),
		new AnimalGroup("Reptiles", new[] { "Alligator", "Chameleon", "Crocodile", "Gecko", "Iguana", "Komodo Dragon", "Python" }),
		new AnimalGroup("Whales", new[] { "Beluga", "Blue Whale", "Humpback Whale", "Narwhal", "Orca", "Sperm Whale" }),
	};

	public MainPage()
	{
		InitializeComponent();

		ZoomedInCollection.ItemsSource = Animals;
		ZoomedOutCollection.ItemsSource = Animals;

		AnimalsSemanticZoom.ViewChangeStarted += (_, e) =>
			ToggleZoomButton.Text = e.IsSourceZoomedInView ? "Zoom in" : "Zoom out";
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

    private void Button_Clicked1(object sender, EventArgs e)
	{
		TabsControl.Items.Add("Tab " + Random.Shared.Next(100));
	}

	private void OnToggleZoomClicked(object sender, EventArgs e)
	{
		AnimalsSemanticZoom.ToggleActiveView();
	}

	private void OnGroupHeaderTapped(object sender, TappedEventArgs e)
	{
		// Clicking a group header in the zoomed-in view zooms out (UWP-like)
		AnimalsSemanticZoom.IsZoomedInViewActive = false;
	}

	private void OnGroupTileTapped(object sender, TappedEventArgs e)
	{
		// Clicking a group tile in the zoomed-out view jumps to that group
		if (sender is BindableObject bindable && bindable.BindingContext is AnimalGroup group)
			AnimalsSemanticZoom.NavigateToGroup(group);
	}
}

/// <summary>Grouped demo data for the SemanticZoom sample.</summary>
public sealed class AnimalGroup : List<string>
{
	public AnimalGroup(string name, IEnumerable<string> animals) : base(animals)
	{
		Name = name;
	}

	public string Name { get; }
}
