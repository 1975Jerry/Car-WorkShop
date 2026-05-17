namespace Workshop.Application.Features.RetailCases;

public static class RetailCaseCalculator
{
    /// <summary>
    /// Returns the gross total for a retail case (cost + VAT) rounded to 2 decimals.
    /// VAT amount is supplied directly — retail jobs use a single agreed VAT amount,
    /// not a derived rate.
    /// </summary>
    public static decimal TotalWithVat(decimal finalCost, decimal vatAmount) =>
        Math.Round(finalCost + vatAmount, 2, MidpointRounding.AwayFromZero);
}
