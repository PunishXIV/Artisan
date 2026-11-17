using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.FCWorkshops;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static ECommons.GenericHelpers;

namespace Artisan
{
    internal class RecipeWindowUI : Window
    {
        private static string search = string.Empty;
        private static bool searched = false;
        private static CraftMenuWindowUI? _craftMenuWindowUi = null;
        private static CraftMenuWindowUI? _cosmicCraftMenuWindowUi = null;

        internal static string Search
        {
            get => search;
            set
            {
                if (search != value)
                {
                    search = value;
                    searched = false;
                }
            }
        }

        public unsafe bool WindowVisible()
        {
            if (TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon))
            {
                return addon->AtkUnitBase.IsVisible;
            }
            if (TryGetAddonByName<AddonGrandCompanySupplyList>("WKSRecipeNotebook", out var addon2))
            {
                return addon2->AtkUnitBase.IsVisible;
            }

            return false;
        }

        private RecipeWindowUI() : base("###RecipeWindow", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing)
        {
            Size = new Vector2(0, 0);
            Position = new Vector2(0, 0);
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            DisableWindowSounds = true;
            SizeConstraints = new WindowSizeConstraints()
            {
                MaximumSize = new Vector2(0, 0),
            };
        }

        public override void Draw()
        {
            try
            {
                if (!WindowVisible() || !GenericHelpers.IsScreenReady())
                {
                    HideCraftingMenuWindow();
                    HideCosmicCraftingMenuWindow();
                    return;
                }

                DrawOptions();
                DrawSearchReplace();
                DrawCosmicWindowOptions();
                DrawRecipeCompletion();
            }
            catch { }
        }

