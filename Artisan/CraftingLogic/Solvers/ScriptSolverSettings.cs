using System.Collections.Generic;
using System;
using ImGuiNET;
using Dalamud.Interface.Utility.Raii;
using System.Linq;

namespace Artisan.CraftingLogic.Solvers;

public class ScriptSolverSettings
{
    public enum CompilationState { None, InProgress, SuccessClean, SuccessWarnings, Failed, Deleted }

    public class Script
    {
        public int ID { get; init; }
        public string SourcePath { get; init; } = "";
        private CompilationState _compilationState;
        private string _compilationOutput = "";
        private Type? _compilationResult;

        public Script(int id, string sourcePath)
        {
            ID = id;
            SourcePath = sourcePath;
        }

        public CompilationState CompilationState() { lock (this) return _compilationState; }
        public string CompilationOutput() { lock (this) return _compilationOutput; }
        public Type? CompilationResult() { lock (this) return _compilationResult; }

        public void UpdateCompilation(CompilationState state, string output, Type? result)
        {
            lock (this)
            {
                if (_compilationState != ScriptSolverSettings.CompilationState.Deleted)
                {
                    _compilationState = state;
                    _compilationOutput = output;
                    _compilationResult = result;
                }
                // else: don't bother, it's dead
            }
        }
    }

    public List<Script> Scripts = new();

    private ScriptSolverCompiler _compiler = new();
    private string _newPath = ""; // TODO: move ui to a separate class...

    public void Init()
    {
        // kick off compilation for all preloaded scripts
        foreach (var s in Scripts)
            _compiler.Recompile(s);
    }

    public void Dispose()
    {
        _compiler.Dispose();
    }

    public bool Draw()
    {
        ImGui.TextWrapped($"This is a very advanced feature, aimed at users wishing to create their own dynamic solvers using C#. Please visit the github source code and view the Demoscripts folder for an example. No support will be given as to learning C# to do this.");
        ImGui.Separator();
        Script? toDel = null;
        foreach (var s in Scripts)
        {
            var state = s.CompilationState();

            using var scope = ImRaii.PushId(s.ID);

            using (ImRaii.Disabled(state == CompilationState.InProgress))
            {
                // TODO: show icon depending on state...
                if (ImGui.Button($"Recompile: {state}", new(100, 0)))
                    _compiler.Recompile(s);
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted($"Compilation output:\n{s.CompilationOutput()}");
                    ImGui.EndTooltip();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Delete"))
                toDel = s;
            ImGui.SameLine();
            ImGui.TextUnformatted($"[{s.ID}] {s.SourcePath}");
        }

        ImGui.InputText("New script path", ref _newPath, 256);
        ImGui.SameLine();
        if (ImGui.Button("Add") && _newPath.Length > 0 && !Scripts.Any(s => s.SourcePath == _newPath))
        {
            AddNewScript(new(_newPath));
            _newPath = "";
            return true;
        }

        if (toDel != null)
        {
            toDel.UpdateCompilation(CompilationState.Deleted, "Deletion in progress", null);
            Scripts.Remove(toDel);
            return true;
        }

        return false;
    }

    public void AddNewScript(string path)
    {
        var rng = new Random();
        var id = rng.Next(1, 50000);
        while (FindScript(id) != null)
            id = rng.Next(1, 50000);
        var s = new Script(id, path);
        Scripts.Add(s);
        _compiler.Recompile(s);
    }

    public Script? FindScript(int id) => Scripts.Find(s => s.ID == id);
}
