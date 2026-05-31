using System.Text.Json.Nodes;

namespace SpikeDce.Mapping;

// Interprets a MappingSpec over a canonical JsonNode → nested Dictionary<string,object?> (the builder-dict).
public sealed class MappingEngine
{
    public Dictionary<string, object?> Apply(JsonNode canonical, MappingSpec spec)
    {
        var derived = new Dictionary<string, object?>();
        foreach (var d in spec.Derive)
        {
            var o = d!.AsObject();
            derived[o["name"]!.GetValue<string>()] = Transforms.Invoke(o["fn"]!.GetValue<string>(), ResolveArgs(o["args"]?.AsArray(), canonical, derived));
        }

        var root = new Dictionary<string, object?>();
        foreach (var r in spec.Rules) ApplyRule(r!.AsObject(), canonical, derived, root);
        return root;
    }

    private void ApplyRule(JsonObject rule, JsonNode scope, Dictionary<string, object?> derived, Dictionary<string, object?> root)
    {
        if (rule["when"] is JsonValue w && !EvalWhen(w.GetValue<string>(), scope, derived)) return;

        if (rule["forEach"] is JsonValue fe)
        {
            var arr = ResolveNode(fe.GetValue<string>(), scope, derived) as JsonArray ?? new JsonArray();
            var target = rule["target"]!.GetValue<string>();        // e.g. "infDCe.det[]"
            var index = rule["index"]?.GetValue<string>();
            var subRules = rule["rules"]!.AsArray();
            int i = 1;
            foreach (var item in arr)
            {
                var child = new Dictionary<string, object?>();
                if (index is not null) child[index] = i.ToString();
                foreach (var sub in subRules) ApplyRule(sub!.AsObject(), item!, derived, child);
                AppendList(root, target, child);
                i++;
            }
            return;
        }

        object? value =
            rule["const"] is JsonNode c ? JsonToClr(c) :
            rule["fn"] is JsonValue fn ? Transforms.Invoke(fn.GetValue<string>(), ResolveArgs(rule["args"]?.AsArray(), scope, derived)) :
            rule["from"] is JsonValue from ? ResolveValue(from.GetValue<string>(), scope, derived) :
            null;

        if (value is not null) SetPath(root, rule["target"]!.GetValue<string>(), value);
    }

    private IReadOnlyList<object?> ResolveArgs(JsonArray? args, JsonNode scope, Dictionary<string, object?> derived)
    {
        var list = new List<object?>();
        if (args is null) return list;
        foreach (var a in args)
        {
            var o = a!.AsObject();
            list.Add(o.ContainsKey("const") ? JsonToClr(o["const"]!) : ResolveValue(o["from"]!.GetValue<string>(), scope, derived));
        }
        return list;
    }

    // "$name" → derived value; otherwise dotted path into the canonical scope (returns CLR scalar).
    private object? ResolveValue(string path, JsonNode scope, Dictionary<string, object?> derived)
    {
        if (path.StartsWith('$')) return derived.TryGetValue(path[1..], out var v) ? v : null;
        var node = ResolveNode(path, scope, derived);
        return node is null ? null : JsonToClr(node);
    }

    private JsonNode? ResolveNode(string path, JsonNode scope, Dictionary<string, object?> derived)
    {
        if (path.StartsWith('$'))
            return derived.TryGetValue(path[1..], out var v) && v is JsonNode n ? n : null;
        JsonNode? cur = scope;
        foreach (var seg in path.Split('.'))
        {
            if (cur is null) return null;
            cur = cur is JsonObject obj && obj.TryGetPropertyValue(seg, out var next) ? next : null;
        }
        return cur;
    }

    private static object? JsonToClr(JsonNode n) => n is JsonValue v
        ? (v.TryGetValue<string>(out var s)  ? s
           : v.TryGetValue<bool>(out var b)  ? b
           : v.TryGetValue<decimal>(out var d) ? d
           : v.TryGetValue<long>(out var l)  ? (decimal)l
           : v.TryGetValue<int>(out var i)   ? (decimal)i
           : v.TryGetValue<double>(out var db) ? (decimal)db
           : (object?)v.ToString())
        : n;

    private static void SetPath(Dictionary<string, object?> root, string path, object? value)
    {
        var segs = path.Split('.');
        var cur = root;
        for (int i = 0; i < segs.Length - 1; i++)
        {
            if (!cur.TryGetValue(segs[i], out var child) || child is not Dictionary<string, object?> m)
            {
                m = new Dictionary<string, object?>();
                cur[segs[i]] = m;
            }
            cur = m;
        }
        cur[segs[^1]] = value;
    }

    private static void AppendList(Dictionary<string, object?> root, string targetWithBrackets, Dictionary<string, object?> item)
    {
        var path = targetWithBrackets[..^2]; // strip trailing "[]"
        var segs = path.Split('.');
        var cur = root;
        for (int i = 0; i < segs.Length - 1; i++)
        {
            if (!cur.TryGetValue(segs[i], out var child) || child is not Dictionary<string, object?> m)
            {
                m = new Dictionary<string, object?>();
                cur[segs[i]] = m;
            }
            cur = m;
        }
        if (!cur.TryGetValue(segs[^1], out var listObj) || listObj is not List<object?> list)
        {
            list = new List<object?>();
            cur[segs[^1]] = list;
        }
        list.Add(item);
    }

    // when: only two supported forms — "<path> != null" and "<path> == <literal>"
    private bool EvalWhen(string expr, JsonNode scope, Dictionary<string, object?> derived)
    {
        var neq = expr.Split("!=", 2);
        if (neq.Length == 2 && neq[1].Trim() == "null")
            return ResolveNode(neq[0].Trim(), scope, derived) is not null;

        var eq = expr.Split("==", 2);
        if (eq.Length == 2)
        {
            var actual = ResolveValue(eq[0].Trim(), scope, derived)?.ToString();
            var expected = eq[1].Trim().Trim('"', '\'');
            return actual == expected;
        }
        throw new InvalidOperationException($"unsupported when expression: '{expr}'");
    }
}