        private unsafe void DrawRecipeCompletion()
        {
            var curPage = AgentRecipeNote.Instance()->SelectedRecipeCategoryPage;

            if (AgentRecipeNote.Instance()->RecipeSearchOpen || (!P.Config.ShowLevelingRecipeProgress && !P.Config.ShowOtherRecipeProgress) || curPage > 1)
                return;

            if ((!P.Config.ShowLevelingRecipeProgress && curPage == 0) || (!P.Config.ShowOtherRecipeProgress && curPage == 1))
                return;

            try
            {
                var recipeWindow = Svc.GameGui.GetAddonByName("RecipeNote", 1);
                if (recipeWindow == IntPtr.Zero)
                    return;

                var addonPtr = (AtkUnitBase*)recipeWindow.Address;
                if (addonPtr == null)
                    return;

                var n = addonPtr->GetNodeById(39)->GetAsAtkComponentTreeList();
                if (n == null)
                    return;

                RecipeInformation.UpdateCompletedRecipes();
                var job = (Job)AgentRecipeNote.Instance()->SelectedCraftType + 8;
                var jobLevel = CharacterInfo.JobLevel(job);
                var filteredList = RecipeInformation.CompletedRecipes.Where(x => x.Key.Job == job).ToDictionary(x => x.Key.DivisionID, x => x.Value);

                uint visited = 0;
                var toVisit = Svc.Data.GetExcelSheet<NotebookDivision>().Where(x => x.RowId < 1000 && x.CraftOpeningLevel <= jobLevel && x.NotebookDivisionCategory.RowId == 0).LastOrDefault().RowId;

                foreach (var subNode in n->UldManager.Nodes)
                {
                    var sn = (AtkComponentNode*)subNode.Value;
                    var info = sn->Component->UldManager;
                    var oinfo = (AtkUldComponentInfo*)info.Objects;

                    if (oinfo->ComponentType is ComponentType.ListItemRenderer)
                    {
                        var compNode = sn->Component;
                        var textNode = compNode->GetNodeById(4);
                        if (textNode == null || textNode->Type is not NodeType.Text)
                            continue;

                        if (curPage == 0) // leveling
                        {
                            uint bracket = toVisit - visited;
                            if (bracket > toVisit)
                                continue;

                            if (filteredList.TryGetFirst(x => x.Key == bracket, out var entry))
                            {
                                var label = Svc.Data.GetExcelSheet<NotebookDivision>().GetRow(bracket).Name.ToString();

                                if (entry.Value.Completed == entry.Value.Total)
                                    textNode->GetAsAtkTextNode()->SetText($"{label} ✓");
                                else
                                    textNode->GetAsAtkTextNode()->SetText($"{label} [{entry.Value.Completed}/{entry.Value.Total}]");

                            }
                            visited++;
                        }
                        else if (curPage == 1) // special recipes
                        {
                            var curLabel = textNode->GetAsAtkTextNode()->NodeText.GetText();
                            foreach (var (divisionId, v) in filteredList)
                            {
                                var divLabel = Svc.Data.GetExcelSheet<NotebookDivision>().GetRow(divisionId).Name.ToString();
                                if (curLabel == divLabel)
                                {
                                    var noParenLabel = curLabel.Replace("(", "").Replace(")", "");
                                    var trimLabel = noParenLabel.Length > 12 ? noParenLabel.Substring(0, 12) + "..." : noParenLabel;

                                    if (v.Completed == v.Total)
                                        textNode->GetAsAtkTextNode()->SetText($"{trimLabel} ✓");
                                    else
                                        textNode->GetAsAtkTextNode()->SetText($"{trimLabel} [{v.Completed}/{v.Total}]");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        public static RecipeWindowUI Create()
        {
            return new();
        }

        private unsafe void DrawCosmicEnduranceCounter()
        {
            if (Endurance.RecipeID == 0)
                return;

            var recipeWindow = Svc.GameGui.GetAddonByName("WKSRecipeNotebook", 1);
            if (recipeWindow == IntPtr.Zero)
                return;

            var addonPtr = (AtkUnitBase*)recipeWindow.Address;
            if (addonPtr == null)
                return;

            if (addonPtr->UldManager.NodeListCount >= 5)
            {
                var node = addonPtr->GetNodeById(50);
                var position = AtkResNodeFunctions.GetNodePosition(node);
                var scale = AtkResNodeFunctions.GetNodeScale(node);
                var text = addonPtr->GetNodeById(34)->GetAsAtkTextNode()->NodeText.ToString();
                var craftableCount = text == "" ? 0 : Convert.ToInt32(text.GetNumbers());

                if (craftableCount == 0) return;

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X - 300f.Scale(), position.Y + 10f.Scale()));

                DrawCounter(node, scale, craftableCount);
            }

        }

        private unsafe void DrawCosmicWindowOptions()
        {
            if (!TryGetAddonByName<AddonRecipeNote>("WKSRecipeNotebook", out var recipeWindow))
            {
                HideCosmicCraftingMenuWindow();
                return;
            }

            if (recipeWindow->UldManager.NodeListCount >= 2 && recipeWindow->GetNodeById(54)->IsVisible())
            {
                var node = recipeWindow->GetNodeById(54);

                ShowCosmicCraftMenuWindow();

                var position = AtkResNodeFunctions.GetNodePosition(node);
                var scale = AtkResNodeFunctions.GetNodeScale(node);
                var size = new Vector2(node->Width, node->Height) * scale;

                if (P.Config.LockMiniMenuR || AtkResNodeFunctions.ResetPosition)
                {
                    if ((AtkResNodeFunctions.ResetPosition && position.X != 0) || P.Config.LockMiniMenuR)
                    {
                        _cosmicCraftMenuWindowUi.ForceMainWindow = true;
                        _cosmicCraftMenuWindowUi.PositionCondition = ImGuiCond.Always;
                        _cosmicCraftMenuWindowUi.Position = new Vector2(position.X + size.X + 7, position.Y + 7);
                        AtkResNodeFunctions.ResetPosition = false;
                    }
                }
                else
                {
                    _cosmicCraftMenuWindowUi.ForceMainWindow = false;
                    _cosmicCraftMenuWindowUi.PositionCondition = ImGuiCond.FirstUseEver;
                    _cosmicCraftMenuWindowUi.Position = new Vector2(position.X + size.X + 7, position.Y + 7);
                }
            }
        }

        private unsafe void DrawSearchReplace()
        {
            if (TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon))
            {
                if (!addon->AtkUnitBase.IsVisible)
                {
                    Search = "";
                    return;
                }
                var searchNode = addon->AtkUnitBase.GetNodeById(26);
                var searchLabel = addon->AtkUnitBase.GetNodeById(25);
                if (searchNode == null || searchLabel == null) return;

                if (P.Config.ReplaceSearch)
                {
                    searchLabel->GetAsAtkTextNode()->SetText("Artisan Search");
                }
                else
                {
                    string searchText = Svc.Data.Excel.GetSheet<Addon>().GetRow(1412).Text.GetText();
                    searchLabel->GetAsAtkTextNode()->SetText(searchText);
                    return;
                }

                var textInput = (AtkComponentTextInput*)searchNode->GetComponent();
                Search = Marshal.PtrToStringAnsi(new IntPtr(textInput->AtkComponentInputBase.EvaluatedString.StringPtr)).Trim();
                ImGui.CalcTextSize(Search);

                var position = AtkResNodeFunctions.GetNodePosition(searchNode);
                var scale = AtkResNodeFunctions.GetNodeScale(searchNode);
                var size = new Vector2(searchNode->Width, searchNode->Height) * scale;

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X, position.Y + size.Y));

                try
                {
                    var compNode = (AtkComponentNode*)searchNode;
                    if (compNode->Component->UldManager.SearchNodeById(18) == null) return;

                    searched = !compNode->Component->UldManager.SearchNodeById(18)->IsVisible();

                    if (Search.Length > 0 && !searched)
                    {
                        if (LuminaSheets.RecipeSheet.Values.Count(x => Regex.Match(x.ItemResult.Value.Name.ToDalamudString().ToString(), Search, RegexOptions.IgnoreCase).Success) > 0)
                        {
                            ImGui.Begin($"###Search{searchNode->NodeId}", ImGuiWindowFlags.NoScrollbar
                                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

                            ImGui.AlignTextToFramePadding();
                            ImGui.SetNextItemWidth(size.Length() - 12f);

                            int results = 0;
                            foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => Regex.Match(x.ItemResult.Value.Name.ToDalamudString().ToString(), Search, RegexOptions.IgnoreCase).Success))
                            {
                                if (results >= 24) continue;
                                var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.ToDalamudString()} ({(Job)recipe.CraftType.RowId + 8})###{recipe.RowId}");
                                if (selected)
                                {
                                    var orid = Operations.GetSelectedRecipeEntry();
                                    if (orid == null || orid->RecipeId != recipe.RowId)
                                    {
                                        AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RowId);
                                    }

                                    searched = true;
                                }
                                results++;
                            }
                            ImGui.End();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not RegexParseException)
                        ex.Log();
                }
            }
        }

