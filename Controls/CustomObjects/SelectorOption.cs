namespace MAUICustomControls.MacCatalyst.Controls.CustomObjects;

public record struct SelectorOption(
	string Text,
	string SystemIconName,
	string IconGlyph = "",
	string IconFontFamily = "",
	double IconFontSize = 0d);
