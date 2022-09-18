using Godot;

namespace Tst {
public interface INetSerializable {
    Godot.Collections.Dictionary NetSerialize();

    INetSerializable SerializeFromNet(Godot.Collections.Dictionary dictionary);
}
}
