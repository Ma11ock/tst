using Godot;
using System.Collections.Generic;
using System;

// Notes on Quake's movement code: It's coordinate system is different to that
// of Godot's. In Quake Z is up/down, in Godot Z is forwards/backwards and Y is
// up/down.

using Snap = Godot.Collections.Dictionary;
using DArray = Godot.Collections.Array;

namespace Tst {

/// <summary>
/// Player input struct.
/// </summary>
public struct Input {
    /// <summary>
    /// Strafe values. Should range from [-1,1].
    /// </summary>
    public float strafe { get; private set; }
    /// <summary>
    /// Forward/backwards values. Should range from [-1,1].
    /// </summary>
    public float forwards { get; private set; }
    /// <summary>
    /// Change in mouse's X direction.
    /// </summary>
    public DArray dx { get; private set; }
    /// <summary>
    /// Change in mouse's Y direction.
    /// </summary>
    public DArray dy { get; private set; }
    /// <summary>
    /// If true, player has queued a jump : the jump key can be held down before hitting the ground
    /// to jump.
    /// </summary>
    public bool jump { get; private set; }

    public ulong id { get; private set; }

    /// <summary>
    /// Create a new input with strafe and forward set, everything else set to 0.
    /// </summary>
    /// <param name="strafe"> Strafe value. </param>
    /// <param name="strafe"> Forwards value. </param>
    public Input(float strafe, float forwards) {
        this.strafe = strafe;
        this.forwards = forwards;
        this.dx = new DArray();
        this.dy = new DArray();
        this.jump = false;
        this.id = 0;
    }

    /// <summary>
    /// Create a new input. Full constructor, sets all members.
    /// <param name="strafe"> Strafe value. </param>
    /// <param name="strafe"> Forwards value. </param>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>
    /// <param name="jump"> This frame's jump value. </param>
    /// </summary>
    public Input(float strafe, float forwards, float dx, float dy, bool jump) {
        this.strafe = strafe;
        this.forwards = forwards;
        this.dx = new DArray() { dx };
        this.dy = new DArray() { dy };
        this.jump = jump;
        this.id = 0;
    }

    /// <summary>
    /// Create a new input. Full constructor, sets all members.
    /// <param name="strafe"> Strafe value. </param>
    /// <param name="strafe"> Forwards value. </param>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>
    /// <param name="jump"> This frame's jump value. </param>
    /// </summary>
    public Input(float strafe, float forwards, DArray dx, DArray dy, bool jump) {
        this.strafe = strafe;
        this.forwards = forwards;
        this.dx = dx;
        this.dy = dy;
        this.jump = jump;
        this.id = 0;
    }

    /// <summary>
    /// Create a new input. Full constructor, sets all members.
    /// </summary>
    /// <returns> Create a string representation of all of <c>Input</c>'s members. </returns>
    public override string ToString() =>
        $"strafe = {strafe}, forwards = {forwards}, dx = {dx}, dy = {dy}, jump = {jump}, id = {id}";

    /// <summary>
    /// Create a new input. Sets the mouse values, all other values are unchanged.
    /// </summary>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>
    public Input SetMouse(float dx, float dy) => new Input(this.strafe, this.forwards, dx, dy,
                                                           this.jump);

    /// <summary>
    /// Create a new input. Sets the mouse values to <see cref="dx"> + dx, and <see cref="dy"> + dy.
    /// Keep all remaining values the same.
    /// </summary>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>

    /// TODO list of mouse movements.
    public Input DeltaMouse(float dx, float dy) => new Input(this.strafe, this.forwards,
                                                             new DArray(this.dx) { dx },
                                                             new DArray(this.dy) { dy }, this.jump);

    /// <summary>
    /// Create a new input. Sets the mouse values to <see cref="dx"> + dx, and <see cref="dy"> + dy.
    /// Keep all remaining values the same.
    /// </summary>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>
    public Input ResetMouse() => SetMouse(0F, 0F);

    /// <summary>
    /// Create a new input. Sets the direction to strafe and forwards. All other values remain the
    /// same.
    /// </summary>
    /// <param name="strafe"> Strafe value. </param>
    /// <param name="strafe"> Forwards value. </param>
    public Input SetDirs(float strafe, float forwards) => new Input(strafe, forwards, this.dx,
                                                                    this.dy, this.jump);

