using System.Diagnostics;
using System.Reflection;
using SwitchPlease.App.Localization;

namespace SwitchPlease.App;

/// <summary>The About window: what this is, which version, and where to find it.</summary>
public sealed class AboutForm : Form
{
    public const string SourceUrl = "https://github.com/marrakesh/switch-please";

    /// <summary>
    /// Funding page, if there is one: Buy Me a Coffee, Patreon, Ko-fi, GitHub Sponsors --
    /// the link does not care which. Empty hides it entirely rather than pointing at a page
    /// that does not exist.
    /// </summary>
    public const string DonateUrl = "https://ko-fi.com/marrakeshgtp";

    public AboutForm()
    {
        var text = Localizer.Text;

        Theme.Apply(this);

        Text = text.AboutTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var close = Theme.Button(text.ButtonClose, DialogResult.OK);

        var body = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(Theme.Wide, Theme.Wide, Theme.Wide, Theme.Pad),
        };

        body.Controls.Add(Theme.TitleLabel("Switch Please"));

        var tagline = Theme.HintLabel(text.AboutTagline, 380);
        tagline.Margin = new Padding(0, Theme.Tight, 0, Theme.Pad);
        body.Controls.Add(tagline);

        var version = Theme.BodyLabel(string.Format(text.AboutVersion, ReadVersion()));
        version.Margin = new Padding(0, 0, 0, Theme.Tight);
        body.Controls.Add(version);

        var copyright = Theme.HintLabel($"{text.AboutLicense}  ·  Copyright (c) 2026 Oleksii Ozerov", 380);
        copyright.Margin = new Padding(0, 0, 0, Theme.Pad);
        body.Controls.Add(copyright);

        body.Controls.Add(Link(text.AboutSource, SourceUrl));

        if (DonateUrl.Length > 0)
        {
            body.Controls.Add(Link(text.AboutDonate, DonateUrl));
        }

        Controls.Add(Theme.Page(body, close));

        AcceptButton = close;
        CancelButton = close;
    }

    private static LinkLabel Link(string caption, string url)
    {
        var link = new LinkLabel
        {
            Text = caption,
            AutoSize = true,
            LinkColor = Theme.Accent,
            ActiveLinkColor = Theme.Accent,
            VisitedLinkColor = Theme.Accent,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Margin = new Padding(0, 0, 0, Theme.Tight),
        };

        link.LinkClicked += (_, _) => Open(url);
        return link;
    }

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser, or the user cancelled the shell prompt. Nothing to recover.
        }
    }

    private static string ReadVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;

        return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
