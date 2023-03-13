using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.QuestSync
{
    internal unsafe static class ToDoListHelper
    {
        public const string AddonName = "_ToDoList";

        public static AtkUnitBase* Addon => (AtkUnitBase*)Svc.GameGui.GetAddonByName(AddonName, 1);

        public static ToDoItem[] ToDoItems => GetToDoItems();

        private static ToDoItem[] GetToDoItems()
        {
            if (Addon == null)
                return Array.Empty<ToDoItem>();

            var holdingList = new List<ToDoItem>();

            var Item1 = Addon->UldManager.NodeList[8];
            var Item1Description = Addon->UldManager.NodeList[22];

            if (Item1 == null || Item1Description == null)
                return holdingList.ToArray();

            ToDoItem item1 = new ToDoItem();
            item1.Name = Item1->GetComponent()->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText.ExtractText();
            item1.Description = Item1Description->GetComponent()->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText.ExtractText();
            holdingList.Add(item1);

            var Item2 = Addon->UldManager.NodeList[9];
            var Item2Description = Addon->UldManager.NodeList[21];

            if (Item2 == null || Item2Description == null)
                return holdingList.ToArray();

            ToDoItem item2 = new ToDoItem();
            item2.Name = Item2->GetComponent()->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText.ExtractText();
            item2.Description = Item2Description->GetComponent()->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText.ExtractText();
            holdingList.Add(item2);

            var Item3 = Addon->UldManager.NodeList[10];
            var Item3Description = Addon->UldManager.NodeList[20];

            if (Item3 == null || Item3Description == null)
                return holdingList.ToArray();

            ToDoItem item3 = new ToDoItem();
            item3.Name = Item3->GetComponent()->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText.ExtractText();
            item3.Description = Item3Description->GetComponent()->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText.ExtractText();
            holdingList.Add(item3);

            return holdingList.ToArray();

        }
    }

    public class ToDoItem
    {
        public string Name;
        public string Description;
    }
}
