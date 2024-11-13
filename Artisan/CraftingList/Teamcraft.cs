using Artisan.RawInformation;
using Artisan.UI;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using PunishLib.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows.Forms;

namespace Artisan.CraftingLists
{
    internal static class Teamcraft
    {
        internal static string importListName = "";
        internal static string importListPreCraft = "";
        internal static string importListItems = "";
        internal static bool openImportWindow = false;
        private static bool precraftQS = false;
        private static bool finalitemQS = false;

        internal static void DrawTeamCraftListButtons()
        {
            string labelText = "Teamcraft Lists";
            var labelLength = ImGui.CalcTextSize(labelText);
            ImGui.SetCursorPosX((ImGui.GetContentRegionMax().X - labelLength.X) * 0.5f);
            ImGui.TextColored(ImGuiColors.ParsedGreen, labelText);
            if (IconButtons.IconTextButton(Dalamud.Interface.FontAwesomeIcon.Download, "Import", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                openImportWindow = true;
            }
            OpenTeamcraftImportWindow();
            if (CraftingListUI.selectedList.ID != 0)
            {
                if (IconButtons.IconTextButton(Dalamud.Interface.FontAwesomeIcon.Upload, "Export", new Vector2(ImGui.GetContentRegionAvail().X, 30), true))
                {
                    ExportSelectedListToTC();
                }
            }
        }

        private static void ExportSelectedListToTC()
        {
            string baseUrl = "https://ffxivteamcraft.com/import/";
            string exportItems = "";

            var sublist = CraftingListUI.selectedList.Recipes.Distinct().Reverse().ToList();
            for (int i = 0; i < sublist.Count; i++)
            {
                if (i >= sublist.Count) break;

                int number = CraftingListUI.selectedList.Recipes[i].Quantity;
                var recipe = LuminaSheets.RecipeSheet[sublist[i].ID];
                var ItemId = recipe.ItemResult.Value.RowId;

                Svc.Log.Debug($"{recipe.ItemResult.Value.Name.RawString} {sublist.Count}");
                ExtractRecipes(sublist, recipe);
            }

            foreach (var item in sublist)
            {
                int number = item.Quantity;
                var recipe = LuminaSheets.RecipeSheet[item.ID];
                var ItemId = recipe.ItemResult.Value.RowId;

                exportItems += $"{ItemId},null,{number};";
            }

            exportItems = exportItems.TrimEnd(';');

            var plainTextBytes = Encoding.UTF8.GetBytes(exportItems);
            string base64 = Convert.ToBase64String(plainTextBytes);

            Svc.Log.Debug($"{baseUrl}{base64}");
            Clipboard.SetText($"{baseUrl}{base64}");
            Notify.Success("Link copied to clipboard");
        }

        private static void ExtractRecipes(List<ListItem> sublist, Recipe recipe)
        {
            foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0))
            {
                var subRec = CraftingListHelpers.GetIngredientRecipe((uint)ing.ItemIngredient);
                if (subRec != null)
                {
                    if (sublist.Any(x => x.ID == subRec.RowId))
                    {
                        foreach (var subIng in subRec.UnkData5.Where(x => x.AmountIngredient > 0))
                        {
                            var subSubRec = CraftingListHelpers.GetIngredientRecipe((uint)subIng.ItemIngredient);
                            if (subSubRec != null)
                            {
                                if (sublist.Any(x => x.ID == subSubRec.RowId))
                                {
                                    sublist.RemoveAll(x => x.ID == subSubRec.RowId);
                                }
                            }
                        }

                        sublist.RemoveAll(x => x.ID == subRec.RowId);
                    }
                }
            }
        }

