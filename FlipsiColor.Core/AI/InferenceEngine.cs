using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

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

    // ImageNet-Normalisierung (EfficientNet / Standard PyTorch-Modelle)
    private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

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
    /// Szenen-Klassifizierung mit EfficientNet.
    /// Pipeline: float[] BGR HWC → OpenCvSharp Mat → Resize 224×224 →
    /// Normalisierung (ImageNet mean/std) → NCHW Transpose → ONNX Inferenz.
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

        // Eingabedaten (float HWC BGR [0,255]) → OpenCvSharp Mat
        using var srcMat = new Mat(hoehe, breite, MatType.CV_32FC3);
        srcMat.SetArray(bildDaten);

        // Resize auf 224×224 mit OpenCvSharp
        using var resized = new Mat();
        Cv2.Resize(srcMat, resized, new OpenCvSharp.Size(targetSize, targetSize), 0, 0, InterpolationFlags.Linear);

        // BGR → RGB (EfficientNet erwartet RGB)
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        // Normalisieren: pixel/255 → (pixel - mean) / std (ImageNet Normalisierung)
        using var normalized = new Mat();
        rgb.ConvertTo(normalized, MatType.CV_32FC3, 1.0 / 255.0); // [0,1]

        // HWC → CHW (NCHW) Transpose: für jeden Pixel (R,G,B) → separater Kanal
        var channels = Cv2.Split(normalized);
        try
        {
            // CHW-Array: [C, H*W] — Kanal 0=R, 1=G, 2=B (RGB-Reihenfolge)
            int pixelCount = targetSize * targetSize;
            var chwData = new float[3 * pixelCount];

            for (int c = 0; c < 3; c++)
            {
                var channelData = new float[pixelCount];
                channels[c].GetArray(out channelData);

                // ImageNet Normalisierung anwenden
                float mean = Mean[c];
                float std = Std[c];
                for (int i = 0; i < pixelCount; i++)
                {
                    chwData[c * pixelCount + i] = (channelData[i] - mean) / std;
                }
            }

            var output = Inferenz(ModellId.EfficientNet, chwData, [1, 3, targetSize, targetSize]);

            // ArgMax für Szenen-Klasse
            int maxIdx = 0;
            float maxVal = output[0];
            for (int i = 1; i < output.Length; i++)
            {
                if (output[i] > maxVal) { maxVal = output[i]; maxIdx = i; }
            }

            Log.Debug("SzeneKlassifizieren: Index={Idx}, Confidence={Conf:F4}", maxIdx, maxVal);
            return SzeneVonIndex(maxIdx);
        }
        finally
        {
            foreach (var c in channels)
                c.Dispose();
        }
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