using System.Diagnostics;

namespace Eshop.Operations.Desktop.Services;

public sealed class ExternalUriLauncher
    : IExternalUriLauncher
{
    public void Open(
        Uri uri)
    {
        ArgumentNullException.ThrowIfNull(
            uri);

        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    uri.AbsoluteUri,

                UseShellExecute =
                    true
            });
    }
}
