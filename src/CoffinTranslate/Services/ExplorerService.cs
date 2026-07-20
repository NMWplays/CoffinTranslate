using System.Diagnostics;

namespace CoffinTranslate.Services;

public static class ExplorerService
{
    /// <summary>Opens a folder in the platform's file manager. Best effort.</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // nothing sensible to do if the OS has no file manager handler
        }
    }

    /// <summary>Opens an http(s) URL in the default browser. Best effort; ignores anything else.</summary>
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            // no browser handler — nothing to do
        }
    }
}