    /// <summary>
    /// Create a new input. Sets <see cref="jump"> to jump. All other values remain the same.
    /// </summary>
    /// <param name="jump"> Jump value. </param>
    public Input SetJump(bool wishJump = true) => new Input(this.strafe, this.forwards, this.dx,
                                                            this.dy, wishJump);

    public Input SetId(ulong id) {
        this.id = id;
        return this;
    }
}
}

/// <summary>
/// Player class. When run on a client, the Player can be:
/// <list type="bullet">
/// <item><term>Client's player</term><description>The player for this client's session. Interprets
/// inputs, does client-side prediction, etc.</description>
/// </item>
/// <item>
/// <term>Client puppet player</term>
/// <description>Player object of someone else's client. The player is a dummy object
/// mastered by the server. It acts as any other physics object in the
/// simulation.</description>
/// </item>
//// <item>
/// <term>Server player</term>
/// <description>Remote player /represented by a client. Interprets user
/// inputs.</description>
/// </item>
/// </list>
/// </summary>
public class Player : Tst.QuakeMover, Tst.Debuggable {
    // Children nodes.
    /// <summary>
    /// Body reference.
    /// </summary>
    private Godot.Spatial mBody = null;
    /// <summary>
    /// Head reference.
    /// </summary>
    private Godot.Spatial mHead = null;
    /// <summary>
    /// Camera reference.
    /// </summary>
    private Godot.Camera mCamera = null;
    /// <summary>
    /// Model reference.
    /// </summary>
    private Godot.MeshInstance mModel = null;
    /// <summary>
    /// Movement tween reference for softer movement.
    /// </summary>
    private Godot.Tween mMovementTween = null;

    /// <summary>
    /// Queue of player inputs. Used only by the server.
    /// </summary>
    private Queue<Snap> mPlayerInputQueue = null;

    /// <summary>
    /// Last state predicted by the client. Only used by the client's real player.
    /// </summary>
    private Snap mLastPredictedState = null;

    /// <summary>
    /// Input counter used to track which inputs the server has processed. Used only by the client's
    /// player.
    /// </summary>
    private ulong mInputIdCounter = 0;

    /// <summary>
    /// List of player inputs. Used only by the client for rollback and reconciliation.
    /// </summary>
    private List<Tst.Input> mPlayerInputList = null;

    /// <summary>
    /// Input struct for the current frame.
    /// </summary>
    private Tst.Input mInputs = new Tst.Input(0F, 0F, 0F, 0F, false);
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
    /// Real id of the network.
    /// </summary>
    private int _mNetworkId = 0;

    /// <summary>
    /// Id of the network. Only settable once.
    /// </summary>
    public int mNetworkId {
        get => _mNetworkId;
        set {
            if (_mNetworkId == 0) {
                _mNetworkId = value;
            }
        }
    }

    /// <summary>
    /// Reference to the Scene's debug overlay.
    /// </summary>
    private DebugOverlay mDebugOverlay = null;

    /// <summary>
    /// Reference to the scene manager.
    /// </summary>
    private Scene mSceneRef = null;

    /// <summary>
    /// The n'th tick the game server is on. Used only by the server and the client's player.
    /// </summary>
    public ulong mCurTick = 0;

    /// <summary>
    /// ID for debugging system.
    /// </summary>
    private ulong mDebugId = 0;

    /// <summary>
    /// Get the debugger id.
    /// </summary>
    public ulong GetDebugId() => mDebugId;

    /// <summary>
    /// Set the debugger ID.
    /// </summary>
    public void SetDebugId(ulong id) => mDebugId = id;

    public void GetDebug(Control c) {
        if (c is Godot.Label label) {
            label.Text = $@"Velocity: {mVelocity}
Wishdir: {mWishDir}
GlobalT: {GlobalTransform}
Is jump: {mIsJump}
Is on floor: {IsOnFloor()}
Vertical velocity: {mVerticalVelocity}";
        }
    }

    public string GetInputDescription() => mInputs.ToString();

    public Vector3 GetLookAt() => mHead.Transform.basis.z;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        base._Ready();

