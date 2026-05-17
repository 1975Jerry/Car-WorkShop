namespace Workshop.Application.Features.InsuranceParts;

public static class InsurancePartLineCalculator
{
    public static decimal Total(decimal quantity, decimal unitCost, decimal? discountPct)
    {
        var discount = discountPct ?? 0m;
        if (discount < 0) discount = 0;
        if (discount > 100) discount = 100;
        var raw = quantity * unitCost;
        return Math.Round(raw * (1 - discount / 100m), 2, MidpointRounding.AwayFromZero);
    }
}
