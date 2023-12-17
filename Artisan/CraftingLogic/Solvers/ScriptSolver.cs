using ECommons.DalamudServices;
using ECommons.Logging;
using ECommons.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Artisan.CraftingLogic.Solvers;

public class ScriptSolverDefinition : ISolverDefinition
{
    private Dictionary<int, Type> _scripts = new();

    private static ImmutableList<MetadataReference>? _referenceList;

    public ScriptSolverDefinition()
    {
        P.Config.ScriptSolverConfig.ScriptChanged += Recompile;
        P.Config.ScriptSolverConfig.ScriptRemoved += s => _scripts.Remove(s.ID);
        Svc.Framework.RunOnTick(() => {
            // we need to execute that after plugin is fully constructed
            // TODO: make threaded?..
            foreach (var s in P.Config.ScriptSolverConfig.Scripts)
                Recompile(s);
        });
    }

    public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
    {
        foreach (var s in _scripts)
            yield return new(this, s.Key, 0, $"Script: {s.Value.FullName}");
    }

    public Solver Create(CraftState craft, int flavour) => (Solver)Activator.CreateInstance(_scripts[flavour])!;

    private void Recompile(ScriptSolverSettings.Script script)
    {
        try
        {
            _scripts.Remove(script.ID);
            var binary = Compile(script.SourcePath);
            var asm = Load(binary);
            var type = asm.GetTypes().FirstOrDefault(t => t.BaseType?.FullName == "Artisan.CraftingLogic.Solver");
            if (type == null)
                throw new Exception($"[ArtisanScript] Source {script.SourcePath} does not contain any classes derived from Solver");
            _scripts[script.ID] = type;
        }
        catch (Exception ex)
        {
            DuoLog.Error($"Failed to recompile {script.SourcePath}:\n{ex}");
        }
    }

    // TODO: add caching of compilation results
    private byte[] Compile(string path)
    {
        _referenceList ??= BuildReferenceList();

        var source = File.ReadAllText(path);
        var sourceText = SourceText.From(source);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, parseOptions);

        var id = $"ArtisanScript-{Path.GetFileNameWithoutExtension(path)}-{Guid.NewGuid()}";
        var compileOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
                allowUnsafe: true);
        var compilation = CSharpCompilation.Create(id, [parsedSyntaxTree], _referenceList, compileOptions);

        using (var peStream = new MemoryStream())
        {
            var result = compilation.Emit(peStream);
            if (!result.Success)
                throw new Exception($"Failed to compile {path}:\n{string.Join('\n', result.Diagnostics)}");
            Svc.Log.Debug($"[ArtisanScript] Compiled {path} without errors.");
            return peStream.ToArray();
        }
    }

    private Assembly Load(byte[] assembly)
    {
        if (DalamudReflector.TryGetLocalPlugin(out var instance, out var type))
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
            Svc.Log.Debug($"[ArtisanScript] Adding reference: {f}");
            references.Add(MetadataReference.CreateFromFile(f));
        }
        //foreach (var f in Directory.GetFiles(Path.GetDirectoryName(typeof(System.Windows.Forms.Form).Assembly.Location), "*", SearchOption.TopDirectoryOnly).Where(IsValidAssembly))
        //{
        //    Svc.Log.Debug($"[ArtisanScript] Adding reference: {f}");
        //    references.Add(MetadataReference.CreateFromFile(f));
        //}
        foreach (var f in Directory.GetFiles(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "*", SearchOption.AllDirectories).Where(IsValidAssembly))
        {
            Svc.Log.Debug($"[ArtisanScript] Adding reference: {f}");
            references.Add(MetadataReference.CreateFromFile(f));
        }
        //foreach (var f in Directory.GetFiles(Path.GetDirectoryName(Svc.PluginInterface.GetType().Assembly.Location)!, "*", SearchOption.AllDirectories).Where(IsValidAssembly))
        //{
        //    Svc.Log.Debug($"[ArtisanScript] Adding reference: {f}");
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
