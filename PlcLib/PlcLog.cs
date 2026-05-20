namespace PlcLib;

public enum PlcLogLevel { Debug, Info, Warning, Error }

/// <summary>
/// 라이브러리 내부 로그를 외부 로거로 라우팅합니다.
/// Handler를 설정하지 않으면 로그가 무시됩니다.
/// </summary>
public static class PlcLog
{
    public static Action<PlcLogLevel, string, string, Exception?>? Handler { get; set; }

    internal static void Publish(PlcLogLevel level, string source, string message, Exception? ex = null)
        => Handler?.Invoke(level, source, message, ex);

    internal static void Info(string source, string msg)    => Publish(PlcLogLevel.Info,    source, msg);
    internal static void Warning(string source, string msg) => Publish(PlcLogLevel.Warning, source, msg);
    internal static void Debug(string source, string msg)   => Publish(PlcLogLevel.Debug,   source, msg);
}
