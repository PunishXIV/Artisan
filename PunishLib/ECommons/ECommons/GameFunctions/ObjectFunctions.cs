using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.GameFunctions
{
    public static unsafe class ObjectFunctions
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate long GetNameplateColorDelegate(IntPtr ptr);
        public static GetNameplateColorDelegate GetNameplateColor;

        public static FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* Struct(this GameObject o)
        {
            return (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)o.Address;
        }

        internal static void Init()
        {
            GetNameplateColor = Marshal.GetDelegateForFunctionPointer<GetNameplateColorDelegate>(Svc.SigScanner.ScanText("48 89 74 24 ?? 57 48 83 EC 20 48 8B 35 ?? ?? ?? ?? 48 8B F9 48 85 F6 75 0D"));
        }

        public static bool IsHostile(this GameObject a)
        {
            var plateType = GetNameplateColor(a.Address);
            //7: yellow, can be attacked, not engaged
            //8: dead
            //9: red, engaged with your party
            //11: orange, aggroed to your party but not attacked yet
            //10: purple, engaged with other party
            return plateType == 7 || plateType == 9 || plateType == 11 || plateType == 10;
        }

        public static int GetAttackableEnemyCountAroundPoint(Vector3 point, float radius)
        {
            int num = 0;
            foreach(var o in Svc.Objects)
            {
                if(o is BattleNpc)
                {
                    var oStruct = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)o.Address;
                    if(oStruct->GetIsTargetable() && o.IsHostile()
                        && Vector3.Distance(point, o.Position) <= radius + o.HitboxRadius)
                    {
                        num++;
                    }
                }
            }
            return num;
        }

        public static bool TryGetPartyMemberObjectByObjectId(uint objectId, out GameObject partyMemberObject)
        {
            if (objectId == Svc.ClientState.LocalPlayer?.ObjectId)
            {
                partyMemberObject = Svc.ClientState.LocalPlayer;
                return true;
            }
            foreach (var p in Svc.Party)
            {
                if (p.GameObject?.ObjectId == objectId)
                {
                    partyMemberObject = p.GameObject;
                    return true;
                }
            }
            partyMemberObject = default;
            return false;
        }

        public static bool TryGetPartyMemberObjectByAddress(IntPtr address, out GameObject partyMemberObject)
        {
            if (address == Svc.ClientState.LocalPlayer?.Address)
            {
                partyMemberObject = Svc.ClientState.LocalPlayer;
                return true;
            }
            foreach (var p in Svc.Party)
            {
                if (p.GameObject?.Address == address)
                {
                    partyMemberObject = p.GameObject;
                    return true;
                }
            }
            partyMemberObject = default;
            return false;
        }
    }
}
