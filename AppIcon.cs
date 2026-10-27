using System;
using System.Drawing;
using System.IO;

namespace HerdrTray;

/// <summary>The exe icon, reused by every window so the app has one face.</summary>
internal static class AppIcon
{
    private const string Resource = "HerdrTray.app.ico";

    public static Icon Load(int size)
    {
        using Stream stream = typeof(AppIcon).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException("The application icon is missing.");
        return new Icon(stream, size, size);
    }
}
