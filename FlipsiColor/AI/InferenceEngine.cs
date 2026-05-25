using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using FlipsiColor.Utils;

namespace FlipsiColor.AI;

/// <summary>
/// ONNX Inference-Engine — führt Modell-Inferenz aus
/// </summary>
public sealed class InferenceEngine : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<InferenceEngine>();
    private readonly ModelManager _modelManager;
    private bool _disposed;

    public InferenceEngine(ModelManager modelManager)
    {
        _modelManager = modelManager;
    }

    /// <summary>
    /// Führt Inferenz mit einem bestimmten Modell aus
    /// </summary>
    public float[] Inferenz(ModellId modellId, float[] eingabe, int[] form)
    {
        var session = _modelManager.Session(modellId);
        if (session == null)
        {
            Log.Error("Keine Session für Modell {Id} — ModellSicherstellenAsync zuerst aufrufen", modellId);
            return Array.Empty<float>();
        }

        var inputName = session.InputNames.First();
        var inputMeta = session.InputMetadata[inputName];
        var dimensions = new int[form.Length];
        for (int i = 0; i < form.Length; i++)
            dimensions[i] = form[i];

        var tensor = new DenseTensor<float>(eingabe, dimensions);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, tensor)
        };

        using var results = session.Run(inputs);
        var output = results.First().AsTensor<float>();
        var resultArray = new float[output.Length];
        for (int i = 0; i < output.Length; i++)
            resultArray[i] = output[i];

        Log.Debug("Inferenz {Modell}: Input=[{Form}], Output Length={Len}",
            modellId, string.Join(",", form), resultArray.Length);

        return resultArray;
    }

    /// <summary>
    /// Szenen-Klassifizierung mit EfficientNet
    /// </summary>
    public async Task<string> SzeneKlassifizierenAsync(float[] bildDaten, int hoehe, int breite)
    {
        if (bildDaten == null || bildDaten.Length == 0)
        {
            Log.Warning("SzeneKlassifizierenAsync: leerer Input — übersprungen");
            return "Unbekannt";
        }

        if (!await _modelManager.ModellSicherstellenAsync(ModellId.EfficientNet))
            return "Unbekannt";

        // EfficientNet erwartet NCHW Format [1, 3, 224, 224]
        const int targetSize = 224;
        var input = new float[1 * 3 * targetSize * targetSize];

        // Bild auf 224x224 resizen und nach NCHW konvertieren
        // TODO: Korrekte Resize+Normalisierung statt Null-Array — siehe https://github.com/TechFlipsi/FlipsiColor/issues/42
        if (Math.Max(hoehe, breite) > 0)
        {
            // Best-Effort: Zentriere verfügbare Daten im Ziel-Array
            int srcChannels = 3;
            int srcRowLen = breite * srcChannels;
            int dstRowLen = targetSize * srcChannels;
            int copyLen = Math.Min(srcRowLen, dstRowLen);
            for (int y = 0; y < Math.Min(hoehe, targetSize); y++)
            {
                int srcOffset = y * srcRowLen;
                int dstOffset = y * dstRowLen;
                if (srcOffset + copyLen <= bildDaten.Length && dstOffset + copyLen <= input.Length)
                    Array.Copy(bildDaten, srcOffset, input, dstOffset, copyLen);
            }
        }
        var output = Inferenz(ModellId.EfficientNet, input, [1, 3, targetSize, targetSize]);

        // ArgMax für Szenen-Klasse
        int maxIdx = 0;
        float maxVal = output[0];
        for (int i = 1; i < output.Length; i++)
        {
            if (output[i] > maxVal) { maxVal = output[i]; maxIdx = i; }
        }

        return SzeneVonIndex(maxIdx);
    }

    private static string SzeneVonIndex(int idx) => idx switch
    {
        0 => "Landschaft",
        1 => "Porträt",
        2 => "Architektur",
        3 => "Nacht",
        4 => "Essen",
        5 => "Innenraum",
        _ => "Unbekannt"
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}