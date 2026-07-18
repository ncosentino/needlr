namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Calculates one-sided Wilson score evidence for a binary sample.
/// </summary>
internal static class ExperimentWilsonScoreCalculator
{
    public static (double Estimate, double Lower, double Upper) Calculate(
        int successCount,
        int sampleCount,
        double confidenceLevel)
    {
        var estimate = (double)successCount / sampleCount;
        var z = InverseStandardNormal(confidenceLevel);
        var zSquared = z * z;
        var denominator = 1 + zSquared / sampleCount;
        var center = estimate + zSquared / (2 * sampleCount);
        var margin = z * Math.Sqrt(
            estimate * (1 - estimate) / sampleCount
            + zSquared / (4d * sampleCount * sampleCount));
        var lower = successCount == 0
            ? 0
            : Math.Max(0, (center - margin) / denominator);
        var upper = successCount == sampleCount
            ? 1
            : Math.Min(1, (center + margin) / denominator);
        return (estimate, lower, upper);
    }

    private static double InverseStandardNormal(double probability)
    {
        const double a1 = -3.969683028665376e+01;
        const double a2 = 2.209460984245205e+02;
        const double a3 = -2.759285104469687e+02;
        const double a4 = 1.383577518672690e+02;
        const double a5 = -3.066479806614716e+01;
        const double a6 = 2.506628277459239e+00;
        const double b1 = -5.447609879822406e+01;
        const double b2 = 1.615858368580409e+02;
        const double b3 = -1.556989798598866e+02;
        const double b4 = 6.680131188771972e+01;
        const double b5 = -1.328068155288572e+01;
        const double c1 = -7.784894002430293e-03;
        const double c2 = -3.223964580411365e-01;
        const double c3 = -2.400758277161838e+00;
        const double c4 = -2.549732539343734e+00;
        const double c5 = 4.374664141464968e+00;
        const double c6 = 2.938163982698783e+00;
        const double d1 = 7.784695709041462e-03;
        const double d2 = 3.224671290700398e-01;
        const double d3 = 2.445134137142996e+00;
        const double d4 = 3.754408661907416e+00;
        const double lowerRegion = 0.02425;
        const double upperRegion = 1 - lowerRegion;
        if (probability < lowerRegion)
        {
            var q = Math.Sqrt(-2 * Math.Log(probability));
            return (((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6)
                / ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
        }

        if (probability <= upperRegion)
        {
            var q = probability - 0.5;
            var r = q * q;
            return (((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6)
                * q
                / (((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1);
        }

        var upperQ = Math.Sqrt(-2 * Math.Log(1 - probability));
        return -(((((c1 * upperQ + c2) * upperQ + c3) * upperQ + c4) * upperQ + c5)
                * upperQ + c6)
            / ((((d1 * upperQ + d2) * upperQ + d3) * upperQ + d4) * upperQ + 1);
    }
}
