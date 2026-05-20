namespace PlcLib.Options;

public enum PlcValueType
{
    Bool    = 0,
    Int16   = 1,
    UInt16  = 2,
    Int32   = 3,
    UInt32  = 4,
    Float   = 5,
    String  = 6,
}

public enum PlcByteOrder
{
    LittleEndian = 0,
    BigEndian    = 1,
}
