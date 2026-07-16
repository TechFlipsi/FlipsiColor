using System;
using System.Collections.Generic;

using OpenCvSharp;

namespace FlipsiColor.Core;

/// <summary>
/// UndoableCommand — eine rückgängig machbare Aktion (Issue #16).
/// Command-Pattern: speichert alte und neue PipelineParams.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Führt die Aktion aus.</summary>
    void Ausfuehren();

    /// <summary>Nimmt die Aktion zurück.</summary>
    void Rueckgaengig();

    /// <summary>Beschreibung der Aktion (für History-Panel).</summary>
    string Beschreibung { get; }

    /// <summary>Zeitstempel der Aktion.</summary>
    DateTime Zeitstempel { get; }
}

/// <summary>
/// ParameterChangeCommand — speichert eine Änderung an PipelineParams (Issue #16).
/// </summary>
public sealed class ParameterChangeCommand : IUndoableCommand
{
    private readonly PipelineParams _ziel;
    private readonly PipelineParams _alteWerte;
    private readonly PipelineParams _neueWerte;
    private readonly string _beschreibung;

    /// <summary>
    /// Erstellt ein ParameterChangeCommand.
    /// </summary>
    /// <param name="ziel">Das PipelineParams-Objekt, das geändert wurde.</param>
    /// <param name="alteWerte">Snapshot der Werte vor der Änderung.</param>
    /// <param name="neueWerte">Snapshot der Werte nach der Änderung.</param>
    /// <param name="beschreibung">Beschreibung der Aktion.</param>
    public ParameterChangeCommand(PipelineParams ziel, PipelineParams alteWerte, PipelineParams neueWerte, string beschreibung)
    {
        _ziel = ziel;
        _alteWerte = alteWerte;
        _neueWerte = neueWerte;
        _beschreibung = beschreibung;
        Zeitstempel = DateTime.Now;
    }

    /// <inheritdoc/>
    public void Ausfuehren()
    {
        KopiereWerte(_neueWerte, _ziel);
    }

    /// <inheritdoc/>
    public void Rueckgaengig()
    {
        KopiereWerte(_alteWerte, _ziel);
    }

    /// <inheritdoc/>
    public string Beschreibung => _beschreibung;

    /// <inheritdoc/>
    public DateTime Zeitstempel { get; }

    /// <summary>Kopiert alle Werte von einem PipelineParams in ein anderes.</summary>
    private static void KopiereWerte(PipelineParams quelle, PipelineParams ziel)
    {
        ziel.WeissabgleichTemp = quelle.WeissabgleichTemp;
        ziel.WeissabgleichTint = quelle.WeissabgleichTint;
        ziel.Belichtung = quelle.Belichtung;
        ziel.Kontrast = quelle.Kontrast;
        ziel.Lichter = quelle.Lichter;
        ziel.Schatten = quelle.Schatten;
        ziel.Saettigung = quelle.Saettigung;
        ziel.Vibranz = quelle.Vibranz;
        ziel.SchaerfeBetrag = quelle.SchaerfeBetrag;
        ziel.LuminanzRauschen = quelle.LuminanzRauschen;
        ziel.ChrominanzRauschen = quelle.ChrominanzRauschen;
        ziel.ObjektivkorrekturAktiv = quelle.ObjektivkorrekturAktiv;
        ziel.GesichtswiederherstellungAktiv = quelle.GesichtswiederherstellungAktiv;
        ziel.HochskalierenFaktor = quelle.HochskalierenFaktor;
        ziel.DistortionGridAktiv = quelle.DistortionGridAktiv;
        ziel.ColorCalibrationAktiv = quelle.ColorCalibrationAktiv;
        ziel.Intensitaet = quelle.Intensitaet;
        ziel.Modus = quelle.Modus;
        ziel.StyleLutPfad = quelle.StyleLutPfad;
        ziel.AiStilName = quelle.AiStilName;
        ziel.KIDenoisingAktiv = quelle.KIDenoisingAktiv;
        ziel.KISchaerfungAktiv = quelle.KISchaerfungAktiv;
        ziel.KIUpscalingAktiv = quelle.KIUpscalingAktiv;
        ziel.KIGesichtswiederherstellungAktiv = quelle.KIGesichtswiederherstellungAktiv;
        ziel.KIFarbstilAktiv = quelle.KIFarbstilAktiv;
        ziel.KISzenenklassifizierungAktiv = quelle.KISzenenklassifizierungAktiv;
    }
}

