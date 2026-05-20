namespace PlcLib.Abstractions;

/// <summary>PLC 제공자별 통신 한계를 정의합니다. PlcPollSvc의 블록 최적화에 사용됩니다.</summary>
public sealed class PlcProfile
{
    public PlcProfile(
        int maxWordBlockPoints,
        int maxBitBlockPoints,
        int maxGapPoints,
        int maxUnusedPoints)
    {
        MaxWordBlockPoints = maxWordBlockPoints;
        MaxBitBlockPoints  = maxBitBlockPoints;
        MaxGapPoints       = maxGapPoints;
        MaxUnusedPoints    = maxUnusedPoints;
    }

    /// <summary>한 번의 BlockRead 요청에 담을 수 있는 최대 Word(16bit) 수</summary>
    public int MaxWordBlockPoints { get; }

    /// <summary>한 번의 BlockRead 요청에 담을 수 있는 최대 Bit(Bool) 수</summary>
    public int MaxBitBlockPoints { get; }

    /// <summary>블록 최적화 시 허용할 최대 빈 주소 수 (주소 간격)</summary>
    public int MaxGapPoints { get; }

    /// <summary>블록 최적화 시 허용할 최대 미사용 포인트 수</summary>
    public int MaxUnusedPoints { get; }
}
