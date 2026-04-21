using Artisan.CraftingLogic.CraftData;
using Artisan.UI;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TerraFX.Interop.Windows;

namespace Artisan.CraftingLogic;

[Serializable]
public class TrackConditions
{
    [NonSerialized]
    public const string ConditionsFileName = "ArtisanConditions.dat";
    [NonSerialized]
    public readonly DirectoryInfo ConditionsDirectory;

    [NonSerialized]
    public bool IsLoaded = false;
    [NonSerialized]
    public bool TableLoaded = false;
    [NonSerialized]
    public Dictionary<string, RecipeConditions>? combinedConditionData = null;
    [NonSerialized]
    public Dictionary<string, int>? combinedTotals = null;

    public int Version { get; set; } = 1;
    public List<RecipeConditions> Records = new();

    public TrackConditions()
    {
        ConditionsDirectory = Svc.PluginInterface.ConfigDirectory;
        try
        {
            Directory.CreateDirectory(ConditionsDirectory.FullName);
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Could not create directory \"{ConditionsDirectory.FullName}\" for conditions tracking:\n{e}");
        }
    }

    public void LoadFile()
    {
        var file = new FileInfo(Path.Combine(ConditionsDirectory.FullName, ConditionsFileName));
        if (!file.Exists)
            return;

        try
        {
            Svc.Log.Information("Loading condition data...");
            Records.Clear();
            Records.AddRange(ReadFile(file));
            IsLoaded = true;
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Could not read conditions tracking file \"{file.FullName}\":\n{e}");
        }
    }

    public static List<RecipeConditions> ReadFile(FileInfo file)
    {
        if (!file.Exists)
            return new List<RecipeConditions>();

        try
        {
            var raw = File.ReadAllText(file.FullName);
            var json = JObject.Parse(raw);
            var tc = json.ToObject<TrackConditions>() ?? new();
            return tc.Records;
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Unknown error reading conditions tracking file \"{file.FullName}\":\n{e}");
            return new List<RecipeConditions>();
        }
    }

    public void WriteFile()
    {
        var file = new FileInfo(Path.Combine(ConditionsDirectory.FullName, ConditionsFileName));
        try
        {
            var json = JObject.FromObject(this).ToString();
            File.WriteAllText(file.FullName, json);
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Could not write conditions tracking file \"{file.FullName}\":\n{e}");
        }
    }

    public void AddRecipeCondition(CraftState craft, StepState step)
    {
        if (!IsLoaded)
            LoadFile();

        var recipeId = (step.MaterialMiracleActive || step.PrevMaterialMiracleActive) ? (int)craft.RecipeId + 1000000 : (int)craft.RecipeId;
        RecipeConditions? rc = Records.Find(r => r.RecipeID == recipeId);
        if (rc == null)
        {
            rc = new RecipeConditions
            {
                RecipeID = recipeId,
                RecipeDurability = craft.CraftDurability,
                RecipeProgress = craft.CraftProgress,
                RecipeQuality = Calculations.RecipeMaxQuality(craft.Recipe),
                RecipeConditionFlags = GetConditionFlagString(craft),
                RecipeConditionIDs = craft.Recipe.RecipeLevelTable.Value.ConditionsFlag,
                RecipeAction = craft.MissionHasMaterialMiracle ? "MM" : craft.MissionHasSteadyHand ? "Steady" : ""
            };
            Records.Add(rc);
        }
        if (rc.RecipeConditionFlags == null)
            rc.RecipeConditionFlags = GetConditionFlagString(craft);
        if (rc.RecipeConditionIDs == 0)
            rc.RecipeConditionIDs = craft.Recipe.RecipeLevelTable.Value.ConditionsFlag;
        rc.AddCondition(step.Condition);

        WriteFile();
    }

    public string GetConditionFlagString(CraftState craft)
    {
        if (!craft.CraftExpert)
            return "non-expert";

        string ret = "";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.Normal))
            ret += "N/";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.Good))
            ret += "G/";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.Centered))
            ret += "C/";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.Sturdy))
            ret += "S/";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.Pliant))
            ret += "Pl/";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.Malleable))
            ret += "M/";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.Primed))
            ret += "Pr/";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.GoodOmen))
            ret += "GO/";
        if (craft.ConditionFlags.HasFlag(ConditionFlags.Robust))
            ret += "R/";
        return ret.Remove(ret.Length - 1);
    }
}

public class RecipeConditions
{
    public int RecipeID;
    public int RecipeDurability;
    public int RecipeProgress;
    public int RecipeQuality;
    public string RecipeConditionFlags = "";
    public ushort RecipeConditionIDs = 0;
    public string RecipeAction = "";
    public Dictionary<Condition, int> Counts = new();

    public void AddCondition(Condition condition)
    {
        if (Counts.ContainsKey(condition))
            Counts[condition]++;
        else
            Counts[condition] = 1;
    }

    public string StatsString()
    {
        return $"{(this.RecipeAction.Length == 0 ? "None-" : this.RecipeAction + "-")}{this.RecipeDurability}/{this.RecipeProgress}/{this.RecipeQuality}";
    }
}