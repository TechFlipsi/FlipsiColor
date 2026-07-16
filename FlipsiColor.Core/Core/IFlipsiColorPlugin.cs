using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using OpenCvSharp;

using FlipsiColor.Utils;

namespace FlipsiColor.Core;

/// <summary>
/// Plugin-Schnittstelle für Drittanbieter-Filter und Module (Issue #18).
/// Plugins erhalten Zugriff auf Bild (Mat), PipelineParams und Settings
/// ausschließlich über diese API.
/// </summary>
public interface IFlipsiColorPlugin
{
    /// <summary>Eindeutiger Name des Plugins.</summary>
    string Name { get; }

    /// <summary>Version des Plugins.</summary>
    string Version { get; }

    /// <summary>App-Version, mit der das Plugin kompatibel ist.</summary>
    string KompatibelMitVersion { get; }

    /// <summary>Kurze Beschreibung des Plugins.</summary>
    string Beschreibung { get; }

    /// <summary>
    /// Verarbeitet ein Bild — die Kernfunktion des Plugins.
    /// </summary>
    /// <param name="bild">Eingabebild (BGR, 8-bit). Darf modifiziert oder ersetzt werden.</param>
    /// <param name="param">Pipeline-Parameter (nur Lesen).</param>
    /// <returns>Verarbeitetes Bild oder null bei Fehler.</returns>
    Mat? Verarbeiten(Mat bild, PipelineParams param);

    /// <summary>
    /// Wird beim Laden des Plugins aufgerufen — für Initialisierung.
    /// </summary>
    void Initialisieren();
}

/// <summary>
/// PluginInfo — Metadaten über ein geladenes Plugin (Issue #18).
/// </summary>
public sealed class PluginInfo
{
    /// <summary>Name des Plugins.</summary>
    public string Name { get; set; } = "";

    /// <summary>Version des Plugins.</summary>
    public string Version { get; set; } = "";

    /// <summary>App-Version, mit der das Plugin kompatibel ist.</summary>
    public string KompatibelMitVersion { get; set; } = "";

    /// <summary>Beschreibung des Plugins.</summary>
    public string Beschreibung { get; set; } = "";

    /// <summary>Pfad zur Plugin-DLL.</summary>
    public string Pfad { get; set; } = "";

    /// <summary>True wenn das Plugin aktiviert ist.</summary>
    public bool Aktiviert { get; set; } = true;

    /// <summary>True wenn das Plugin erfolgreich geladen wurde.</summary>
    public bool Geladen { get; set; }
}

