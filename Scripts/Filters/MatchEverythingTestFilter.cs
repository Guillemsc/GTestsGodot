#if TOOLS

using NUnit.Framework.Interfaces;

namespace GTestsGodot.Filters;

public sealed class MatchEverythingTestFilter : ITestFilter
{
    public static readonly MatchEverythingTestFilter Instance = new();
    
    MatchEverythingTestFilter()
    {
        
    }
    
    public TNode AddToXml(TNode parentNode, bool recursive) => null;
    public TNode ToXml(bool recursive) => null;

    public bool IsExplicitMatch(ITest test) => true;
    public bool Pass(ITest test) => true;
}

#endif