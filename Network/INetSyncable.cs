using Godot;

namespace Tst {
public interface INetSyncable {
    INetSerializable Snapshot();
}
}
