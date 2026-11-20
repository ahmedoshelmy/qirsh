namespace Qirsh.Ledger.Domain;

public enum Direction
{
    Out,
    In,
}

public enum RuleSource
{
    /// <summary>Shipped keyword ("UBER" → Transport), matched anywhere in the merchant.</summary>
    Keyword,

    /// <summary>Learned when the owner moved a transaction from this exact merchant to another category.</summary>
    Learned,
}
