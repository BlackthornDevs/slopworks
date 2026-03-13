using UnityEngine;

public enum BeltValidationError
{
    None,
    TooShort,
    TooLong,
    TooSteep,
    TurnTooSharp
}

public struct BeltValidationResult
{
    public bool IsValid;
    public BeltValidationError Error;

    public static BeltValidationResult Valid()
    {
        return new BeltValidationResult { IsValid = true, Error = BeltValidationError.None };
    }

    public static BeltValidationResult Invalid(BeltValidationError error)
    {
        return new BeltValidationResult { IsValid = false, Error = error };
    }
}

/// <summary>
/// Validates belt placement parameters before sending to server.
/// Pure math -- no MonoBehaviour, no side effects.
/// </summary>
public static class BeltPlacementValidator
{
    public const float MinLength = 0.5f;
    public const float MaxLength = 56f;
    public const float MaxSlopeAngle = BeltRouteBuilder.MaxRampAngle;
    public const float MinTurnAngle = 30f; // minimum angle between startDir and endDir

    public static BeltValidationResult Validate(
        Vector3 startPos, Vector3 startDir,
        Vector3 endPos, Vector3 endDir)
    {
        // Zero endDir signals an invalid direction (e.g. straight backward)
        if (endDir.sqrMagnitude < 0.001f)
            return BeltValidationResult.Invalid(BeltValidationError.TurnTooSharp);

        float distance = Vector3.Distance(startPos, endPos);

        if (distance < MinLength)
            return BeltValidationResult.Invalid(BeltValidationError.TooShort);

        if (distance > MaxLength)
            return BeltValidationResult.Invalid(BeltValidationError.TooLong);

        float horizontalDist = new Vector2(endPos.x - startPos.x, endPos.z - startPos.z).magnitude;
        float verticalDist = Mathf.Abs(endPos.y - startPos.y);

        if (horizontalDist > 0.001f)
        {
            float slopeAngle = Mathf.Atan2(verticalDist, horizontalDist) * Mathf.Rad2Deg;
            if (slopeAngle > MaxSlopeAngle)
                return BeltValidationResult.Invalid(BeltValidationError.TooSteep);
        }
        else if (verticalDist > 0.001f)
        {
            return BeltValidationResult.Invalid(BeltValidationError.TooSteep);
        }

        // Turn angle is not rejected here. Zero endDir (straight backward with
        // no offset) is caught above. All other angles including U-turns are valid
        // -- the route builder and per-mode validation in NetworkBuildController
        // handle turn geometry constraints.

        return BeltValidationResult.Valid();
    }
}
