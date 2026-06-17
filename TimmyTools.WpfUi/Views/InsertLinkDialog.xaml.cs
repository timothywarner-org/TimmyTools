using System.Windows;

namespace TimmyTools.WpfUi.Views;

public partial class InsertLinkDialog : Window
{
    public string? LinkUrl => string.IsNullOrWhiteSpace(LinkTextBox.Text)
        ? null
        : LinkTextBox.Text.Trim();

    public InsertLinkDialog()
    {
        InitializeComponent();

        // Seed from the clipboard when it already holds a usable absolute URL, so the
        // common "copy a link, then insert it" flow needs no typing.
        LinkTextBox.Text = TryGetClipboardUrl() ?? "";
        LinkTextBox.SelectAll();
        LinkTextBox.Focus();
    }

    private static string? TryGetClipboardUrl()
    {
        try
        {
            if (!Clipboard.ContainsText())
                return null;

            string text = Clipboard.GetText().Trim();
            return Uri.TryCreate(text, UriKind.Absolute, out Uri? _) ? text : null;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Clipboard locked by another application; just start with an empty box.
            return null;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
