using Microsoft.Extensions.Logging;

namespace MAUICustomControls.MacCatalyst;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
            .ConfigureMauiHandlers(handlers =>
            {
#if WINDOWS
                handlers.AddHandler(typeof(MAUICustomControls.MacCatalyst.Controls.PopoverButton), typeof(MAUICustomControls.MacCatalyst.Platforms.Windows.PopoverButtonHandler));
#elif MACCATALYST
                handlers.AddHandler(typeof(Controls.PopoverButton), typeof(Platforms.MacCatalyst.PopoverButtonHandler));
                handlers.AddHandler(typeof(Controls.ToggleButton), typeof(Platforms.MacCatalyst.ToggleButtonHandler));
                handlers.AddHandler(typeof(Controls.ComboBox), typeof(Platforms.MacCatalyst.ComboBoxHandler));
                handlers.AddHandler(typeof(Controls.ToggleDropdown), typeof(Platforms.MacCatalyst.ToggleDropdownHandler));
                handlers.AddHandler(typeof(Controls.CheckBox), typeof(Platforms.MacCatalyst.CheckBoxHandler));
                handlers.AddHandler(typeof(Controls.ToggleSwitch), typeof(Platforms.MacCatalyst.ToggleSwitchHandler));
#endif
            });

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
