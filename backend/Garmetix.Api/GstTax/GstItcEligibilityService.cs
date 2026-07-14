namespace Garmetix.Api.GstTax;

/// <summary>
/// Shared ITC (Input Tax Credit) eligibility heuristic, first introduced in Stage GST-8 (Purchase GST Review)
/// and reused as-is by Stage GST-9 (ITC Register) so both features agree on exactly the same classification.
/// This is a heuristic, not a formal ledger calculation - this codebase has no ITC ledger table. "Not Eligible"
/// when the purchase invoice itself carries no vendor GSTIN (no valid tax invoice for ITC). "At Risk" when the
/// vendor master's own GST registration status is on record as something other than Active (e.g.
/// cancelled/suspended). "Unverified" when a GSTIN is present but has never been confirmed via the GST & Taxes
/// GSTIN Verification tool (Stage GST-3). "Eligible" otherwise.
/// </summary>
public static class GstItcEligibilityService
{
    public const string Eligible = "Eligible";
    public const string Unverified = "Unverified";
    public const string AtRisk = "At Risk";
    public const string NotEligible = "Not Eligible";

    public sealed record VendorGstSnapshot(Guid VendorId, bool GSTVerified, string? GSTRegistrationStatus);

    public static (string Status, string Note) Resolve(
        string? vendorGstin,
        Guid vendorId,
        IReadOnlyDictionary<Guid, VendorGstSnapshot> vendors)
    {
        if (string.IsNullOrWhiteSpace(vendorGstin))
        {
            return (NotEligible, "No vendor GSTIN captured on this purchase invoice - ITC cannot be claimed without one.");
        }

        if (!vendors.TryGetValue(vendorId, out var vendor))
        {
            return (Unverified, "Vendor GSTIN present but vendor master record was not found for a verification check.");
        }

        if (!string.IsNullOrWhiteSpace(vendor.GSTRegistrationStatus) && !string.Equals(vendor.GSTRegistrationStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return (AtRisk, $"Vendor GST registration status is on record as \"{vendor.GSTRegistrationStatus}\", not Active.");
        }

        if (!vendor.GSTVerified)
        {
            return (Unverified, "Vendor GSTIN has not been verified via GST & Taxes GSTIN Verification.");
        }

        return (Eligible, "Vendor GSTIN present and verified.");
    }

    /// <summary>Amount treated as claimable for register/export purposes: full tax when Eligible/Unverified, zero when At Risk/Not Eligible.</summary>
    public static bool IsClaimable(string status) => status is Eligible or Unverified;
}
