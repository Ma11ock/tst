using Godot;
using System;

using Snap = Godot.Collections.Dictionary;

class Util {
    public static Vector3 Lerp(Vector3 v1, Vector3 v2,
                               float weight) => new Vector3(Mathf.Lerp(v1.x, v2.x, weight),
                                                            Mathf.Lerp(v1.y, v2.y, weight),
                                                            Mathf.Lerp(v1.z, v2.z, weight));

    public static Vector3 ChangeXY(Vector3 vec, float x, float y) => new Vector3(x, y, vec.z);

    public static Vector3 ChangeXZ(Vector3 vec, float x, float z) => new Vector3(x, vec.y, z);

    public static Vector3 ChangeYZ(Vector3 vec, float y, float z) => new Vector3(vec.x, y, z);

    public static Vector3 ChangeX(Vector3 vec, float x) => new Vector3(x, vec.y, vec.z);

    public static Vector3 ChangeY(Vector3 vec, float y) => new Vector3(vec.x, y, vec.z);

    public static Vector3 ChangeZ(Vector3 vec, float z) => new Vector3(vec.x, vec.y, z);

    public static Vector3 ChangeXRef(ref Vector3 vec, float x) => vec = new Vector3(x, vec.y,
                                                                                    vec.z);

    public static Vector3 ChangeYRef(ref Vector3 vec, float y) => vec = new Vector3(vec.x, y,
                                                                                    vec.z);

    public static Vector3 ChangeZRef(ref Vector3 vec, float z) => vec = new Vector3(vec.x, vec.y,
                                                                                    z);

    public static Transform ChangeTFormOrigin(Transform transform, Vector3 origin) {
        transform.origin = origin;
        return transform;
    }

    public static bool IsFinite(float u) => !(float.IsInfinity(u) || float.IsNaN(u));

    /// <summary>
    /// Try to get a ulong from a map.
    /// NOTE: Only necessary because of a bug in Godot (maps cannot store ulongs, so they have to be
    /// encoded as strings).
    /// </summary>
    public static ulong TryGetVOr(Snap dat, string key, ulong v) {
        string r = TryGetR<string>(dat, key);
        try {
            return UInt64.Parse(r);
        } catch (Exception) {
            return v;
        }
    }

    public static T TryGetVOr<T>(Snap dat, string key, T or)
        where T : unmanaged {
        T? r = TryGetV<T>(dat, key);
        return (r == null) ? or : r.Value;
    }

    public static T? TryGetV<T>(Snap dat, string key)
        where T : unmanaged {
        object obj = null;

        try {
            obj = dat[key];
        } catch (System.Collections.Generic.KeyNotFoundException) {
        }

        if (obj == null || !(obj is T)) {
            return null;
        }

        return (T)obj;
    }

    public static T TryGetR<T>(Snap dat, string key)

        where T : class {
        object obj = null;

        try {
            obj = dat[key];
        } catch (System.Collections.Generic.KeyNotFoundException) {
        }

        if (obj == null || !(obj is T)) {
            return null;
        }

        return (T)obj;
    }
}
