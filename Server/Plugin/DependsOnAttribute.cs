using System;
using System.Collections.Generic;
using System.Linq;

namespace DuckyNet.Server.Plugin
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class DependsOnAttribute : Attribute
    {
        public DependsOnAttribute(params string[] dependencies)
        {
            Dependencies = dependencies?.ToArray() ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Dependencies { get; }
    }
}
