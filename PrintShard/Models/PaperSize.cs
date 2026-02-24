namespace PrintShard.Models;

/// <summary>Named paper size with dimensions in millimetres (portrait orientation).</summary>
public sealed record PaperSize(string Name, double WidthMm, double HeightMm)
{
    public static readonly PaperSize A4      = new("A4",      210.0,  297.0);
    public static readonly PaperSize Letter  = new("Letter",  215.9,  279.4);
    public static readonly PaperSize A3      = new("A3",      297.0,  420.0);
    public static readonly PaperSize Legal   = new("Legal",   215.9,  355.6);
    public static readonly PaperSize A5      = new("A5",      148.0,  210.0);
    public static readonly PaperSize Tabloid = new("Tabloid", 279.4,  431.8);

    public static PaperSize[] All => [A4, Letter, A3, Legal, A5, Tabloid];

    public override string ToString() => Name;
}
