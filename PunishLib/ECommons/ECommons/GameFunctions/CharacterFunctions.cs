using Dalamud.Game.ClientState.Objects.Types;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.GameFunctions
{
    public static unsafe class CharacterFunctions
    {
        public static ushort GetVFXId(void* VfxData)
        {
            if (VfxData == null) return 0;
            return *(ushort*)((IntPtr)(VfxData) + 8);
        }

        public static FFXIVClientStructs.FFXIV.Client.Game.Character.Character* Struct(this Character o)
        {
            return (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)o.Address;
        }

        public static FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara* Struct(this BattleChara o)
        {
            return (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)o.Address;
        }

        public static bool IsCharacterVisible(this Character chr)
        {
            var v = (IntPtr)(((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)chr.Address)->GameObject.DrawObject);
            if (v == IntPtr.Zero) return false;
            return Bitmask.IsBitSet(*(byte*)(v + 136), 0);
        }

        public static CombatRole GetRole(this Character c)
        {
            if (c.ClassJob.GameData.Role == 1) return CombatRole.Tank;
            if (c.ClassJob.GameData.Role == 2) return CombatRole.DPS;
            if (c.ClassJob.GameData.Role == 3) return CombatRole.DPS;
            if (c.ClassJob.GameData.Role == 4) return CombatRole.Healer;
            return CombatRole.NonCombat;
        }

        public static bool IsCasting(this BattleChara c, uint spellId = 0)
        {
            return c.IsCasting && (spellId == 0 || c.CastActionId.EqualsAny(spellId));
        }

        public static bool IsCasting(this BattleChara c, params uint[] spellId)
        {
            return c.IsCasting && c.CastActionId.EqualsAny(spellId);
        }

        public static bool IsCasting(this BattleChara c, IEnumerable<uint> spellId)
        {
            return c.IsCasting && c.CastActionId.EqualsAny(spellId);
        }
    }
}
