using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChipMapping.Models.HZCC
{
    [AttributeUsage(AttributeTargets.Property)]
    public class BinaryFieldAttribute : Attribute
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public FieldType Type { get; set; }

        public BinaryFieldAttribute(int offset, int size, FieldType type)
        {
            Offset = offset;
            Size = size;
            Type = type;
        }
    }

    public enum FieldType
    {
        Byte,
        String,
        UInt16,
        UInt32,
        Pointer
    }

    public enum ProbingStartPosition : byte
    {
        UpperLeft = 1,
        UpperRight = 2,
        LowerLeft = 3,
        LowerRight = 4
    }

    public enum ProbingDirection : byte
    {
        Leftward = 1,
        Rightward = 2,
        Upward = 3,
        Backward = 4
    }

    public enum XIncreaseDirection : byte
    {
        Leftward = 1,
        Rightward = 2
    }

    public enum YIncreaseDirection : byte
    {
        Forward = 1,
        Backward = 2
    }

    public enum ReferenceDieSetting : byte
    {
        WaferCenterDie = 1,
        TeachingDie = 2,
        TargetSenseDie = 3
    }

    public enum TestingEndInformation : byte
    {
        NormalEnd = 0,
        YieldNG = 1,
        ContinuousFAILNG = 2,
        ManualUnload = 3,
        OtherReject = 4
    }

    public enum MapVersion : byte
    {
        Normal = 0,
        Chips250000 = 1,
        MultiSites256 = 2,
        MultiSites256WithoutExtendedHeader = 3,
        Category1024 = 4,
        MultiSites256Over9 = 5,
        ExtendedHeader2 = 6,
        Category9999 = 7
    }
}
