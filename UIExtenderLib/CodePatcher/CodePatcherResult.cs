using System;

namespace UIExtenderLib.CodePatcher
{
    [Flags]
    public enum CodePatcherResult : int
    {
        Success = 1,
        Failure = 2,
        Partial = 4,
    }
}