        internal static unsafe void CreateGCListAgent(AtkUnitBase* atkUnitBase, bool withSubcrafts, bool boostedCraftsOnly)
        {
            CreateGCList(atkUnitBase, withSubcrafts, boostedCraftsOnly, 425, 432, -40, -360, -400);
        }

        private static uint TextureIdToJob(int textureId)
        {
            return textureId switch
            {
                62008 => 8,
                62009 => 9,
                62010 => 10,
                62011 => 11,
                62012 => 12,
                62013 => 13,
                62014 => 14,
                62015 => 15,
                _ => 0
            };
        }

        internal static unsafe void CreateGCList(AtkUnitBase* atkUnitBase, bool withSubcrafts, bool boostedCraftOnly)
        {
            CreateGCList(atkUnitBase, withSubcrafts, boostedCraftOnly, 233, 240, 16, 8, 40);
        }

        private static unsafe void CreateGCList(AtkUnitBase* atkUnitBase, bool withSubcrafts, bool boostedCraftOnly, int firstIndex, int lastIndex, int requestedOffset, int jobOffset, int starredOffset)
        {
            var craftingList = new NewCraftingList
            {
                Name = $"GC Supply List ({DateTime.Now.ToShortDateString()})"
            };

            for (int i = firstIndex; i <= lastIndex; i++)
            {
                if (atkUnitBase->AtkValues[i].Type == 0)
                    continue;

                var itemId = atkUnitBase->AtkValues[i].Int;
                var requested = atkUnitBase->AtkValues[i + requestedOffset].Int;
                var job = TextureIdToJob(atkUnitBase->AtkValues[i + jobOffset].Int);
                var starred = atkUnitBase->AtkValues[i + starredOffset].Byte == 1;

                if (boostedCraftOnly && !starred)
                    continue;

                if (!LuminaSheets.RecipeSheet.Values.TryGetFirst(x => x.ItemResult.RowId == itemId && x.CraftType.RowId + 8 == job, out var recipe))
                    continue;

                var timesToAdd = requested / recipe.AmountResult;
                if (withSubcrafts)
                    CraftingListUI.AddAllSubcrafts(recipe, craftingList, timesToAdd);

                var existingRecipe = craftingList.Recipes.FirstOrDefault(x => x.ID == recipe.RowId);
                if (existingRecipe != null)
                    existingRecipe.Quantity = timesToAdd;
                else
                    craftingList.Recipes.Add(new() { Quantity = timesToAdd, ID = recipe.RowId });
            }

            craftingList.SetID();
            craftingList.Save(true);
            Notify.Success("Crafting List Created");
        }