        mSceneRef = (Scene)GetParent();
        mDebugOverlay = GetParent().GetNodeOrNull<DebugOverlay>(Scene.DEBUG_OVERLAY_NAME);
        if (mDebugOverlay != null) {
            mDebugOverlay.Add<Label>(this);
        }
        mBody = GetNode<Godot.Spatial>("Body");
        mHead = mBody.GetNode<Godot.Spatial>("Head");
        mCamera = mHead.GetNode<Godot.Camera>("Camera");
        mMovementTween = GetNode<Godot.Tween>("MovementTween");

        Input.MouseMode = Input.MouseModeEnum.Captured;
        mBodyEulerY = mBody.GlobalTransform.basis.GetEuler().y;

        mCameraTargetPos = mCamera.GlobalTransform.origin;
        mCamera.SetAsToplevel(true);
        mCamera.PhysicsInterpolationMode = Godot.Node.PhysicsInterpolationModeEnum.Off;

        mModel = GetNode<Godot.MeshInstance>("Model");

        mCamera.Current = mIsRealPlayer;
        mModel.Visible = !mIsRealPlayer;

        // Setup specific to server/client.
        if (GetTree().IsNetworkServer()) {
            mPlayerInputQueue = new Queue<Snap>();
        } else {
            mPlayerInputList = new List<Tst.Input>();
            mLastPredictedState = SnapshotState();
        }
    }

    public override void _UnhandledInput(InputEvent @event) {
        base._UnhandledInput(@event);

        if (!mIsRealPlayer) {
            return;
        }
        if (@event is InputEventMouseMotion mouseEvent &&
            Input.MouseMode == Input.MouseModeEnum.Captured) {
            float dx = -mouseEvent.Relative.x * mMouseSensitivity;
            float dy = -mouseEvent.Relative.y * mMouseSensitivity;
            mInputs = mInputs.DeltaMouse(dx, dy);
            MoveHead(dx, dy, Mathf.Deg2Rad(-89), Mathf.Deg2Rad(89));
            // MoveHead(dx, dy);
        }
    }

    public override void _Process(float delta) {
        base._Process(delta);
        if (!mIsRealPlayer) {
            return;
        }

        //  Find the current interpolated transform of the target.
        Transform tr = mHead.GetGlobalTransformInterpolated();

        // Provide some delayed smoothed lerping towards the target position.
        mCameraTargetPos = Util.Lerp(mCameraTargetPos, tr.origin,
                                     delta * mVelocity.Length() * STAIRS_FEELING_COEFFICIENT);

        mCamera.Translation = Util.ChangeX(mCamera.Translation, tr.origin.x);

        // TODO fix mCameraCoefficient vs CAMERA_COEFFICIENT
        if (IsOnFloor()) {
            mTimeInAir = 0F;
            mCameraCoefficient = 1.0F;
            mCamera.Translation = Util.ChangeY(mCamera.Translation, mCameraTargetPos.y);
        } else {
            mTimeInAir += delta;
            if (mTimeInAir > 1.0F) {
                mCameraCoefficient += delta;
                mCameraCoefficient = Mathf.Clamp(mCameraCoefficient, 2.0F, 4.0F);
            } else {
                mCameraCoefficient = 2.0F;
            }

            mCamera.Translation = Util.ChangeY(mCamera.Translation, mCameraTargetPos.y);
        }

        mCamera.Translation = Util.ChangeZ(mCamera.Translation, tr.origin.z);
        mCamera.Rotation =
            Util.ChangeXY(mCamera.Rotation, mHead.Rotation.x, mBody.Rotation.y + mBodyEulerY);
    }

    private void SimulatePhysics(float delta, bool dummyInput = false) {
    }

    public override void _ExitTree() {
        base._ExitTree();

        if (mDebugOverlay != null) {
            mDebugOverlay.Remove(this);
        }
    }

    private Snap SnapshotState() => new Snap() {
        {  "gt",                    GlobalTransform},
        { "hgt",              mHead.GlobalTransform},
        { "bgt",              mBody.GlobalTransform},
        { "vel",                          mVelocity},
        { "ajp",                          mAutoJump},
        {"grav",                        mGravityVec},
        {"snap",                              mSnap},
        {"vvel",                  mVerticalVelocity},
        {"wdir",                           mWishDir},
        {  "ts", OS.GetSystemTimeMsecs().ToString()},
        {"tick",                mCurTick.ToString()},
        { "iid",              mInputs.id.ToString()}
    };

    public override void _PhysicsProcess(float delta) {
        base._PhysicsProcess(delta);
        ++mCurTick;

        float forwardInput = mInputs.forwards;
        float strafeInput = mInputs.strafe;
        if (mIsRealPlayer && !Global.InputCaptured) {
            forwardInput =
                Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
            strafeInput =
                Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");

            mInputs = mInputs.SetDirs(strafeInput, forwardInput).SetJump(QueueJump());
            SendInputPacket();
        } else if (GetTree().IsNetworkServer()) {
            // Get inputs.
            NextInput();
            MoveHead();
        }
        mWishDir = new Vector3(strafeInput, 0F, forwardInput)
                       .Rotated(Vector3.Up, mBody.GlobalTransform.basis.GetEuler().y)
                       .Normalized();
        Simulate(delta);

        // Interpolate playermovement for smooth.
        mMovementTween.InterpolateProperty(
            this, "global_transform", GlobalTransform,
            new Transform(GlobalTransform.basis, GlobalTransform.origin), 0.1F);
        mMovementTween.Start();
        if (mIsRealPlayer && mInputs.jump) {
            mIsJump = true;
        } else if (GetTree().IsNetworkServer()) {
            SendPlayerState();
        }
    }

    // Set wish_jump depending on player input.
    public override bool QueueJump() {
        if (!mIsRealPlayer) {
            return mInputs.jump;
        }
        if (!IsOnFloor()) {
            return false;
        }
        // If auto_jump is true, the player keeps jumping as long as the key is kept down.
        if (mAutoJump) {
            return Input.IsActionPressed("jump");
        }

        if (Input.IsActionJustPressed("jump")) {
            return true;
        }
        return false;
    }

    private void MoveHead() {
        for (int i = 0; (mInputs.dx != null) && (mInputs.dy != null) && (i < mInputs.dx.Count) &&
                        (i < mInputs.dy.Count);
             i++) {
            float? x = mInputs.dx[i] as float ? ;
            float? y = mInputs.dy[i] as float ? ;
            // Validate input.
            if ((x == null) || (y == null) || !Util.IsFinite(x.Value) || !Util.IsFinite(y.Value)) {
                GD.PrintErr($"Invalid mouse input: ({x},{y}). Skipping.");
                continue;
            }
            MoveHead(x.Value, y.Value, Mathf.Deg2Rad(-89), Mathf.Deg2Rad(89));
        }
    }

    public override void MoveHead(float dx, float dy, float minXRotRad, float maxXRotRad) {
        mBody.RotateY(Mathf.Deg2Rad(dx));
        mHead.RotateX(Mathf.Deg2Rad(dy));
        float newRotX = Mathf.Clamp(mHead.Rotation.x, Mathf.Deg2Rad(-89), Mathf.Deg2Rad(89));
        mHead.Rotation = Util.ChangeX(mHead.Rotation, newRotX);

        mBody.Transform = mBody.Transform.Orthonormalized();
        mHead.Transform = mHead.Transform.Orthonormalized();
        Transform = Transform.Orthonormalized();
    }

    public void PlayerInput(Snap dat) {
        if (mPlayerInputQueue.Count > 32) {
            GD.PrintErr($"Err: {mPlayerInputQueue.Count} is at max!");
        }
        mPlayerInputQueue.Enqueue(dat);
    }

    public void ClientPredict(Snap recv) {
        return;
        if (!mIsRealPlayer) {
            return;
        }
        // Our current client-predicted state is used to interpolate to the new one.
        mLastPredictedState = SnapshotState();
        UpdatePlayer(recv);
        ulong acks = Util.TryGetVOr(recv, "iid", ulong.MaxValue);
        var tmp = mInputs;
        int nRm = 0;
        foreach(var input in mPlayerInputList) {
            // We rollback and reinterpret all unacknowledged inputs with the new state received by
            // the server.
            if (input.id > acks) {
                mInputs = input;
                MoveHead();
                // TODO do not hardcode (1/128).
                SimulatePhysics((1F / 128F), true);
            } else {
                nRm++;
            }
        }
        mPlayerInputList.RemoveRange(0, nRm);
        mInputs = tmp;
    }

    public void Lerp(Transform wholeTo, Transform headTo, Transform bodyTo, float factor) {
        Snap oldPlayerDat = mLastPredictedState;
        Transform oldPosition = (Transform)oldPlayerDat["gt"];
        GlobalTransform = oldPosition.InterpolateWith(wholeTo, factor);

        Transform oldHeadPosition = (Transform)oldPlayerDat["hgt"];
        mHead.GlobalTransform = oldHeadPosition.InterpolateWith(headTo, factor);

        Transform oldBodyPosition = (Transform)oldPlayerDat["bgt"];
        mHead.GlobalTransform = oldBodyPosition.InterpolateWith(bodyTo, factor);
    }

    public void ExtrapolateTo(Transform wholeTo, Transform headTo, Transform bodyTo) {
        if (mIsRealPlayer) {
            return;
        }
    }

    ulong macks = 0;

    private void NextInput() {
        if (mPlayerInputQueue.Count < 1 || !GetTree().IsNetworkServer()) {
            return;
        }
        Snap dat = mPlayerInputQueue.Dequeue();
        macks = Util.TryGetVOr(dat, "tick", ulong.MaxValue);
        DArray dx = Util.TryGetR<DArray>(dat, "dx");
        DArray dy = Util.TryGetR<DArray>(dat, "dy");
        float strafe = Util.TryGetVOr<float>(dat, "str", 0F);
        float forward = Util.TryGetVOr<float>(dat, "for", 0F);
        bool jump = Util.TryGetVOr<bool>(dat, "jmp", false);
        bool autoJump = Util.TryGetVOr<bool>(dat, "ajp", false);
        ulong iid = Util.TryGetVOr(dat, "iid", ulong.MaxValue);

        // Ensure all floats are valid values. Reject the input if so.
        if (!Util.IsFinite(0)) {
            GD.PrintErr($"Head rotation is not finite");
            return;
        } else if (!Util.IsFinite(strafe)) {
            GD.PrintErr($"Strafe value is not finite");
            return;
        } else if (!Util.IsFinite(forward)) {
            GD.PrintErr($"Forward movement value is not finite");
            return;
        }
        // TODO make this tst from the snap in a better way and validate (probably as a
        // constructor).

        // Set the input.
        mInputs = new Tst.Input(strafe, forward, dx, dy, jump).SetId(iid);
        mAutoJump = autoJump;
    }

    public void UpdatePlayer(Snap recv) {
        GlobalTransform = Util.TryGetVOr<Transform>(recv, "gt", GlobalTransform);
        mVelocity = Util.TryGetVOr<Vector3>(recv, "vel", mVelocity);
        mGravityVec = Util.TryGetVOr<Vector3>(recv, "grav", mGravityVec);
        mWishDir = Util.TryGetVOr<Vector3>(recv, "wdir", mWishDir);
        mVerticalVelocity = Util.TryGetVOr<float>(recv, "vvel", mVerticalVelocity);
        mSnap = Util.TryGetVOr<Vector3>(recv, "snap", mSnap);
        mHead.GlobalTransform = Util.TryGetVOr<Transform>(recv, "hgt", mHead.GlobalTransform);
        mBody.GlobalTransform = Util.TryGetVOr<Transform>(recv, "bgt", mBody.GlobalTransform);
    }

    private void SendInputPacket() {
        ulong iid = ++mInputIdCounter;
        Snap send = new Snap() {
            {  "dx",                         mInputs.dx},
            {  "dy",                         mInputs.dy},
            { "str",                     mInputs.strafe},
            { "for",                   mInputs.forwards},
            { "jmp",                       mInputs.jump},
            { "ajp",                          mAutoJump},
            { "iid",                     iid.ToString()},
            {  "ts", OS.GetSystemTimeMsecs().ToString()},
            {"tick",                mCurTick.ToString()}
        };
        mPlayerInputList.Add(mInputs.SetId(iid));
        mInputs = mInputs.ResetMouse();
        mSceneRef.SendPlayerInput(send);
    }

    private void SendPlayerState() => mSceneRef.SendPlayerState(mNetworkId, SnapshotState());
}
