using PlcLib.Abstractions;
using PlcLib.Clients;
using PlcLib.Options;

namespace PlcLib.Factories;

/// <summary>
/// PlcClientOpt 설정으로부터 IPlcClient 인스턴스를 생성하는 팩토리입니다.
///
/// 지원 Provider (대소문자 무관):
///   "McpX"        → McpXPlcClient  (미쯔비시 MC Protocol, McpXOpt 필요)
///   "MxComponent" → MxCompPlcClient (미쯔비시 COM, MxCompOpt 필요)
///   "S7"          → S7PlcClient    (지멘스 S7, S7Opt 필요)
///   "Modbus"      → ModbusPlcClient (Modbus TCP/RTU, ModbusOpt 필요)
///   "Virtual"     → VirtualPlcClient (테스트/시뮬레이션)
///   기타          → PlcClientOpt.ImplType으로 리플렉션 생성 시도
/// </summary>
public static class PlcClientFactory
{
    /// <summary>
    /// PlcClientOpt를 기반으로 IPlcClient를 생성합니다.
    /// </summary>
    public static IPlcClient CreateClient(PlcClientOpt opt)
    {
        if (opt == null) throw new ArgumentNullException(nameof(opt));

        var provider = (opt.Provider ?? string.Empty).Trim();

        switch (provider.ToUpperInvariant())
        {
            case "MCPX":
                if (opt.McpX == null)
                    throw new InvalidOperationException($"[{opt.Name}] Provider=McpX이지만 McpXOpt가 설정되지 않았습니다.");
                return new McpXPlcClient(opt.Name, opt.McpX);

            case "MXCOMPONENT":
                if (opt.MxComponent == null)
                    throw new InvalidOperationException($"[{opt.Name}] Provider=MxComponent이지만 MxCompOpt가 설정되지 않았습니다.");
                return new MxCompPlcClient(opt.Name, opt.MxComponent);

            case "S7":
                if (opt.S7 == null)
                    throw new InvalidOperationException($"[{opt.Name}] Provider=S7이지만 S7Opt가 설정되지 않았습니다.");
                return new S7PlcClient(opt.Name, opt.S7);

            case "MODBUS":
                if (opt.Modbus == null)
                    throw new InvalidOperationException($"[{opt.Name}] Provider=Modbus이지만 ModbusOpt가 설정되지 않았습니다.");
                return new ModbusPlcClient(opt.Name, opt.Modbus);

            case "VIRTUAL":
                return new VirtualPlcClient(opt.Name);

            default:
                if (!string.IsNullOrWhiteSpace(opt.ImplType))
                    return CreateByReflection(opt.Name, opt.ImplType, opt);
                throw new NotSupportedException($"지원하지 않는 Provider입니다: '{provider}'. (McpX|MxComponent|S7|Modbus|Virtual 또는 ImplType 설정)");
        }
    }

    /// <summary>
    /// 여러 PlcClientOpt 목록에서 Enabled=true인 항목만 생성합니다.
    /// </summary>
    public static IReadOnlyList<IPlcClient> CreateClients(IReadOnlyList<PlcClientOpt> opts)
    {
        if (opts == null) throw new ArgumentNullException(nameof(opts));
        return opts
            .Where(o => o.Enabled)
            .Select(CreateClient)
            .ToArray();
    }

    // ── 리플렉션 생성 ──────────────────────────────────────────────────

    private static IPlcClient CreateByReflection(string deviceName, string implType, PlcClientOpt opt)
    {
        var type = Type.GetType(implType, throwOnError: false)
            ?? throw new TypeLoadException($"IPlcClient 구현 타입을 찾을 수 없습니다: '{implType}'");

        if (!typeof(IPlcClient).IsAssignableFrom(type))
            throw new InvalidOperationException($"'{implType}'은 IPlcClient를 구현하지 않습니다.");

        // 우선순위: (string, PlcClientOpt) → (string) → 기본 생성자
        var ctorWithOpt = type.GetConstructor(new[] { typeof(string), typeof(PlcClientOpt) });
        if (ctorWithOpt != null)
            return (IPlcClient)ctorWithOpt.Invoke(new object[] { deviceName, opt });

        var ctorName = type.GetConstructor(new[] { typeof(string) });
        if (ctorName != null)
            return (IPlcClient)ctorName.Invoke(new object[] { deviceName });

        var ctorDefault = type.GetConstructor(Type.EmptyTypes);
        if (ctorDefault != null)
            return (IPlcClient)ctorDefault.Invoke(null);

        throw new InvalidOperationException($"'{implType}'에서 적합한 생성자를 찾을 수 없습니다. (string deviceName, PlcClientOpt), (string), () 중 하나를 제공하세요.");
    }
}
