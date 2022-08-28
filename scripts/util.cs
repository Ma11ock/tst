using Godot;
using System;

class Util {

    public static Vector3 Lerp(Vector3 v1, Vector3 v2, float weight) {
        return new Vector3(Mathf.Lerp(v1.x, v2.x, weight),
                           Mathf.Lerp(v1.y, v2.y, weight),
                           Mathf.Lerp(v1.z, v2.z, weight));
    }

    public static Vector3 ChangeXY(Vector3 vec, float x, float y) {
        return new Vector3(x, y, vec.z);
    }

    public static Vector3 ChangeXZ(Vector3 vec, float x, float z) {
        return new Vector3(x, vec.y, z);
    }

    public static Vector3 ChangeYZ(Vector3 vec, float y, float z) {
        return new Vector3(vec.x, y, z);
    }

    public static Vector3 ChangeX(Vector3 vec, float x) {
        return new Vector3(x, vec.y, vec.z);
    }

    public static Vector3 ChangeY(Vector3 vec, float y) {
        return new Vector3(vec.x, y, vec.z);
    }

    public static Vector3 ChangeZ(Vector3 vec, float z) {
        return new Vector3(vec.x, vec.y, z);
    }

    public static Vector3 ChangeXRef(ref Vector3 vec, float x) {
        vec = new Vector3(x, vec.y, vec.z);
        return vec;
    }

    public static Vector3 ChangeYRef(ref Vector3 vec, float y) {
        vec = new Vector3(vec.x, y, vec.z);
        return vec;
    }

    public static Vector3 ChangeZRef(ref Vector3 vec, float z) {
        vec = new Vector3(vec.x, vec.y, z);
        return vec;
    }

    public static Transform ChangeTFormOrigin(Transform transform, Vector3 origin) {
        transform.origin = origin;
        return transform;
    }
}