/// <summary>
/// PluginManager — lädt, verwaltet und aktiviert/deaktiviert Plugins (Issue #18).
/// Plugins werden aus LocalAppData/FlipsiColor/Plugins/ geladen.
/// Verwendet AssemblyLoadContext für Isolation.
/// Pitfall #17: System.AppContext.BaseDirectory statt Assembly.Location.
/// </summary>
public sealed class PluginManager : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<PluginManager>();

    private readonly string _pluginVerzeichnis;
    private readonly List<IFlipsiColorPlugin> _geladenePlugins = [];
    private readonly List<PluginInfo> _pluginInfos = [];
    private readonly AssemblyLoadContext _loadContext;
    private bool _disposed;

    /// <summary>Verzeichnis für Plugin-DLLs.</summary>
    public string Verzeichnis => _pluginVerzeichnis;

    /// <summary>Alle geladenen Plugin-Infos.</summary>
    public IReadOnlyList<PluginInfo> PluginInfos => _pluginInfos.AsReadOnly();

    /// <summary>Alle aktivierten Plugins (für Pipeline-Ausführung).</summary>
    public IReadOnlyList<IFlipsiColorPlugin> AktivePlugins =>
        _geladenePlugins.Where((_, i) => _pluginInfos[i].Aktiviert).ToList();

    /// <summary>App-Version für Kompatibilitäts-Prüfung.</summary>
    public string AppVersion { get; set; } = "0.6.1";

    public PluginManager()
    {
        _pluginVerzeichnis = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlipsiColor", "Plugins");

        // Eigener AssemblyLoadContext für Plugin-Isolation
        _loadContext = new PluginLoadContext(_pluginVerzeichnis);
    }

    /// <summary>
    /// Initialisiert das Plugin-Verzeichnis und lädt alle Plugins.
    /// </summary>
    public void Initialisieren()
    {
        try
        {
            if (!Directory.Exists(_pluginVerzeichnis))
            {
                Directory.CreateDirectory(_pluginVerzeichnis);
                Log.Information("Plugin-Verzeichnis erstellt: {Verzeichnis}", _pluginVerzeichnis);
                return; // Keine Plugins vorhanden
            }

            PluginsLaden();
        }
        catch (Exception ex)
        {
            Log.Warning("Plugin-Initialisierung fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }
    }

    /// <summary>
    /// Lädt alle Plugin-DLLs aus dem Plugin-Verzeichnis.
    /// </summary>
    private void PluginsLaden()
    {
        var dlls = Directory.GetFiles(_pluginVerzeichnis, "*.dll");
        foreach (var dll in dlls)
        {
            try
            {
                var plugin = PluginAusDllLaden(dll);
                if (plugin != null)
                {
                    _geladenePlugins.Add(plugin);
                    _pluginInfos.Add(new PluginInfo
                    {
                        Name = plugin.Name,
                        Version = plugin.Version,
                        KompatibelMitVersion = plugin.KompatibelMitVersion,
                        Beschreibung = plugin.Beschreibung,
                        Pfad = dll,
                        Aktiviert = true,
                        Geladen = true
                    });
                    Log.Information("Plugin geladen: {Name} v{Version}", plugin.Name, plugin.Version);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Plugin konnte nicht geladen werden ({Dll}): {Fehler}",
                    Path.GetFileName(dll), SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            }
        }
    }

    /// <summary>
    /// Lädt eine einzelne Plugin-DLL und instanziiert das Plugin.
    /// </summary>
    private IFlipsiColorPlugin? PluginAusDllLaden(string dllPfad)
    {
        var assembly = _loadContext.LoadFromAssemblyPath(dllPfad);

        // Typen suchen, die IFlipsiColorPlugin implementieren
        var pluginTyp = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IFlipsiColorPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        if (pluginTyp == null)
        {
            Log.Warning("Kein IFlipsiColorPlugin-Typ in {Dll} gefunden", Path.GetFileName(dllPfad));
            return null;
        }

        var plugin = (IFlipsiColorPlugin)Activator.CreateInstance(pluginTyp)!;

        // Kompatibilitäts-Prüfung
        if (!IstKompatibel(plugin.KompatibelMitVersion, AppVersion))
        {
            Log.Warning("Plugin {Name} ist nicht kompatibel (Plugin: {PluginVersion}, App: {AppVersion})",
                plugin.Name, plugin.KompatibelMitVersion, AppVersion);
            return null;
        }

        plugin.Initialisieren();
        return plugin;
    }

    /// <summary>
    /// Prüft ob eine Plugin-Version mit der App-Version kompatibel ist.
    /// Einfache Major-Version-Prüfung.
    /// </summary>
    private static bool IstKompatibel(string pluginVersion, string appVersion)
    {
        if (string.IsNullOrEmpty(pluginVersion)) return true;

        try
        {
            var pluginParts = pluginVersion.Split('.');
            var appParts = appVersion.Split('.');
            return pluginParts[0] == appParts[0];
        }
        catch
        {
            return true; // Bei Parse-Fehler: erlauben
        }
    }

    /// <summary>
    /// Aktiviert oder deaktiviert ein Plugin anhand des Namens.
    /// </summary>
    public bool PluginAktivieren(string name, bool aktivieren)
    {
        for (var i = 0; i < _pluginInfos.Count; i++)
        {
            if (_pluginInfos[i].Name == name)
            {
                _pluginInfos[i].Aktiviert = aktivieren;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Verarbeitet ein Bild durch alle aktiven Plugins in der Reihenfolge ihrer Registrierung.
    /// </summary>
    public Mat? PluginsAusfuehren(Mat bild, PipelineParams param)
    {
        var result = bild;
        foreach (var plugin in AktivePlugins)
        {
            try
            {
                var pluginResult = plugin.Verarbeiten(result, param);
                if (pluginResult != null)
                {
                    if (result != bild)
                        result.Dispose();
                    result = pluginResult;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Plugin {Name} Fehler: {Fehler}", plugin.Name,
                    SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            }
        }
        return result;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadContext.Unload();
    }
}

/// <summary>
/// AssemblyLoadContext für Plugin-Isolation (Issue #18).
/// Lädt Plugins aus dem Plugin-Verzeichnis, isoliert von der Haupt-App.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly string _pluginVerzeichnis;

    internal PluginLoadContext(string pluginVerzeichnis) : base(isCollectible: true)
    {
        _pluginVerzeichnis = pluginVerzeichnis;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Nur Plugin-DLLs aus dem Plugin-Verzeichnis laden
        var dllPfad = Path.Combine(_pluginVerzeichnis, assemblyName.Name + ".dll");
        if (File.Exists(dllPfad))
        {
            return LoadFromAssemblyPath(dllPfad);
        }
        // Default: null → aufrufende Assembly (Haupt-App) liefert die Assembly
        return null;
    }
}