        internal static unsafe void CreateWorkshopPhaseList(AtkUnitBase* addonPtr, bool includePrecrafts)
        {
            var itemName1 = addonPtr->GetTextNodeById(4)->NodeText.GetText();
            var itemName2 = addonPtr->GetTextNodeById(6)->NodeText.GetText();
            var project = LuminaSheets.WorkshopSequenceSheet.Values.FirstOrDefault(x => x.ResultItem.Value.Name.GetText() == itemName1 || x.ResultItem.Value.Name.GetText() == itemName2);
            if (project.RowId == 0)
                return;

            var phaseNum = Convert.ToInt32(addonPtr->GetTextNodeById(16)->NodeText.ToString().First().ToString());
            var matchingParts = project.CompanyCraftPart.Where(x => x.RowId > 0);
            var hasSinglePart = matchingParts.Count() == 1;
            var partStep = hasSinglePart ? string.Empty : addonPtr->GetTextNodeById(13)->NodeText.GetText().Split(":").Last();
            if (!hasSinglePart && !matchingParts.Any(x => x.Value.CompanyCraftType.Value.Name.GetText() == partStep))
                return;

            var part = hasSinglePart
                ? matchingParts.First().Value
                : matchingParts.First(x => x.Value.CompanyCraftType.Value.Name.GetText() == partStep).Value;
            var phase = part.CompanyCraftProcess[phaseNum - 1];
            FCWorkshopUI.CreatePhaseList(phase.Value!, part.CompanyCraftType.Value.Name.GetText(), phaseNum, includePrecrafts, null, project);
            Notify.Success("FC Workshop List Created");
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
            {
                P.Style.Push();
                P.StylePushed = true;
            }
        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                P.StylePushed = false;
            }
        }

        public override void OnClose()
        {
            HideCraftingMenuWindow();
            HideCosmicCraftingMenuWindow();

            base.OnClose();
        }




        public static unsafe void DrawOptions()
        {
            if (!TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var recipeWindow))
            {
                HideCraftingMenuWindow();
                return;
            }

            if (recipeWindow->UldManager.NodeListCount > 1)
            {
                if (recipeWindow->GetNodeById(109)->IsVisible())//1
                {
                    var node = recipeWindow->GetNodeById(109);
                    ShowCraftMenuWindow();

                    var position = AtkResNodeFunctions.GetNodePosition(node);
                    var scale = AtkResNodeFunctions.GetNodeScale(node);
                    var size = new Vector2(node->Width, node->Height) * scale;

                    if (P.Config.LockMiniMenuR || AtkResNodeFunctions.ResetPosition)
                    {
                        if ((AtkResNodeFunctions.ResetPosition && position.X != 0) || P.Config.LockMiniMenuR)
                        {
                            _craftMenuWindowUi.ForceMainWindow = true;
                            _craftMenuWindowUi.PositionCondition = ImGuiCond.Always;
                            _craftMenuWindowUi.Position = new Vector2(position.X + size.X + 7, position.Y + 7);
                            AtkResNodeFunctions.ResetPosition = false;
                        }
                    }
                    else
                    {
                        _craftMenuWindowUi.ForceMainWindow = false;
                        _craftMenuWindowUi.PositionCondition = ImGuiCond.FirstUseEver;
                        _craftMenuWindowUi.Position = new Vector2(position.X + size.X + 7, position.Y + 7);
                    }
                }
            }

        }

        internal static unsafe void DrawEnduranceCounter()
        {
            if (Endurance.RecipeID == 0)
                return;

            var recipeWindow = Svc.GameGui.GetAddonByName("RecipeNote", 1);
            if (recipeWindow == IntPtr.Zero)
                return;

            var addonPtr = (AtkUnitBase*)recipeWindow.Address;
            if (addonPtr == null)
                return;

            if (addonPtr->UldManager.NodeListCount >= 5)
            {
                var node = addonPtr->GetNodeById(103);

                var position = AtkResNodeFunctions.GetNodePosition(node);
                var scale = AtkResNodeFunctions.GetNodeScale(node);
                var text = addonPtr->GetTextNodeById(78)->NodeText.ToString();
                var craftableCount = text == "" ? 0 : Convert.ToInt32(text.GetNumbers());

                if (craftableCount == 0) return;

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + (4f * scale.X) - 40f, position.Y + 16f + (17f * scale.Y)));

                DrawCounter(node, scale, craftableCount);
            }
        }

        private static unsafe void DrawCounter(AtkResNode* node, Vector2 scale, int craftableCount)
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5f, 2.5f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f, 3f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

            ImGui.Begin($"###Repeat{node->NodeId}", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

            var oldScale = ImGui.GetIO().FontGlobalScale;
            ImGui.GetIO().FontGlobalScale = 1f * scale.X;
            using (ImRaii.PushFont(ImGui.GetFont()))
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Craft X Times:");
                ImGui.SameLine();
                ImGui.PushItemWidth(110f * scale.X);
                if (ImGui.InputInt($"###TimesRepeat{node->NodeId}", ref P.Config.CraftX, step: 1, stepFast: 1))
                {
                    if (P.Config.CraftX < 0)
                        P.Config.CraftX = 0;

                    if (P.Config.CraftX > craftableCount)
                        P.Config.CraftX = craftableCount;

                }
                ImGui.SameLine();
                if (P.Config.CraftX > 0)
                {
                    if (ImGui.Button($"Craft {P.Config.CraftX}"))
                    {
                        P.Config.CraftingX = true;
                        Endurance.ToggleEndurance(true);
                    }
                }
                else
                {
                    if (ImGui.Button($"Craft All ({craftableCount})"))
                    {
                        P.Config.CraftX = craftableCount;
                        P.Config.CraftingX = true;
                        Endurance.ToggleEndurance(true);
                    }
                }

                ImGui.GetIO().FontGlobalScale = oldScale;
            }

            ImGui.End();
            ImGui.PopStyleVar(5);
            ImGui.PopStyleColor();
        }

        private static void ShowCraftMenuWindow()
        {
            _craftMenuWindowUi.Flags = GetWindowFlags();
            _craftMenuWindowUi.IsOpen = true;
        }

        public static CraftMenuWindowUI AddCraftMenuWindow()
        {
            _craftMenuWindowUi = new CraftMenuWindowUI("Artisan Crafting Window###CraftMenuWindow", GetWindowFlags());
            return _craftMenuWindowUi;
        }

        private static void HideCraftingMenuWindow()
        {
            if (_craftMenuWindowUi == null)
            {
                return;
            }

            _craftMenuWindowUi.IsOpen = false;
        }

        private static void ShowCosmicCraftMenuWindow()
        {
            _cosmicCraftMenuWindowUi.Flags = GetWindowFlags();
            _cosmicCraftMenuWindowUi.IsOpen = true;
        }

        public static CraftMenuWindowUI AddCosmicCraftMenuWindow()
        {
            _cosmicCraftMenuWindowUi = new CraftMenuWindowUI("Artisan Crafting Window###CosmicCraftMenuWindow", GetWindowFlags());
            return _cosmicCraftMenuWindowUi;
        }

        private static void HideCosmicCraftingMenuWindow()
        {
            if (_cosmicCraftMenuWindowUi == null)
            {
                return;
            }

            _cosmicCraftMenuWindowUi.IsOpen = false;
        }

        private static ImGuiWindowFlags GetWindowFlags()
        {
            var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.AlwaysUseWindowPadding;
            if (P.Config.PinMiniMenu)
                flags |= ImGuiWindowFlags.NoMove;

            return flags;
        }
    }
}
