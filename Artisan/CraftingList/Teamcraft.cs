using Artisan.Autocraft;
using Artisan.RawInformation;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using ImGuiNET;
using PunishLib.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.CraftingLists
{
    internal static class Teamcraft
    {
        internal static string importListName = "";
        internal static string importListPreCraft = "";
        internal static string importListItems = "";
        internal static bool openImportWindow = false;

        internal static void DrawTeamCraftListButtons()
        {
            ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - 90);
            if (ImGui.BeginChild("###TeamCraftSection", new Vector2(0, 0), false))
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
                ImGui.EndChild();
            }
        }

        private static void ExportSelectedListToTC()
        {
            string baseUrl = "https://ffxivteamcraft.com/import/";
            string exportItems = "";

            var sublist = CraftingListUI.selectedList.Items.Distinct().Reverse().ToList();
            for (int i = 0; i < sublist.Count(); i++)
            {
                if (i >= sublist.Count()) break;

                int number = CraftingListUI.selectedList.Items.Count(x => x == sublist[i]);
                var recipe = CraftingListUI.FilteredList[sublist[i]];
                var itemID = recipe.ItemResult.Value.RowId;

                Dalamud.Logging.PluginLog.Debug($"{recipe.ItemResult.Value.Name.RawString} {sublist.Count()}");
                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0))
                {
                    var subRec = CraftingListUI.GetIngredientRecipe(ing.ItemIngredient);
                    if (sublist.Contains(subRec.RowId))
                    {
                        foreach (var subIng in subRec.UnkData5.Where(x => x.AmountIngredient > 0))
                        {
                            var subSubRec = CraftingListUI.GetIngredientRecipe(subIng.ItemIngredient);
                            if (sublist.Contains(subSubRec.RowId))
                            {
                                for (int y = 1; y <= subIng.AmountIngredient; y++)
                                {
                                    sublist.Remove(subSubRec.RowId);
                                }
                            }
                        }

                        for (int y = 1; y <= ing.AmountIngredient; y++)
                        {
                            sublist.Remove(subRec.RowId);
                        }
                    }
                }
            }

            foreach (var item in sublist)
            {
                int number = CraftingListUI.selectedList.Items.Count(x => x == item);
                var recipe = CraftingListUI.FilteredList[item];
                var itemID = recipe.ItemResult.Value.RowId;

                exportItems += $"{itemID},null,{number};";
            }

            exportItems = exportItems.TrimEnd(';');

            var plainTextBytes = Encoding.UTF8.GetBytes(exportItems);
            string base64 = Convert.ToBase64String(plainTextBytes);

            ImGui.SetClipboardText($"{baseUrl}{base64}");
            Notify.Success("Link copied to clipboard");
        }

        private static void OpenTeamcraftImportWindow()
        {
            if (!openImportWindow) return;


            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.2f, 0.1f, 0.2f, 1f));
            if (ImGui.Begin("Teamcraft Import", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize))
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
                ImGui.InputTextMultiline("###PrecraftItems", ref importListPreCraft, 10000000, new Vector2(ImGui.GetContentRegionAvail().X, 100));
                ImGui.Text("Final Items");
                ImGui.InputTextMultiline("###FinalItems", ref importListItems, 10000000, new Vector2(ImGui.GetContentRegionAvail().X, 100));


                if (ImGui.Button("Import"))
                {
                    CraftingList? importedList = ParseImport();
                    if (importedList is not null)
                    {
                        importedList.SetID();
                        importedList.Save();
                        openImportWindow = false;
                        importListName = "";
                        importListPreCraft = "";
                        importListItems = "";

                    }
                    else
                    {
                        Notify.Error("Something has gone wrong with importing. Please check you have filled everything out correctly.");
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

        private static CraftingList? ParseImport()
        {
            if (string.IsNullOrEmpty(importListName) || string.IsNullOrEmpty(importListItems) || string.IsNullOrEmpty(importListPreCraft)) return null;
            CraftingList output = new CraftingList();
            output.Name = importListName;
            using (System.IO.StringReader reader = new System.IO.StringReader(importListPreCraft))
            {
                string line = "";
                while ((line = reader.ReadLine()) != null)
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
                        Dalamud.Logging.PluginLog.Debug($"{numberOfItem} x {item}");

                        var recipe = LuminaSheets.RecipeSheet?.Where(x => x.Value.ItemResult.Value.Name.RawString == item).Select(x => x.Value).FirstOrDefault();
                        if (recipe is not null)
                        {
                            for (int i = 1; i <= Math.Ceiling((double)numberOfItem / (double)recipe.AmountResult); i++)
                            {
                                output.Items.Add(recipe.RowId);
                            }
                        }
                    }

                }
            }
            using (System.IO.StringReader reader = new System.IO.StringReader(importListItems))
            {
                string line = "";
                while ((line = reader.ReadLine()) != null)
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
                        if (AutocraftDebugTab.Debug) Dalamud.Logging.PluginLog.Debug($"{numberOfItem} x {item}");

                        var recipe = LuminaSheets.RecipeSheet?.Where(x => x.Value.ItemResult.Value.Name.RawString == item).Select(x => x.Value).FirstOrDefault();
                        if (recipe is not null)
                        {
                            for (int i = 1; i <= Math.Ceiling((double)numberOfItem / (double)recipe.AmountResult); i++)
                            {
                                output.Items.Add(recipe.RowId);
                            }
                        }
                    }

                }
            }

            if (output.Items.Count == 0) return null;

            return output;
        }
    }
}
