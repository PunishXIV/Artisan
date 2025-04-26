using Artisan.RawInformation.Character;
using System;
using System.Collections.Generic;

namespace Artisan.CraftingLogic.Solvers;

public class MacroSolverSettings
{
    public class Macro
    {
        public int ID;
        public string Name = "";
        public MacroOptions Options = new();
        public List<MacroStep> Steps = new();
    }

    public class MacroOptions
    {
        public bool SkipQualityIfMet = false;
        public bool UpgradeQualityActions = false;
        public bool UpgradeProgressActions = false;
        public bool SkipObservesIfNotPoor = false;
        public int MinCraftsmanship = 0;
        public int ExactCraftsmanship = 0;
        public int MinControl = 0;
        public int MinCP = 0;
    }

    public class MacroStep
    {
        public Skills Action;
        public bool ExcludeFromUpgrade = false;
        public bool ExcludeNormal = false;
        public bool ExcludePoor = false;
        public bool ExcludeGood = false;
        public bool ExcludeExcellent = false;
        public bool ExcludeCentered = false;
        public bool ExcludeSturdy = false;
        public bool ExcludePliant = false;
        public bool ExcludeMalleable = false;
        public bool ExcludePrimed = false;
        public bool ExcludeGoodOmen = false;
        public bool ReplaceOnExclude = false;
        public Skills ReplacementAction = Skills.None;

        public bool HasExcludeCondition =>  ExcludeNormal ||
                                            ExcludeGood ||
                                            ExcludePoor  ||
                                            ExcludeExcellent ||
                                            ExcludeCentered ||
                                            ExcludeSturdy ||
                                            ExcludePliant||
                                            ExcludeMalleable ||
                                            ExcludePrimed ||
                                            ExcludeGoodOmen;
    }

    public List<Macro> Macros = new();

    public void AddNewMacro(Macro macro)
    {
        var rng = new Random();
        macro.ID = rng.Next(1, 50000);
        while (FindMacro(macro.ID) != null)
            macro.ID = rng.Next(1, 50000);
        Macros.Add(macro);
    }

    public Macro? FindMacro(int id) => Macros.Find(m => m.ID == id);
}
