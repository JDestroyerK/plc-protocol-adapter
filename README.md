# PlcLib

.NET 기반 다중 PLC 통신 라이브러리입니다. 지멘스 S7, 미쯔비시 MC Protocol / MX Component, Modbus TCP 를 단일 인터페이스로 추상화합니다.

## 지원 프로토콜

| Provider | 대상 PLC | 사용 라이브러리 |
|---|---|---|
| **S7** | 지멘스 S7-1200 / S7-1500 | S7netplus |
| **McpX** | 미쯔비시 (MC Protocol 3E/E3) | McpX |
| **MxComponent** | 미쯔비시 (MX Component COM) | Interop.ActUtlType64Lib |
| **Modbus** | Modbus TCP / RTU | FluentModbus |
| **Virtual** | 시뮬레이션 / 테스트 | (메모리 기반) |

## 프로젝트 구조

```
MySharedInfra/
├── PlcLib/                     # 라이브러리 (netstandard2.1)
│   ├── Abstractions/
│   │   ├── IPlcClient.cs       # 통합 인터페이스
│   │   └── PlcProfile.cs       # 블록 읽기 프로파일
│   ├── Clients/                # Provider별 구현체
│   │   ├── S7PlcClient.cs
│   │   ├── McpXPlcClient.cs
│   │   ├── MxCompPlcClient.cs
│   │   ├── ModbusPlcClient.cs
│   │   └── VirtualPlcClient.cs
│   ├── Factories/
│   │   └── PlcClientFactory.cs # 팩토리
│   ├── Options/                # 설정 클래스
│   ├── Runtime/
│   │   └── PlcPollSvc.cs       # 주기 폴링 서비스
│   └── PlcLog.cs               # 로깅
└── PlcLib.TestUI/              # WinForms 테스트 앱 (net8.0-windows)
    └── plc-settings.json       # Provider별 샘플 설정
```

## 빠른 시작

### 1. 클라이언트 생성

```csharp
var opt = new PlcClientOpt
{
    Name = "PLC1",
    Provider = "S7",
    S7 = new S7Opt { Ip = "192.168.1.1", Rack = 0, Slot = 1 }
};

using var client = PlcClientFactory.CreateClient(opt);
client.Connect();
```

### 2. 단건 읽기 / 쓰기

```csharp
short value = client.Read<short>("DB1.DBW10");
client.Write<short>("DB1.DBW10", 100);
```

### 3. 블록 읽기

```csharp
// 연속된 주소를 한 번의 통신으로 읽음
short[] values = client.BlockRead<short>("D100", 10);
```

### 4. 폴링 서비스

```csharp
var items = new[]
{
    new PlcItemOpt { Key = "temperature", Address = "D100", Type = PlcValueType.Int16 },
    new PlcItemOpt { Key = "running",     Address = "M10",  Type = PlcValueType.Bool  },
};

var svc = new PlcPollSvc(client, items, pollIntvMs: 100, reconnIntvMs: 3000);
svc.ItemUpdated += (_, snap) => Console.WriteLine($"{snap.Key} = {snap.Value}");
svc.Start();
```

## IPlcClient 인터페이스

```csharp
public interface IPlcClient : IDisposable
{
    string ProviderName { get; }
    string Name         { get; }
    PlcProfile Profile  { get; }
    bool IsConnected    { get; }

    void Connect();
    void Disconnect();

    T    Read<T>(string device)                          where T : unmanaged;
    void Write<T>(string device, T value)                where T : unmanaged;

    T[]  BlockRead<T>(string startDevice, ushort length) where T : unmanaged;
    void BlockWrite<T>(string startDevice, IReadOnlyList<T> values) where T : unmanaged;

    IReadOnlyDictionary<string, T> RandomRead<T>(IReadOnlyList<string> devices)                  where T : unmanaged;
    void                           RandomWrite<T>(IReadOnlyDictionary<string, T> valuesByDevice) where T : unmanaged;
}
```

## 주소 형식

| Provider | Word 주소 예 | Bit 주소 예 | WordBit 예 |
|---|---|---|---|
| S7 | `DB1.DBW10`, `MW10` | `M0.0`, `DB1.DBX0.0` | — |
| McpX / MxComponent | `D100`, `W1A0` | `M10`, `B10` | `D100.3` |
| Modbus | `HR100`, `IR100` | `C10`, `DI10` | `HR100.3` |
| Virtual | 임의 문자열 | 임의 문자열 | — |

## 지원 데이터 타입

`PlcValueType`: `Bool`, `Int16`, `UInt16`, `Int32`, `UInt32`, `Float`, `String`

제네릭 API(`Read<T>`, `BlockRead<T>` 등)는 `unmanaged` 제약으로 컴파일 타임 타입 안전성을 보장합니다.

## 설정 파일 (plc-settings.json)

`PlcLib.TestUI/plc-settings.json`에 Provider별 샘플 설정이 포함되어 있습니다.

```json
{
  "PlcClient": {
    "Name": "TestPLC",
    "Provider": "McpX",
    "McpX": { "Ip": "192.168.10.10", "Port": 5007, "PlcType": "Q" }
  },
  "PlcItems": [
    { "Key": "d100",     "Address": "D100",   "Type": "Int16" },
    { "Key": "m10",      "Address": "M10",    "Type": "Bool"  },
    { "Key": "d200_bit", "Address": "D200.3", "Type": "Bool"  }
  ]
}
```

## 커스텀 로깅

```csharp
PlcLog.Handler = (level, msg) => myLogger.Log(level, msg);
```

## 커스텀 클라이언트 등록

`IPlcClient`를 구현한 클래스를 `PlcClientOpt.ImplType`에 지정하면 리플렉션으로 동적 로드됩니다.

## 빌드 요구 사항

- .NET Standard 2.1 이상을 구현하는 런타임 (.NET 6 / 7 / 8 / 9)
- MxComponent 사용 시: 프로세스 비트니스에 맞는 MX Component 설치 필요
  - 32비트 프로세스 → MX Component v4 (32비트)
  - 64비트 프로세스 → MX Component v5 이상 (64비트)
  - ProgId는 런타임에 프로세스 비트니스 기준으로 자동 선택됨

## 의존성

| 패키지 | 버전 | 용도 |
|---|---|---|
| S7netplus | 0.20.0 | 지멘스 S7 통신 |
| FluentModbus | 5.1.0 | Modbus TCP/RTU |
| McpX | 0.6.0 | 미쯔비시 MC Protocol |
| Microsoft.CSharp | 4.7.0 | COM 인터롭 지원 |
