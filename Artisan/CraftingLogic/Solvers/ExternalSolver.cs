using Artisan.CraftingLogic.CraftData;
using Artisan.RawInformation.Character;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Artisan.CraftingLogic.Solvers
{
    [Flags]
    public enum CraftSupport
    {
        Normal = 1 << 0, // !CraftExpert && !IsCosmic
        Expert = 1 << 1, // CraftExpert && !IsCosmic
        Cosmic = 1 << 2, // IsCosmic
        All    = Normal | Expert | Cosmic,
    }

    public static class CraftFieldNames
    {
        // Player stats
        public const string UnlockedManipulation      = "UnlockedManipulation";
        public const string Specialist                = "Specialist";
        public const string SplendorCosmic            = "SplendorCosmic";

        // Recipe flags
        public const string CraftHQ                   = "CraftHQ";
        public const string CraftCollectible          = "CraftCollectible";
        public const string IshgardExpert             = "IshgardExpert";
        public const string IsCosmic                  = "IsCosmic";
        public const string MissionHasMaterialMiracle = "MissionHasMaterialMiracle";
        public const string MissionHasSteadyHand      = "MissionHasSteadyHand";

        // Recipe thresholds
        public const string CraftQualityMin1          = "CraftQualityMin1";
        public const string CraftQualityMin2          = "CraftQualityMin2";
        public const string CraftQualityMin3          = "CraftQualityMin3";
        public const string CraftRequiredQuality      = "CraftRequiredQuality";
        public const string CraftRecommendedCraftsmanship = "CraftRecommendedCraftsmanship";

        // Recipe context
        public const string ItemId                    = "ItemId";
        public const string RecipeId                  = "RecipeId";
        public const string InitialQuality            = "InitialQuality";
        public const string CurrentSteadyHandCharges  = "CurrentSteadyHandCharges";

        // Computed efficiency ratios
        public const string BaseProgress              = "BaseProgress";
        public const string BaseQuality               = "BaseQuality";
    }

    public class ExternalSolverDefinition : ISolverDefinition
    {
        public string Name { get; }
        private readonly Func<string, string> _callback;
        private readonly IReadOnlySet<string>? _requestedFields;
        private readonly CraftSupport _support;

        public ExternalSolverDefinition(string name, Func<string, string> callback, IReadOnlySet<string>? requestedFields = null, CraftSupport support = CraftSupport.All)
        {
            Name = name;
            _callback = callback;
            _requestedFields = requestedFields;
            _support = support;
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            yield return new(this, 0, 50, Name, GetUnsupportedReason(craft));
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours()
        {
            yield return new(this, 0, 50, Name);
        }

        public Solver Create(CraftState craft, int flavour) => new ExternalSolver(Name, _callback, _requestedFields);

        private string GetUnsupportedReason(CraftState craft)
        {
            if (craft.IsCosmic  && !_support.HasFlag(CraftSupport.Cosmic))  return "Does not support Cosmic recipes";
            if (craft.CraftExpert && !_support.HasFlag(CraftSupport.Expert)) return "Does not support Expert recipes";
            if (!craft.CraftExpert && !craft.IsCosmic && !_support.HasFlag(CraftSupport.Normal)) return "Does not support Normal recipes";
            return "";
        }
    }

    public class ExternalSolver : Solver
    {
        // Add entries here to make new fields available to external solvers.
        private static readonly Dictionary<string, Func<CraftState, StepState, JsonNode?>> FieldRegistry = new()
        {
            // Player stats
            [CraftFieldNames.UnlockedManipulation]          = (c, _) => JsonValue.Create(c.UnlockedManipulation),
            [CraftFieldNames.Specialist]                    = (c, _) => JsonValue.Create(c.Specialist),
            [CraftFieldNames.SplendorCosmic]                = (c, _) => JsonValue.Create(c.SplendorCosmic),
            // Recipe flags
            [CraftFieldNames.CraftHQ]                       = (c, _) => JsonValue.Create(c.CraftHQ),
            [CraftFieldNames.CraftCollectible]              = (c, _) => JsonValue.Create(c.CraftCollectible),
            [CraftFieldNames.IshgardExpert]                 = (c, _) => JsonValue.Create(c.IshgardExpert),
            [CraftFieldNames.IsCosmic]                      = (c, _) => JsonValue.Create(c.IsCosmic),
            [CraftFieldNames.MissionHasMaterialMiracle]     = (c, _) => JsonValue.Create(c.MissionHasMaterialMiracle),
            [CraftFieldNames.MissionHasSteadyHand]          = (c, _) => JsonValue.Create(c.MissionHasSteadyHand),
            // Recipe thresholds
            [CraftFieldNames.CraftQualityMin1]              = (c, _) => JsonValue.Create(c.CraftQualityMin1),
            [CraftFieldNames.CraftQualityMin2]              = (c, _) => JsonValue.Create(c.CraftQualityMin2),
            [CraftFieldNames.CraftQualityMin3]              = (c, _) => JsonValue.Create(c.CraftQualityMin3),
            [CraftFieldNames.CraftRequiredQuality]          = (c, _) => JsonValue.Create(c.CraftRequiredQuality),
            [CraftFieldNames.CraftRecommendedCraftsmanship] = (c, _) => JsonValue.Create(c.CraftRecommendedCraftsmanship),
            // Recipe context
            [CraftFieldNames.ItemId]                        = (c, _) => JsonValue.Create(c.ItemId),
            [CraftFieldNames.RecipeId]                      = (c, _) => JsonValue.Create(c.RecipeId),
            [CraftFieldNames.InitialQuality]                = (c, _) => JsonValue.Create(c.InitialQuality),
            [CraftFieldNames.CurrentSteadyHandCharges]      = (c, _) => JsonValue.Create(c.CurrentSteadyHandCharges),
            // Computed
            [CraftFieldNames.BaseProgress]                  = (c, _) => JsonValue.Create(Simulator.BaseProgress(c)),
            [CraftFieldNames.BaseQuality]                   = (c, _) => JsonValue.Create(Simulator.BaseQuality(c)),
        };

        internal static void ValidateFields(IReadOnlySet<string> fields)
        {
            var unknown = fields.Where(f => !FieldRegistry.ContainsKey(f)).ToList();
            if (unknown.Count > 0)
                throw new ArgumentException(
                    $"Unknown requested field(s): {string.Join(", ", unknown)}. " +
                    $"Available: {string.Join(", ", FieldRegistry.Keys)}");
        }

        private readonly string _name;
        private readonly Func<string, string> _callback;
        private readonly IReadOnlySet<string>? _requestedFields;

        public ExternalSolver(string name, Func<string, string> callback, IReadOnlySet<string>? requestedFields)
        {
            _name = name;
            _callback = callback;
            _requestedFields = requestedFields;
        }

        public override Recommendation Solve(CraftState craft, StepState step)
        {
            try
            {
                var stateJson = SerializeState(craft, step);
                var responseJson = _callback(stateJson);
                var skill = DeserializeSkill(responseJson);
                return new Recommendation(skill, $"[{_name}]");
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, $"[ExternalSolver:{_name}] Callback failed, falling back to BasicSynthesis");
                return new Recommendation(Skills.BasicSynthesis, $"[{_name}] fallback");
            }
        }

        private string SerializeState(CraftState craft, StepState step)
        {
            var craftObj = new JsonObject
            {
                ["StatCraftsmanship"] = craft.StatCraftsmanship,
                ["StatControl"]       = craft.StatControl,
                ["StatCP"]            = craft.StatCP,
                ["StatLevel"]         = craft.StatLevel,
                ["CraftLevel"]        = craft.CraftLevel,
                ["RecipeLevel"]       = (int)craft.LevelTable.RowId,
                ["CraftDurability"]   = craft.CraftDurability,
                ["CraftProgress"]     = craft.CraftProgress,
                ["CraftQualityMax"]   = craft.CraftQualityMax,
                ["CraftExpert"]       = craft.CraftExpert,
                ["ConditionFlags"]    = (int)craft.ConditionFlags,
            };

            if (_requestedFields != null)
            {
                foreach (var field in _requestedFields)
                {
                    if (FieldRegistry.TryGetValue(field, out var getter))
                        craftObj[field] = getter(craft, step);
                }
            }

            var root = new JsonObject
            {
                ["craft"] = craftObj,
                ["step"] = new JsonObject
                {
                    ["Index"]                      = step.Index,
                    ["Progress"]                   = step.Progress,
                    ["Quality"]                    = step.Quality,
                    ["Durability"]                 = step.Durability,
                    ["RemainingCP"]                = step.RemainingCP,
                    ["IQStacks"]                   = step.IQStacks,
                    ["Condition"]                  = step.Condition.ToString(),
                    ["PrevCondition"]              = step.PrevCondition.ToString(),
                    ["InnovationLeft"]             = step.InnovationLeft,
                    ["GreatStridesLeft"]           = step.GreatStridesLeft,
                    ["VenerationLeft"]             = step.VenerationLeft,
                    ["WasteNotLeft"]               = step.WasteNotLeft,
                    ["ManipulationLeft"]           = step.ManipulationLeft,
                    ["MuscleMemoryLeft"]           = step.MuscleMemoryLeft,
                    ["FinalAppraisalLeft"]         = step.FinalAppraisalLeft,
                    ["CarefulObservationLeft"]     = step.CarefulObservationLeft,
                    ["HeartAndSoulActive"]         = step.HeartAndSoulActive,
                    ["HeartAndSoulAvailable"]      = step.HeartAndSoulAvailable,
                    ["ExpedienceLeft"]             = step.ExpedienceLeft,
                    ["QuickInnoLeft"]              = step.QuickInnoLeft,
                    ["QuickInnoAvailable"]         = step.QuickInnoAvailable,
                    ["TrainedPerfectionAvailable"] = step.TrainedPerfectionAvailable,
                    ["TrainedPerfectionActive"]    = step.TrainedPerfectionActive,
                    ["PrevComboAction"]            = step.PrevComboAction.ToString(),
                    ["PrevActionFailed"]           = step.PrevActionFailed,
                    ["MaterialMiracleCharges"]     = step.MaterialMiracleCharges,
                    ["MaterialMiracleActive"]      = step.MaterialMiracleActive,
                    ["MaterialMiraclesUsed"]       = step.MaterialMiraclesUsed,
                    ["MaterialMiracleSecondsLeft"] = step.MaterialMiracleSecondsLeft,
                    ["ObserveCounter"]             = step.ObserveCounter,
                    ["ExpertEmergency"]            = step.ExpertEmergency,
                    ["SteadyHandCharges"]          = step.SteadyHandCharges,
                    ["SteadyHandLeft"]             = step.SteadyHandLeft,
                    ["SteadyHandsUsed"]            = step.SteadyHandsUsed,
                },
            };

            return root.ToJsonString();
        }

        private static Skills DeserializeSkill(string responseJson)
        {
            using var doc = JsonDocument.Parse(responseJson);
            var actionName = doc.RootElement.GetProperty("action").GetString() ?? "";

            if (Enum.TryParse<Skills>(actionName, out var skill))
                return skill;

            return actionName switch
            {
                "WasteNotII" => Skills.WasteNot2,
                _ => throw new InvalidOperationException($"Unknown skill name: '{actionName}'")
            };
        }
    }
}
