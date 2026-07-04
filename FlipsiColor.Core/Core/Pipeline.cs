using System;

using FlipsiColor.Utils;

namespace FlipsiColor.Core;

/// <summary>
/// Pipeline-Logik — Szenen-spezifische Parameter-Vorschläge
/// </summary>
public sealed class Pipeline
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<Pipeline>();

    public Intensitaet Intensitaet { get; set; } = Intensitaet.Mittel;
    public BetriebsModus Modus { get; set; } = BetriebsModus.Ask;

    /// <summary>
    /// Gibt Standard-Parameter für einen Szenen-Typ zurück.
    /// Intensität und Modus aus der Pipeline-Instanz werden in die Parameter übernommen.
    /// </summary>
    public PipelineParams StandardParamsFuerSzene(string szenenTyp)
    {
        var param = new PipelineParams
        {
            Intensitaet = Intensitaet,
            Modus = Modus
        };

        switch (szenenTyp.ToLowerInvariant())
        {
            case "landschaft":
                param.Saettigung = IntensitaetToValue(0.15f);
                param.Kontrast = IntensitaetToValue(0.1f);
                param.SchaerfeBetrag = IntensitaetToValue(0.3f);
                break;
            case "porträt":
            case "portrait":
                param.Saettigung = IntensitaetToValue(0.05f);
                param.Belichtung = IntensitaetToValue(0.1f);
                param.GesichtswiederherstellungAktiv = true;
                break;
            case "architektur":
                param.Kontrast = IntensitaetToValue(0.2f);
                param.SchaerfeBetrag = IntensitaetToValue(0.5f);
                param.Saettigung = IntensitaetToValue(-0.05f);
                break;
            case "nacht":
                param.Belichtung = IntensitaetToValue(0.2f);
                param.LuminanzRauschen = IntensitaetToValue(0.3f);
                param.ChrominanzRauschen = IntensitaetToValue(0.2f);
                break;
            case "essen":
            case "food":
                param.Saettigung = IntensitaetToValue(0.25f);
                param.Vibranz = IntensitaetToValue(0.15f);
                param.Belichtung = IntensitaetToValue(0.05f);
                break;
            case "innenraum":
            case "interior":
                param.WeissabgleichTemp = 5200f;
                param.Belichtung = IntensitaetToValue(0.15f);
                param.LuminanzRauschen = IntensitaetToValue(0.15f);
                break;
            default:
                // Default: leichte Verbesserung
                param.Saettigung = IntensitaetToValue(0.08f);
                param.Kontrast = IntensitaetToValue(0.05f);
                break;
        }

        Log.Debug("Szenen-Params für '{Szene}': Bel={Bel} Kontr={Kontr} Sätt={Saet}",
            szenenTyp, param.Belichtung, param.Kontrast, param.Saettigung);
        return param;
    }

    /// <summary>
    /// CodeFormer Fidelity Weight basierend auf Intensität
    /// </summary>
    public float CodeFormerFidelityWeight() => Intensitaet switch
    {
        Intensitaet.Leicht => 0.9f,
        Intensitaet.Mittel => 0.7f,
        Intensitaet.Stark => 0.5f,
        _ => 0.7f
    };

    private float IntensitaetToValue(float baseValue) => Intensitaet switch
    {
        Intensitaet.Leicht => baseValue * 0.5f,
        Intensitaet.Mittel => baseValue,
        Intensitaet.Stark => baseValue * 1.5f,
        _ => baseValue
    };
}