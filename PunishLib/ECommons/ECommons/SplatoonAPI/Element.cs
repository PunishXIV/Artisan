using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.SplatoonAPI
{
    public class Element
    {
        int Version;
        internal object Instance;
        public Element(ElementType type)
        {
            Version = Splatoon.Version;
            Instance = Splatoon.Instance.GetType().Assembly.CreateInstance("Splatoon.Element", false, BindingFlags.Default, null, new object[] { (int)type }, null, null);
        }

        public bool IsValid()
        {
            return Version == Splatoon.Version;
        }

        public void SetRefCoord(Vector3 v)
        {
            this.refX = v.X;
            this.refY = v.Z;
            this.refZ = v.Y;
        }

        public void SetOffCoord(Vector3 v)
        {
            this.offX = v.X;
            this.offY = v.Z;
            this.offZ = v.Y;
        }

        public ElementType type
        {
            get => (ElementType)Instance.GetType().GetField("type").GetValue(Instance);
            set => Instance.GetType().GetField("type").SetValue(Instance, (int)value);
        }
        public bool Enabled
        {
            get => (bool)Instance.GetType().GetField("Enabled").GetValue(Instance);
            set => Instance.GetType().GetField("Enabled").SetValue(Instance, value);
        }
        public float refX
        {
            get => (float)Instance.GetType().GetField("refX").GetValue(Instance);
            set => Instance.GetType().GetField("refX").SetValue(Instance, value);
        }
        public float refY
        {
            get => (float)Instance.GetType().GetField("refY").GetValue(Instance);
            set => Instance.GetType().GetField("refY").SetValue(Instance, value);
        }
        public float refZ
        {
            get => (float)Instance.GetType().GetField("refZ").GetValue(Instance);
            set => Instance.GetType().GetField("refZ").SetValue(Instance, value);
        }
        public float offX
        {
            get => (float)Instance.GetType().GetField("offX").GetValue(Instance);
            set => Instance.GetType().GetField("offX").SetValue(Instance, value);
        }
        public float offY
        {
            get => (float)Instance.GetType().GetField("offY").GetValue(Instance);
            set => Instance.GetType().GetField("offY").SetValue(Instance, value);
        }
        public float offZ
        {
            get => (float)Instance.GetType().GetField("offZ").GetValue(Instance);
            set => Instance.GetType().GetField("offZ").SetValue(Instance, value);
        }
        public float radius
        {
            get => (float)Instance.GetType().GetField("radius").GetValue(Instance);
            set => Instance.GetType().GetField("radius").SetValue(Instance, value);
        }
        public float Donut
        {
            get => (float)Instance.GetType().GetField("Donut").GetValue(Instance);
            set => Instance.GetType().GetField("Donut").SetValue(Instance, value);
        }
        public int coneAngleMin
        {
            get => (int)Instance.GetType().GetField("coneAngleMin").GetValue(Instance);
            set => Instance.GetType().GetField("coneAngleMin").SetValue(Instance, value);
        }
        public int coneAngleMax
        {
            get => (int)Instance.GetType().GetField("coneAngleMax").GetValue(Instance);
            set => Instance.GetType().GetField("coneAngleMax").SetValue(Instance, value);
        }
        public uint color
        {
            get => (uint)Instance.GetType().GetField("color").GetValue(Instance);
            set => Instance.GetType().GetField("color").SetValue(Instance, value);
        }
        public uint overlayBGColor
        {
            get => (uint)Instance.GetType().GetField("overlayBGColor").GetValue(Instance);
            set => Instance.GetType().GetField("overlayBGColor").SetValue(Instance, value);
        }
        public uint overlayTextColor
        {
            get => (uint)Instance.GetType().GetField("overlayTextColor").GetValue(Instance);
            set => Instance.GetType().GetField("overlayTextColor").SetValue(Instance, value);
        }
        public float overlayVOffset
        {
            get => (float)Instance.GetType().GetField("overlayVOffset").GetValue(Instance);
            set => Instance.GetType().GetField("overlayVOffset").SetValue(Instance, value);
        }
        public float overlayFScale
        {
            get => (float)Instance.GetType().GetField("overlayFScale").GetValue(Instance);
            set => Instance.GetType().GetField("overlayFScale").SetValue(Instance, value);
        }
        public bool overlayPlaceholders
        {
            get => (bool)Instance.GetType().GetField("overlayPlaceholders").GetValue(Instance);
            set => Instance.GetType().GetField("overlayPlaceholders").SetValue(Instance, value);
        }
        public float thicc
        {
            get => (float)Instance.GetType().GetField("thicc").GetValue(Instance);
            set => Instance.GetType().GetField("thicc").SetValue(Instance, value);
        }
        public string overlayText
        {
            get => (string)Instance.GetType().GetField("overlayText").GetValue(Instance);
            set => Instance.GetType().GetField("overlayText").SetValue(Instance, value);
        }
        public string refActorName
        {
            get => (string)Instance.GetType().GetField("refActorName").GetValue(Instance);
            set => Instance.GetType().GetField("refActorName").SetValue(Instance, value);
        }
        public uint refActorModelID
        {
            get => (uint)Instance.GetType().GetField("refActorModelID").GetValue(Instance);
            set => Instance.GetType().GetField("refActorModelID").SetValue(Instance, value);
        }
        public uint refActorObjectID
        {
            get => (uint)Instance.GetType().GetField("refActorObjectID").GetValue(Instance);
            set => Instance.GetType().GetField("refActorObjectID").SetValue(Instance, value);
        }
        public uint refActorDataID
        {
            get => (uint)Instance.GetType().GetField("refActorDataID").GetValue(Instance);
            set => Instance.GetType().GetField("refActorDataID").SetValue(Instance, value);
        }
        public uint refActorNPCID
        {
            get => (uint)Instance.GetType().GetField("refActorNPCID").GetValue(Instance);
            set => Instance.GetType().GetField("refActorNPCID").SetValue(Instance, value);
        }
        public List<string> refActorPlaceholder
        {
            get => (List<string>)Instance.GetType().GetField("refActorPlaceholder").GetValue(Instance);
            set => Instance.GetType().GetField("refActorPlaceholder").SetValue(Instance, value);
        }
        public uint refActorNPCNameID
        {
            get => (uint)Instance.GetType().GetField("refActorNPCNameID").GetValue(Instance);
            set => Instance.GetType().GetField("refActorNPCNameID").SetValue(Instance, value);
        }
        public bool refActorComparisonAnd
        {
            get => (bool)Instance.GetType().GetField("refActorComparisonAnd").GetValue(Instance);
            set => Instance.GetType().GetField("refActorComparisonAnd").SetValue(Instance, value);
        }
        public bool refActorRequireCast
        {
            get => (bool)Instance.GetType().GetField("refActorRequireCast").GetValue(Instance);
            set => Instance.GetType().GetField("refActorRequireCast").SetValue(Instance, value);
        }
        public List<uint> refActorCastId
        {
            get => (List<uint>)Instance.GetType().GetField("refActorCastId").GetValue(Instance);
            set => Instance.GetType().GetField("refActorCastId").SetValue(Instance, value);
        }
        public bool refActorUseCastTime
        {
            get => (bool)Instance.GetType().GetField("refActorUseCastTime").GetValue(Instance);
            set => Instance.GetType().GetField("refActorUseCastTime").SetValue(Instance, value);
        }
        public float refActorCastTimeMin
        {
            get => (float)Instance.GetType().GetField("refActorCastTimeMin").GetValue(Instance);
            set => Instance.GetType().GetField("refActorCastTimeMin").SetValue(Instance, value);
        }
        public float refActorCastTimeMax
        {
            get => (float)Instance.GetType().GetField("refActorCastTimeMax").GetValue(Instance);
            set => Instance.GetType().GetField("refActorCastTimeMax").SetValue(Instance, value);
        }
        public bool refActorUseOvercast
        {
            get => (bool)Instance.GetType().GetField("refActorUseOvercast").GetValue(Instance);
            set => Instance.GetType().GetField("refActorUseOvercast").SetValue(Instance, value);
        }
        public bool refActorRequireBuff
        {
            get => (bool)Instance.GetType().GetField("refActorRequireBuff").GetValue(Instance);
            set => Instance.GetType().GetField("refActorRequireBuff").SetValue(Instance, value);
        }
        public List<uint> refActorBuffId
        {
            get => (List<uint>)Instance.GetType().GetField("refActorBuffId").GetValue(Instance);
            set => Instance.GetType().GetField("refActorBuffId").SetValue(Instance, value);
        }
        public bool refActorRequireAllBuffs
        {
            get => (bool)Instance.GetType().GetField("refActorRequireAllBuffs").GetValue(Instance);
            set => Instance.GetType().GetField("refActorRequireAllBuffs").SetValue(Instance, value);
        }
        public bool refActorRequireBuffsInvert
        {
            get => (bool)Instance.GetType().GetField("refActorRequireBuffsInvert").GetValue(Instance);
            set => Instance.GetType().GetField("refActorRequireBuffsInvert").SetValue(Instance, value);
        }
        public bool refActorUseBuffTime
        {
            get => (bool)Instance.GetType().GetField("refActorUseBuffTime").GetValue(Instance);
            set => Instance.GetType().GetField("refActorUseBuffTime").SetValue(Instance, value);
        }
        public float refActorBuffTimeMin
        {
            get => (float)Instance.GetType().GetField("refActorBuffTimeMin").GetValue(Instance);
            set => Instance.GetType().GetField("refActorBuffTimeMin").SetValue(Instance, value);
        }
        public float refActorBuffTimeMax
        {
            get => (float)Instance.GetType().GetField("refActorBuffTimeMax").GetValue(Instance);
            set => Instance.GetType().GetField("refActorBuffTimeMax").SetValue(Instance, value);
        }
        public bool refActorObjectLife
        {
            get => (bool)Instance.GetType().GetField("refActorObjectLife").GetValue(Instance);
            set => Instance.GetType().GetField("refActorObjectLife").SetValue(Instance, value);
        }
        public float refActorLifetimeMin
        {
            get => (float)Instance.GetType().GetField("refActorLifetimeMin").GetValue(Instance);
            set => Instance.GetType().GetField("refActorLifetimeMin").SetValue(Instance, value);
        }
        public float refActorLifetimeMax
        {
            get => (float)Instance.GetType().GetField("refActorLifetimeMax").GetValue(Instance);
            set => Instance.GetType().GetField("refActorLifetimeMax").SetValue(Instance, value);
        }
        public float FillStep
        {
            get => (float)Instance.GetType().GetField("FillStep").GetValue(Instance);
            set => Instance.GetType().GetField("FillStep").SetValue(Instance, value);
        }
        public RefActorComparisonType refActorComparisonType
        {
            get => (RefActorComparisonType)Instance.GetType().GetField("refActorComparisonType").GetValue(Instance);
            set => Instance.GetType().GetField("refActorComparisonType").SetValue(Instance, (int)value);
        }
        public RefActorType refActorType
        {
            get => (RefActorType)Instance.GetType().GetField("refActorType").GetValue(Instance);
            set => Instance.GetType().GetField("refActorType").SetValue(Instance, (int)value);
        }
        public bool includeHitbox
        {
            get => (bool)Instance.GetType().GetField("includeHitbox").GetValue(Instance);
            set => Instance.GetType().GetField("includeHitbox").SetValue(Instance, value);
        }
        public bool includeOwnHitbox
        {
            get => (bool)Instance.GetType().GetField("includeOwnHitbox").GetValue(Instance);
            set => Instance.GetType().GetField("includeOwnHitbox").SetValue(Instance, value);
        }
        public bool includeRotation
        {
            get => (bool)Instance.GetType().GetField("includeRotation").GetValue(Instance);
            set => Instance.GetType().GetField("includeRotation").SetValue(Instance, value);
        }
        public bool onlyTargetable
        {
            get => (bool)Instance.GetType().GetField("onlyTargetable").GetValue(Instance);
            set => Instance.GetType().GetField("onlyTargetable").SetValue(Instance, value);
        }
        public bool onlyUnTargetable
        {
            get => (bool)Instance.GetType().GetField("onlyUnTargetable").GetValue(Instance);
            set => Instance.GetType().GetField("onlyUnTargetable").SetValue(Instance, value);
        }
        public bool onlyVisible
        {
            get => (bool)Instance.GetType().GetField("onlyVisible").GetValue(Instance);
            set => Instance.GetType().GetField("onlyVisible").SetValue(Instance, value);
        }
        public bool tether
        {
            get => (bool)Instance.GetType().GetField("tether").GetValue(Instance);
            set => Instance.GetType().GetField("tether").SetValue(Instance, value);
        }
        public float AdditionalRotation
        {
            get => (float)Instance.GetType().GetField("AdditionalRotation").GetValue(Instance);
            set => Instance.GetType().GetField("AdditionalRotation").SetValue(Instance, value);
        }
        public bool LineAddHitboxLengthX
        {
            get => (bool)Instance.GetType().GetField("LineAddHitboxLengthX").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddHitboxLengthX").SetValue(Instance, value);
        }
        public bool LineAddHitboxLengthY
        {
            get => (bool)Instance.GetType().GetField("LineAddHitboxLengthY").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddHitboxLengthY").SetValue(Instance, value);
        }
        public bool LineAddHitboxLengthZ
        {
            get => (bool)Instance.GetType().GetField("LineAddHitboxLengthZ").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddHitboxLengthZ").SetValue(Instance, value);
        }
        public bool LineAddHitboxLengthXA
        {
            get => (bool)Instance.GetType().GetField("LineAddHitboxLengthXA").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddHitboxLengthXA").SetValue(Instance, value);
        }
        public bool LineAddHitboxLengthYA
        {
            get => (bool)Instance.GetType().GetField("LineAddHitboxLengthYA").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddHitboxLengthYA").SetValue(Instance, value);
        }
        public bool LineAddHitboxLengthZA
        {
            get => (bool)Instance.GetType().GetField("LineAddHitboxLengthZA").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddHitboxLengthZA").SetValue(Instance, value);
        }
        public bool LineAddPlayerHitboxLengthX
        {
            get => (bool)Instance.GetType().GetField("LineAddPlayerHitboxLengthX").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddPlayerHitboxLengthX").SetValue(Instance, value);
        }
        public bool LineAddPlayerHitboxLengthY
        {
            get => (bool)Instance.GetType().GetField("LineAddPlayerHitboxLengthY").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddPlayerHitboxLengthY").SetValue(Instance, value);
        }
        public bool LineAddPlayerHitboxLengthZ
        {
            get => (bool)Instance.GetType().GetField("LineAddPlayerHitboxLengthZ").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddPlayerHitboxLengthZ").SetValue(Instance, value);
        }
        public bool LineAddPlayerHitboxLengthXA
        {
            get => (bool)Instance.GetType().GetField("LineAddPlayerHitboxLengthXA").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddPlayerHitboxLengthXA").SetValue(Instance, value);
        }
        public bool LineAddPlayerHitboxLengthYA
        {
            get => (bool)Instance.GetType().GetField("LineAddPlayerHitboxLengthYA").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddPlayerHitboxLengthYA").SetValue(Instance, value);
        }
        public bool LineAddPlayerHitboxLengthZA
        {
            get => (bool)Instance.GetType().GetField("LineAddPlayerHitboxLengthZA").GetValue(Instance);
            set => Instance.GetType().GetField("LineAddPlayerHitboxLengthZA").SetValue(Instance, value);
        }
        public bool Filled
        {
            get => (bool)Instance.GetType().GetField("Filled").GetValue(Instance);
            set => Instance.GetType().GetField("Filled").SetValue(Instance, value);
        }
        public bool FaceMe
        {
            get => (bool)Instance.GetType().GetField("FaceMe").GetValue(Instance);
            set => Instance.GetType().GetField("FaceMe").SetValue(Instance, value);
        }
        public bool LimitDistance
        {
            get => (bool)Instance.GetType().GetField("LimitDistance").GetValue(Instance);
            set => Instance.GetType().GetField("LimitDistance").SetValue(Instance, value);
        }
        public bool LimitDistanceInvert
        {
            get => (bool)Instance.GetType().GetField("LimitDistanceInvert").GetValue(Instance);
            set => Instance.GetType().GetField("LimitDistanceInvert").SetValue(Instance, value);
        }
        public float DistanceSourceX
        {
            get => (float)Instance.GetType().GetField("DistanceSourceX").GetValue(Instance);
            set => Instance.GetType().GetField("DistanceSourceX").SetValue(Instance, value);
        }
        public float DistanceSourceY
        {
            get => (float)Instance.GetType().GetField("DistanceSourceY").GetValue(Instance);
            set => Instance.GetType().GetField("DistanceSourceY").SetValue(Instance, value);
        }
        public float DistanceSourceZ
        {
            get => (float)Instance.GetType().GetField("DistanceSourceZ").GetValue(Instance);
            set => Instance.GetType().GetField("DistanceSourceZ").SetValue(Instance, value);
        }
        public float DistanceMin
        {
            get => (float)Instance.GetType().GetField("DistanceMin").GetValue(Instance);
            set => Instance.GetType().GetField("DistanceMin").SetValue(Instance, value);
        }
        public float DistanceMax
        {
            get => (float)Instance.GetType().GetField("DistanceMax").GetValue(Instance);
            set => Instance.GetType().GetField("DistanceMax").SetValue(Instance, value);
        }
        public string refActorVFXPath
        {
            get => (string)Instance.GetType().GetField("refActorVFXPath").GetValue(Instance);
            set => Instance.GetType().GetField("refActorVFXPath").SetValue(Instance, value);
        }
        public int refActorVFXMin
        {
            get => (int)Instance.GetType().GetField("refActorVFXMin").GetValue(Instance);
            set => Instance.GetType().GetField("refActorVFXMin").SetValue(Instance, value);
        }
        public int refActorVFXMax
        {
            get => (int)Instance.GetType().GetField("refActorVFXMax").GetValue(Instance);
            set => Instance.GetType().GetField("refActorVFXMax").SetValue(Instance, value);
        }
        public bool LimitRotation
        {
            get => (bool)Instance.GetType().GetField("LimitRotation").GetValue(Instance);
            set => Instance.GetType().GetField("LimitRotation").SetValue(Instance, value);
        }
        public float RotationMax
        {
            get => (float)Instance.GetType().GetField("RotationMax").GetValue(Instance);
            set => Instance.GetType().GetField("RotationMax").SetValue(Instance, value);
        }
        public float RotationMin
        {
            get => (float)Instance.GetType().GetField("RotationMin").GetValue(Instance);
            set => Instance.GetType().GetField("RotationMin").SetValue(Instance, value);
        }
    }
}
