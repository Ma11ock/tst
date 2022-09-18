using Godot;
using Godot.Collections;

namespace Tst {
public struct QuakePlayerData : INetSerializable {
    public Vector3? mVelocity;
    public Vector3? mWishDir;
    public Vector3? mGravity;
    public float? mVerticalVelocity;
    public Transform? mHeadTransform;
    public Transform? mBodyTransform;
    public Transform? mTransform;

    public QuakePlayerData(Vector3? velocity, Vector3? wishdir, Vector3? gravity,
                           float? verticalVelocity, Transform? headTransform,
                           Transform? bodyTransform, Transform? transform) {
        mVelocity = velocity;
        mWishDir = wishdir;
        mGravity = gravity;
        mVerticalVelocity = verticalVelocity;
        mHeadTransform = headTransform;
        mBodyTransform = bodyTransform;
        mTransform = transform;
    }

    public Dictionary NetSerialize() => new Dictionary() {
        { "vel",         mVelocity},
        {"wdir",          mWishDir},
        {"grav",          mGravity},
        {"vvel", mVerticalVelocity},
        { "hgt",    mHeadTransform},
        { "bgt",    mBodyTransform},
        {  "gt",        mTransform}
    };

    public INetSerializable SerializeFromNet(Dictionary dictionary) => new QuakePlayerData(
        Util.TryGetV<Vector3>(dictionary, "vel"), Util.TryGetV<Vector3>(dictionary, "wdir"),
        Util.TryGetV<Vector3>(dictionary, "grav"), Util.TryGetV<float>(dictionary, "vvel"),
        Util.TryGetV<Transform>(dictionary, "hgt"), Util.TryGetV<Transform>(dictionary, "bgt"),
        Util.TryGetV<Transform>(dictionary, "gt"));
}

public abstract class QuakeNetPlayer : QuakeMover, INetSyncable {
    /// <summary>
    /// True if this player instance is client's player character.
    /// </summary>
    public bool mIsRealPlayer { get; private set; } = false;

    /// <summary>
    /// Mark this player as being the client's player.
    /// </summary>
    public void SetRealPlayer() {
        mIsRealPlayer = true;
    }

    /// <summary>
    /// Id of the network. Only settable once.
    /// </summary>
    public int mNetworkId {
        get => GetTree().GetNetworkUniqueId();
        set {}
    }

    protected QuakePlayerData mLastPredictedState = new QuakePlayerData();

    public QuakePlayerData SnapshotState() => new QuakePlayerData(mVelocity, mWishDir, mGravityVec,
                                                                  mVerticalVelocity,
                                                                  mHead.GlobalTransform,
                                                                  mBody.GlobalTransform,
                                                                  GlobalTransform);

    public INetSerializable Snapshot() => SnapshotState();
}
}
