using System.Collections.Generic;
using System;
using ImGuiNET;
using Dalamud.Interface.Utility.Raii;
using System.Linq;

namespace Artisan.CraftingLogic.Solvers;

public class ScriptSolverSettings
{
    public class Script
    {
        public int ID;
        public string SourcePath = "";
    }

    public List<Script> Scripts = new();

    public event Action<Script>? ScriptChanged;
    public event Action<Script>? ScriptRemoved;

    private string _newPath = ""; // TODO: move ui to a separate class...

    public bool Draw()
    {
        Script? toDel = null;
        foreach (var s in Scripts)
        {
            using var scope = ImRaii.PushId(s.ID);
            if (ImGui.Button("Recompile"))
                ScriptChanged?.Invoke(s);
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
            AddNewScript(new() { SourcePath = _newPath });
            _newPath = "";
            return true;
        }

        if (toDel != null)
        {
            Scripts.Remove(toDel);
            ScriptRemoved?.Invoke(toDel);
            return true;
        }

        return false;
    }

    public void AddNewScript(Script script)
    {
        var rng = new Random();
        script.ID = rng.Next(1, 50000);
        while (FindScript(script.ID) != null)
            script.ID = rng.Next(1, 50000);
        Scripts.Add(script);
        ScriptChanged?.Invoke(script);
    }

    public Script? FindScript(int id) => Scripts.Find(s => s.ID == id);
}
