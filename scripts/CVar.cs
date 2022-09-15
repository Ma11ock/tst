
using System;
using System.Collections.Generic;

namespace Tst {

public abstract class CVar {
    public abstract object Get();
}

public class CIVar : CVar {
    private long _mValue = 0;
    public long mValue {
        get => _mValue;
        set {
            if (mSetter != null) {
                _mValue = mSetter(value);
            } else {
                _mValue = value;
            }
        }
    }
    private Func<long, long> mSetter = null;

    public CIVar(long @value, Func<long, long> setter = null) {
        mValue = @value;
        mSetter = setter;
    }

    public CIVar() {
    }

    public override object Get() => mValue;
}

public class CFVar : CVar {
    private float _mValue = 0F;
    public float mValue {
        get => _mValue;
        set {
            if (mSetter != null) {
                _mValue = mSetter(value);
            } else {
                _mValue = value;
            }
        }
    }
    private Func<float, float> mSetter = null;

    public CFVar(float @value, Func<float, float> setter = null) {
        _mValue = @value;
        mSetter = setter;
    }

    public CFVar() {
    }

    public override object Get() => mValue;
}

public class CBVar : CVar {
    private bool _mValue = false;
    public bool mValue {
        get => _mValue;
        set {
            if (mSetter != null) {
                _mValue = mSetter(value);
            } else {
                _mValue = value;
            }
        }
    }
    private Func<bool, bool> mSetter = null;

    public CBVar(bool @value, Func<bool, bool> setter = null) {
        _mValue = @value;
        mSetter = setter;
    }

    public CBVar() {
    }

    public override object Get() => mValue;
}

public class CSVar : CVar {
    private string _mValue = "";
    public string mValue {
        get => _mValue;
        set {
            if (mSetter != null) {
                _mValue = mSetter(value);
            } else {
                _mValue = value;
            }
        }
    }
    private Func<string, string> mSetter = null;

    public CSVar(string @value, Func<string, string> setter = null) {
        _mValue = @value;
        mSetter = setter;
    }

    public CSVar() {
    }

    public override object Get() => mValue;
}

public class CFunc : CVar {
    private Func<string[], object> mFunc = null;

    public CFunc(Func<string[], object> func) {
        mFunc = func;
    }

    public object Call(string[] args) => mFunc(args);

    public override object Get() => null;
}

// If passed with no args, just print the value.
// If passed with args, set the value or perform some action.

public class CVarCollection {
    public class NotFunction : Exception {
        public NotFunction(string msg) : base(msg) {
        }

        public NotFunction(string msg, Exception inner) : base(msg, inner) {
        }
    }

    public class WrongType : Exception {
        public WrongType(string msg) : base(msg) {
        }

        public WrongType(string msg, Exception inner) : base(msg, inner) {
        }
    }

    public class NotFound : Exception {
        public NotFound(string msg) : base(msg) {
        }

        public NotFound(string msg, Exception inner) : base(msg, inner) {
        }
    }

    public class FunctionError : Exception {
        public FunctionError(string msg) : base(msg) {
        }

        public FunctionError(string msg, Exception inner) : base(msg, inner) {
        }
    }

    private Dictionary<string, CVar> mMap = new Dictionary<string, CVar>();

    public CVarCollection(Dictionary<string, CVar> map) => mMap = map;

    public CVarCollection() {
    }

    public object RunOrSet(string input) {
        char[] delims = {
            '\r',
            ' ',
            '\n',
        };
        string[] args = input.Split(delims, StringSplitOptions.RemoveEmptyEntries);

        string to = null;
        if (args.Length == 0) {
            throw new NotFound($"Empty string is not a function or variable");
        } else if (args.Length > 1) {
            to = args[1];
        }
        string call = args[0];

        // Remove the call from args.
        string[] argv = new string[args.Length - 1];
        for (int i = 1; i < args.Length; i++) {
            argv[i - 1] = args[i];
        }

        CVar @var = null;
        if (!mMap.TryGetValue(call, out @var)) {
            throw new NotFound($"Cannot find {call}.");
        }

        try {
            if (@var is CFunc f) {
                return f.Call(argv);
            }
        } catch (Exception e) {
            throw new FunctionError($"Error in running function {call}", e);
        }

        // Just read.
        if (to == null) {
            return @var.Get();
        }

        // Try to set the value.
        try {
            if (@var is CIVar iv) {
                iv.mValue = long.Parse(to);
            } else if (@var is CFVar fv) {
                fv.mValue = float.Parse(to);
            } else if (@var is CBVar bv) {
                bv.mValue = bool.Parse(to);
            } else if (@var is CSVar sv) {
                sv.mValue = to;
            }
            return @var.Get();
        } catch (InvalidCastException e) {
            throw new WrongType($"Cannot convert {to} to the desired type: {e}");
        }
    }

    public T TryGetValue<T>(string name, T or)

        where T : unmanaged {
        T? result = TryGetValue<T>(name);
        return result != null ? result.Value : or;
    }

    public T? TryGetValue<T>(string name)

        where T : unmanaged {
        try {
            return (T)mMap[name].Get();
        } catch (Exception) {
            return null;
        }
    }

    public T TryGet<T>(string name, T or)

        where T : class {
        T result = TryGet<T>(name);
        return result != null ? result : or;
    }

    public T TryGet<T>(string name)

        where T : class {
        try {
            return (T)mMap[name].Get();
        } catch (Exception) {
            return null;
        }
    }

    public object this[string key] => mMap[key].Get();

    public bool Register(string name, CVar @var) {
        if (mMap.ContainsKey(name)) {
            return false;
        }
        mMap[name] = @var;
        return true;
    }
}
}