/// <summary>
/// SnapshotCommand — speichert ein Bild-Snapshot (Mat) für Undo/Redo (Issue #16).
/// Disposed alte Mats korrekt.
/// </summary>
public sealed class SnapshotCommand : IUndoableCommand, IDisposable
{
    private readonly Action<Mat> _setBild;
    private readonly Mat _altesBild;
    private readonly Mat _neuesBild;
    private bool _disposed;

    /// <summary>
    /// Erstellt ein SnapshotCommand.
    /// </summary>
    /// <param name="setBild">Callback zum Setzen des aktuellen Bilds.</param>
    /// <param name="altesBild">Bild vor der Änderung (wird gesichert, beim Undo wiederhergestellt).</param>
    /// <param name="neuesBild">Bild nach der Änderung.</param>
    /// <param name="beschreibung">Beschreibung der Aktion.</param>
    public SnapshotCommand(Action<Mat> setBild, Mat altesBild, Mat neuesBild, string beschreibung)
    {
        _setBild = setBild;
        _altesBild = altesBild.Clone(); // Kopie sichern
        _neuesBild = neuesBild;
        Beschreibung = beschreibung;
        Zeitstempel = DateTime.Now;
    }

    /// <inheritdoc/>
    public void Ausfuehren()
    {
        _setBild(_neuesBild);
    }

    /// <inheritdoc/>
    public void Rueckgaengig()
    {
        _setBild(_altesBild);
    }

    /// <inheritdoc/>
    public string Beschreibung { get; }

    /// <inheritdoc/>
    public DateTime Zeitstempel { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _altesBild.Dispose();
    }
}

/// <summary>
/// UndoManager — verwaltet Undo/Redo-Stacks (Issue #16).
/// Max 50 Schritte (Speicher-Limit). Command-Pattern.
/// Speicher-Management: alte Mats werden disposiert.
/// </summary>
public sealed class UndoManager : IDisposable
{
    private const int MaxStufen = 50;

    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private bool _disposed;

    /// <summary>True wenn Undo möglich ist.</summary>
    public bool KannUndo => _undoStack.Count > 0;

    /// <summary>True wenn Redo möglich ist.</summary>
    public bool KannRedo => _redoStack.Count > 0;

    /// <summary>Anzahl Schritte im Undo-Stack.</summary>
    public int UndoAnzahl => _undoStack.Count;

    /// <summary>Anzahl Schritte im Redo-Stack.</summary>
    public int RedoAnzahl => _redoStack.Count;

    /// <summary>Liste aller Undo-Schritte (für History-Panel, älteste zuerst).</summary>
    public IReadOnlyList<IUndoableCommand> History
    {
        get
        {
            var list = new List<IUndoableCommand>(_undoStack);
            list.Reverse(); // Neueste zuerst → umkehren für Anzeige
            return list;
        }
    }

    /// <summary>
    /// Führt ein Command aus und legt es auf den Undo-Stack.
    /// Der Redo-Stack wird gelöscht.
    /// </summary>
    public void Ausfuehren(IUndoableCommand command)
    {
        command.Ausfuehren();
        _undoStack.Push(command);

        // Redo-Stack löschen — neue Aktion macht alte Redos ungültig
        _redoStack.Clear();

        // Max Stufen begrenzen — älteste entfernen
        while (_undoStack.Count > MaxStufen)
        {
            var stackArray = _undoStack.ToArray();
            Array.Reverse(stackArray); // Neueste zuerst → älteste am Ende
            Array.Resize(ref stackArray, MaxStufen); // älteste abschneiden
            _undoStack.Clear();
            Array.Reverse(stackArray); // wieder umkehren für Push
            foreach (var cmd in stackArray)
                _undoStack.Push(cmd);
        }
    }

    /// <summary>
    /// Macht die letzte Aktion rückgängig.
    /// </summary>
    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        var command = _undoStack.Pop();
        command.Rueckgaengig();
        _redoStack.Push(command);
    }

    /// <summary>
    /// Stellt eine rückgängig gemachte Aktion wieder her.
    /// </summary>
    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var command = _redoStack.Pop();
        command.Ausfuehren();
        _undoStack.Push(command);
    }

    /// <summary>
    /// Leert beide Stacks.
    /// </summary>
    public void Leeren()
    {
        while (_undoStack.Count > 0)
        {
            var cmd = _undoStack.Pop();
            (cmd as IDisposable)?.Dispose();
        }
        while (_redoStack.Count > 0)
        {
            var cmd = _redoStack.Pop();
            (cmd as IDisposable)?.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Leeren();
    }
}