        private static void OpenTeamcraftImportWindow()
        {
            if (!openImportWindow) return;


            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.2f, 0.1f, 0.2f, 1f));
            ImGui.SetNextWindowSize(new Vector2(1, 1), ImGuiCond.Appearing);
            if (ImGui.Begin("Teamcraft Import###TCImport", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("List Name");
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("Guide to importing lists.\r\n\r\n" +
                    "Step 1. Open a list on Teamcraft with the items you wish to craft.\r\n\r\n" +
                    "Step 2. Find the pre crafts section and click the \"Copy as Text\" button.\r\n\r\n" +
                    "Step 3. Paste into the Pre-Craft Items box in this window.\r\n\r\n" +
                    "Step 4. Repeat Step 2 & 3 but for the final items section.\r\n\r\n" +
                    "Step 5. Give your list a name and click import.");
                ImGui.InputText("###ImportListName", ref importListName, 50);
                ImGui.Text("Pre-craft Items");
                ImGui.InputTextMultiline("###PrecraftItems", ref importListPreCraft, 5000000, new Vector2(ImGui.GetContentRegionAvail().X, 100));

                if (!P.Config.DefaultListQuickSynth)
                    ImGui.Checkbox("Import as Quick Synth###ImportQSPre", ref precraftQS);
                else
                    ImGui.TextWrapped($@"These items will try to be added as quick synth due to the default setting being enabled.");
                ImGui.Text("Final Items");
                ImGui.InputTextMultiline("###FinalItems", ref importListItems, 5000000, new Vector2(ImGui.GetContentRegionAvail().X, 100));
                if (!P.Config.DefaultListQuickSynth)
                    ImGui.Checkbox("Import as Quick Synth###ImportQSFinal", ref finalitemQS);
                else
                    ImGui.TextWrapped($@"These items will try to be added as quick synth due to the default setting being enabled.");

                if (ImGui.Button("Import"))
                {
                    NewCraftingList? importedList = ParseImport(precraftQS, finalitemQS);
                    if (importedList is not null)
                    {
                        if (importedList.Name.IsNullOrEmpty())
                            importedList.Name = importedList.Recipes.FirstOrDefault().ID.NameOfRecipe();
                        importedList.SetID();
                        importedList.Save();
                        openImportWindow = false;
                        importListName = "";
                        importListPreCraft = "";
                        importListItems = "";

                    }
                    else
                    {
                        Notify.Error("The imported list has no items. Please check your import and try again.");
                    }

                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    openImportWindow = false;
                    importListName = "";
                    importListPreCraft = "";
                    importListItems = "";
                }
                ImGui.End();
            }
            ImGui.PopStyleColor();
        }

        private static NewCraftingList? ParseImport(bool precraftQS, bool finalitemQS)
        {
            if (string.IsNullOrEmpty(importListName) && string.IsNullOrEmpty(importListItems) && string.IsNullOrEmpty(importListPreCraft)) return null;
            NewCraftingList output = new NewCraftingList();
            output.Name = importListName;
            using (System.IO.StringReader reader = new System.IO.StringReader(importListPreCraft))
            {
                string line = "";
                while ((line = reader.ReadLine()!) != null)
                {
                    var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    if (parts[0][^1] == 'x')
                    {
                        int numberOfItem = int.Parse(parts[0].Substring(0, parts[0].Length - 1));
                        var builder = new StringBuilder();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            builder.Append(parts[i]);
                            builder.Append(" ");
                        }
                        var item = builder.ToString().Trim();
                        Svc.Log.Debug($"{numberOfItem} x {item}");

                        var recipe = LuminaSheets.RecipeSheet?.Where(x => x.Value.ItemResult.Row > 0 && x.Value.ItemResult.Value.Name.RawString == item).Select(x => x.Value).FirstOrDefault();
                        if (recipe is not null)
                        {
                            int quantity = (int)Math.Ceiling(numberOfItem / (double)recipe.AmountResult);
                            if (output.Recipes.Any(x => x.ID == recipe.RowId))
                                output.Recipes.First(x => x.ID == recipe.RowId).Quantity += quantity;
                            else
                                output.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = quantity, ListItemOptions = new() });

                            if (precraftQS && recipe.CanQuickSynth)
                                output.Recipes.First(x => x.ID == recipe.RowId).ListItemOptions.NQOnly = true;
                        }
                    }

                }
            }
            using (System.IO.StringReader reader = new System.IO.StringReader(importListItems))
            {
                string line = "";
                while ((line = reader.ReadLine()!) != null)
                {
                    var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    if (parts[0][^1] == 'x')
                    {
                        int numberOfItem = int.Parse(parts[0].Substring(0, parts[0].Length - 1));
                        var builder = new StringBuilder();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            builder.Append(parts[i]);
                            builder.Append(" ");
                        }
                        var item = builder.ToString().Trim();
                        if (DebugTab.Debug) Svc.Log.Debug($"{numberOfItem} x {item}");

                        var recipe = LuminaSheets.RecipeSheet?.Where(x => x.Value.ItemResult.Row > 0 && x.Value.ItemResult.Value.Name.RawString == item).Select(x => x.Value).FirstOrDefault();
                        if (recipe is not null)
                        {
                            int quantity = (int)Math.Ceiling(numberOfItem / (double)recipe.AmountResult);
                            output.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = quantity, ListItemOptions = new() });

                            if (finalitemQS && recipe.CanQuickSynth)
                                output.Recipes.First(x => x.ID == recipe.RowId).ListItemOptions.NQOnly = true;
                        }
                    }

                }
            }

            if (output.Recipes.Count == 0) return null;

            return output;
        }
    }
}
