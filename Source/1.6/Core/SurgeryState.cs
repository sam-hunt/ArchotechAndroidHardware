namespace ArchotechAndroidHardware;

/// <summary>
/// Shared state for surgery coordination between Harmony patches and hediff classes.
///
/// SuppressBodyPartDestruction is set by RecipeInstallAndroidPart_ApplyOnPawn's Prefix
/// during replacement surgery. AAH_ hediff classes check this flag in PostRemoved to
/// avoid adding MissingBodyPart when a new part is about to be installed in the same slot.
/// </summary>
internal static class SurgeryState
{
    internal static bool SuppressBodyPartDestruction;
}
