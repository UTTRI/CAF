namespace CellphoneProcessor.Utilities.OTP;

/// <summary>
/// The resulting transit LoS information
/// </summary>
/// <param name="Transfers">The number of transfers along the path.</param>
/// <param name="Ivtt">The in-vehicle time stored in minutes.</param>
/// <param name="Walk">The Walk time stored in minutes.</param>
/// <param name="Wait">The Wait time stored in minutes.</param>
public record struct TransitLoS(int Transfers, double Ivtt, double Walk, double Wait)
{
    /// <summary>
    /// Get a default value for the transit LoS for an invalid request
    /// </summary>
    /// <returns>An invalid TransitLoS</returns>
    public static TransitLoS GetBadRequest()
    {
        return new TransitLoS(-1, -1, -1, -1);
    }

    internal static TransitLoS GetIntrazonal()
    {
        return new TransitLoS(-2, -2, -2, -2);
    }

    /// <summary>
    /// Check to see if the TransitLoS bundle is invalid
    /// </summary>
    /// <returns>True if it is invalid, false otherwise.</returns>
    public bool IsInvalid()
    {
        return (Ivtt < 0) | (Walk < 0) | (Wait < 0) | (Transfers < 0);
    }

    /// <summary>
    /// Gets the error message if the trip was too short for transit.
    /// </summary>
    /// <returns>True if the trip was too short for transit.</returns>
    public bool IsTooShort()
    {
        return Ivtt == -2;
    }
}

