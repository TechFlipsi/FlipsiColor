namespace FlipsiColor.AI;

/// <summary>
/// KI-Modell-IDs — gleiche Modelle wie C++ Version
/// </summary>
public enum ModellId
{
    /// <summary>NAFNet — Entrauschen (17MB, Core)</summary>
    NAFNet,
    /// <summary>RestormerLight — Entschärfen/Multi-Task (24MB, Core)</summary>
    RestormerLight,
    /// <summary>RealHATGAN — Hochskalieren beste Qualität (120MB, Lazy)</summary>
    RealHATGAN,
    /// <summary>RealESRGAN — Hochskalieren schnell (64MB, Lazy)</summary>
    RealESRGAN,
    /// <summary>CodeFormer — Gesichtswiederherstellung (350MB, Lazy)</summary>
    CodeFormer,
    /// <summary>AiLUTTransform — Farbstil-Lernen (8MB, Core)</summary>
    AiLUTTransform,
    /// <summary>EfficientNet — Szenen-Klassifizierung (4,6MB, Core)</summary>
    EfficientNet
}

/// <summary>
/// Metadaten für ein KI-Modell
/// </summary>
public sealed class ModellInfo
{
    public required ModellId Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public string? Sha256 { get; init; }
    public required long GroesseBytes { get; init; }
    public required bool Erforderlich { get; init; }
    public bool Heruntergeladen { get; set; }
}