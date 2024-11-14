using ECommons.DalamudServices;
using ECommons.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Linq;
using ECommons.Logging;

namespace Artisan.CraftingLogic.Solvers;

public class ScriptSolverCompiler : IDisposable
{
    private Thread _compilerThread;
    private ConcurrentQueue<ScriptSolverSettings.Script> _compilationQueue = new();
    private Semaphore _sema = new(0, int.MaxValue);
    private volatile bool _cancel;

    public ScriptSolverCompiler()
    {
        _compilerThread = new(ThreadProc);
        _compilerThread.Name = "Artisan Script Compiler";
        // note that we can only start compilation after plugin is fully constructed
        Svc.Framework.RunOnTick(_compilerThread.Start);
    }

    public void Dispose()
    {
        _cancel = true;
        _sema.Release();
        _compilerThread.Join();
    }

    public void Recompile(ScriptSolverSettings.Script script)
    {
        script.UpdateCompilation(ScriptSolverSettings.CompilationState.InProgress, "", null);
        _compilationQueue.Enqueue(script);
        _sema.Release();
    }

    private void ThreadProc()
    {
        var referenceList = BuildReferenceList();
        while (true)
        {
            _sema.WaitOne();
            if (_cancel)
                return;

            bool ok = _compilationQueue.TryDequeue(out var script);
            if (!ok)
            {
                Svc.Log.Error($"[ArtisanScript] Failed to dequeue script to compile, please report this error to the developer.");
                throw new Exception("Failed to dequeue a script for compilation");
            }

            if (script.CompilationState() == ScriptSolverSettings.CompilationState.Deleted)
                continue; // script was deleted before we got around to compiling it

            string diagnostics = "";
            try
            {
                var compiled = Compile(script.SourcePath, referenceList);
                diagnostics = compiled.diagnostics;
                if (compiled.binary == null)
                    throw new Exception($"Compilation failed:\n{compiled.diagnostics}");
                var asm = Load(compiled.binary);
                var type = asm.GetTypes().FirstOrDefault(t => t.BaseType?.FullName == "Artisan.CraftingLogic.Solver");
                if (type == null)
                    throw new Exception($"Source {script.SourcePath} does not contain any classes derived from Solver");
                script.UpdateCompilation(compiled.worst >= DiagnosticSeverity.Warning ? ScriptSolverSettings.CompilationState.SuccessWarnings : ScriptSolverSettings.CompilationState.SuccessClean, compiled.diagnostics, type);
            }
            catch (Exception ex)
            {
                DuoLog.Error($"Failed to compile {script.SourcePath}, see log for details.");
                Svc.Log.Error(ex, $"[ArtisanScript] Error compiling {script.SourcePath}");
                script.UpdateCompilation(ScriptSolverSettings.CompilationState.Failed, diagnostics, null);
            }
        }
    }

    // TODO: add caching of compilation results
    private (byte[]? binary, string diagnostics, DiagnosticSeverity worst) Compile(string path, IImmutableList<MetadataReference> referenceList)
    {
        var source = File.ReadAllText(path);
        var sourceText = SourceText.From(source);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, parseOptions);

        var id = $"ArtisanScript-{Path.GetFileNameWithoutExtension(path)}-{Guid.NewGuid()}";
        var compileOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
                allowUnsafe: true);
        var compilation = CSharpCompilation.Create(id, [parsedSyntaxTree], referenceList, compileOptions);

        using (var peStream = new MemoryStream())
        {
            var result = compilation.Emit(peStream);
            var diagnostics = string.Join('\n', result.Diagnostics);
            Svc.Log.Debug($"[ArtisanScript] Compiled {path} {(result.Success ? "without" : "with")} errors:\n{diagnostics}");
            return (result.Success ? peStream.ToArray() : null, diagnostics, result.Diagnostics.Length > 0 ? result.Diagnostics.Select(d => d.Severity).Max() : DiagnosticSeverity.Hidden);
        }
    }

    private Assembly Load(byte[] assembly)
    {
        if (DalamudReflector.TryGetLocalPlugin(out var instance, out var _, out var type))
        {
            var loader = type.GetField("loader", ReflectionHelper.AllFlags).GetValue(instance);
            var context = loader.GetFoP<AssemblyLoadContext>("context");
            using var stream = new MemoryStream(assembly);
            return context.LoadFromStream(stream);
        }
        throw new Exception("Failed to get local plugin");
    }

    private static ImmutableList<MetadataReference> BuildReferenceList()
    {
        Svc.Log.Debug("[ArtisanScript] Rebuilding references");
        var references = new List<MetadataReference>();
        foreach (var f in Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*", SearchOption.TopDirectoryOnly).Where(IsValidAssembly))
        {
            Svc.Log.Verbose($"[ArtisanScript] Adding reference: {f}");
            references.Add(MetadataReference.CreateFromFile(f));
        }
        //foreach (var f in Directory.GetFiles(Path.GetDirectoryName(typeof(System.Windows.Forms.Form).Assembly.Location), "*", SearchOption.TopDirectoryOnly).Where(IsValidAssembly))
        //{
        //    Svc.Log.Verbose($"[ArtisanScript] Adding reference: {f}");
        //    references.Add(MetadataReference.CreateFromFile(f));
        //}
        foreach (var f in Directory.GetFiles(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "*", SearchOption.AllDirectories).Where(IsValidAssembly))
        {
            Svc.Log.Verbose($"[ArtisanScript] Adding reference: {f}");
            references.Add(MetadataReference.CreateFromFile(f));
        }
        //foreach (var f in Directory.GetFiles(Path.GetDirectoryName(Svc.PluginInterface.GetType().Assembly.Location)!, "*", SearchOption.AllDirectories).Where(IsValidAssembly))
        //{
        //    Svc.Log.Verbose($"[ArtisanScript] Adding reference: {f}");
        //    references.Add(MetadataReference.CreateFromFile(f));
        //}
        return references.ToImmutableList();
    }

    private static bool IsValidAssembly(string path)
    {
        try
        {
            var assembly = AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
