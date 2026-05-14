using MakroPad.Core.Protocol;

namespace MakroPad.Device;

public sealed record DeviceEvent(DateTimeOffset Timestamp, string Direction, string Line, ProtocolMessage? Message = null);
