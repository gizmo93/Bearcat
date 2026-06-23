namespace Bearcat.Domain.ValueObjects;

public enum QualityGateState
{
    NotEvaluated = 1,
    Passed = 2,
    Failed = 3,
    ManuallyApproved = 4,
}
