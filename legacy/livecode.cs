    public static Result<int> Bind(Result<int> r, Func<int, Result<int>> f)
    {
        if (r is Result<int>.Fail(var e))
            return new Result<int>.Fail(e);
        var ok=(Result<int>.Ok)r;
        return f(ok.Value);
    }